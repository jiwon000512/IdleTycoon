using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5: 빵집 규칙의 단일 소유자. 손님 도착 → 진열대 → 줄 → 계산, 오븐 굽기, 업그레이드·빵 해금.
    // 걷는 시간도 여기서 센다. World는 이벤트를 받아 그 시간에 맞춰 걷기만 한다
    public sealed class ShopSim
    {
        public const string k_OvenCount = "oven_count";
        public const string k_OvenSpeed = "oven_speed";
        public const string k_ShelfCapacity = "shelf_capacity";
        public const string k_CheckoutSpeed = "checkout_speed";

        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly GameConfig.ShopConfig m_config;
        private readonly IRandom m_random;
        private readonly List<BreadRecord> m_unlocked = new List<BreadRecord>();
        private readonly Dictionary<string, int> m_stock = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_levels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Oven> m_ovens = new List<Oven>();
        private readonly List<Customer> m_customers = new List<Customer>();
        private readonly List<Customer> m_queue = new List<Customer>();
        private double m_arrivalElapsed;
        private int m_nextCustomerId;

        public IReadOnlyList<BreadRecord> UnlockedBreads => m_unlocked;
        public IReadOnlyList<Oven> Ovens => m_ovens;
        public IReadOnlyList<Customer> Customers => m_customers;
        public IReadOnlyList<Customer> Queue => m_queue;
        public int ShelfRows => (m_unlocked.Count + 1) / 2;
        public int OvenRows => (m_ovens.Count + 1) / 2;
        public int ShelfCapacity => m_config.ShelfCapacity + (int)Effect(k_ShelfCapacity);
        public BreadRecord NextBread => m_unlocked.Count < m_tables.Breads.Count ? m_tables.Breads[m_unlocked.Count] : null;

        public event Action<Customer> CustomerArrived;
        public event Action<Customer> CustomerPicked;
        public event Action QueueChanged;
        public event Action<Customer, double> CustomerPaid;
        public event Action<Customer> CustomerGaveUp;
        public event Action<string> StockChanged;
        public event Action<int> OvenChanged;
        public event Action LayoutChanged;
        public event Action UpgradesChanged;

        // 첫 손님은 첫 틱에 온다
        public ShopSim(ZooState state, GameTables tables, IRandom random)
        {
            m_state = state;
            m_tables = tables;
            m_config = tables.Config.Shop;
            m_random = random;
            m_arrivalElapsed = m_config.ArrivalSeconds;

            Unlock(tables.Breads[0]);

            for (int i = 0; i < m_config.OvenCount; i++)
            {
                m_ovens.Add(new Oven());
            }
        }

        public int Stock(string breadId)
        {
            return m_stock[breadId];
        }

        public int UpgradeLevel(string upgradeId)
        {
            m_levels.TryGetValue(upgradeId, out int level);
            return level;
        }

        public double UpgradeCost(string upgradeId)
        {
            ShopUpgradeRecord record = GetUpgrade(upgradeId);
            return record.BaseCost * Math.Pow(record.CostGrowth, UpgradeLevel(upgradeId));
        }

        public bool IsMaxed(string upgradeId)
        {
            return UpgradeLevel(upgradeId) >= GetUpgrade(upgradeId).MaxLevel;
        }

        // 순서: 오븐(재고 보충) → 손님 단계 → 계산 → 도착
        public void Tick(double dt)
        {
            TickOvens(dt);
            TickCustomers(dt);
            TickCheckout(dt);
            TickArrival(dt);
        }

        public bool TryBake(int ovenIndex, string breadId)
        {
            Oven oven = m_ovens[ovenIndex];
            BreadRecord bread = m_unlocked.Find(b => b.Id == breadId);

            if (!oven.IsEmpty || bread == null)
            {
                return false;
            }

            oven.Bread = bread;
            oven.Remaining = bread.BakeSeconds;
            OnOvenChanged(ovenIndex);
            return true;
        }

        public bool TryUpgrade(string upgradeId)
        {
            if (IsMaxed(upgradeId) || !m_state.TrySpendCoins(UpgradeCost(upgradeId)))
            {
                return false;
            }

            m_levels[upgradeId] = UpgradeLevel(upgradeId) + 1;

            if (upgradeId == k_OvenCount)
            {
                m_ovens.Add(new Oven());
                OnLayoutChanged();
            }

            OnUpgradesChanged();
            return true;
        }

        // 빵은 breads.json 행 순서대로만 해금한다(설계 08 결정 1). 자리 = 해금 순서
        public bool TryUnlockNextBread()
        {
            BreadRecord next = NextBread;

            if (next == null || !m_state.TrySpendCoins(next.UnlockCost))
            {
                return false;
            }

            Unlock(next);
            OnLayoutChanged();
            OnUpgradesChanged();
            return true;
        }

        private void Unlock(BreadRecord bread)
        {
            m_unlocked.Add(bread);
            m_stock[bread.Id] = 0;
        }

        // 다 구운 빵은 오븐에서 기다리다 진열대에 자리가 나는 만큼 옮겨진다(설계 08 결정 3)
        private void TickOvens(double dt)
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                Oven oven = m_ovens[i];

                if (oven.IsEmpty)
                {
                    continue;
                }

                if (oven.Remaining > 0d)
                {
                    oven.Remaining -= dt * (1d + Effect(k_OvenSpeed));

                    if (oven.Remaining <= 0d)
                    {
                        oven.Remaining = 0d;
                        oven.Ready = oven.Bread.BatchSize;
                    }
                }

                int moved = Math.Min(oven.Ready, ShelfCapacity - m_stock[oven.Bread.Id]);

                if (moved > 0)
                {
                    oven.Ready -= moved;
                    m_stock[oven.Bread.Id] += moved;
                    OnStockChanged(oven.Bread.Id);
                }

                if (oven.Remaining <= 0d && oven.Ready == 0)
                {
                    oven.Bread = null;
                }

                OnOvenChanged(i);
            }
        }

        // 도착 순서대로 돌아야 같은 틱에 줄에 서는 손님의 순서가 맞다
        private void TickCustomers(double dt)
        {
            for (int i = 0; i < m_customers.Count; i++)
            {
                Customer customer = m_customers[i];

                switch (customer.Phase)
                {
                    case CustomerPhase.ToShelf:
                        customer.Timer -= dt;

                        if (customer.Timer <= 0d)
                        {
                            customer.Phase = CustomerPhase.AtShelf;
                            customer.Timer = m_config.PatienceSeconds;
                            TryPick(customer);
                        }

                        break;

                    case CustomerPhase.AtShelf:
                        if (TryPick(customer))
                        {
                            break;
                        }

                        customer.Timer -= dt;

                        if (customer.Timer <= 0d)
                        {
                            m_customers.RemoveAt(i);
                            i--;
                            OnCustomerGaveUp(customer);
                        }

                        break;

                    case CustomerPhase.ToQueue:
                        customer.Timer -= dt;

                        if (customer.Timer <= 0d)
                        {
                            customer.Phase = CustomerPhase.Queued;
                            customer.Timer = m_config.CheckoutSeconds;
                            m_queue.Add(customer);
                            OnQueueChanged();
                        }

                        break;
                }
            }
        }

        private bool TryPick(Customer customer)
        {
            if (m_stock[customer.Bread.Id] == 0)
            {
                return false;
            }

            m_stock[customer.Bread.Id]--;
            customer.Phase = CustomerPhase.ToQueue;
            customer.Timer = m_config.ToQueueSeconds;
            OnStockChanged(customer.Bread.Id);
            OnCustomerPicked(customer);
            return true;
        }

        // 줄 머리만 계산한다. 남은 작업량은 다음 손님에게 넘긴다
        private void TickCheckout(double dt)
        {
            if (m_queue.Count == 0)
            {
                return;
            }

            m_queue[0].Timer -= dt * (1d + Effect(k_CheckoutSpeed));

            while (m_queue.Count > 0 && m_queue[0].Timer <= 0d)
            {
                Customer paid = m_queue[0];
                double overflow = -paid.Timer;
                m_queue.RemoveAt(0);
                m_customers.Remove(paid);
                m_state.AddCoins(paid.Bread.Price);
                OnCustomerPaid(paid, paid.Bread.Price);

                if (m_queue.Count > 0)
                {
                    m_queue[0].Timer -= overflow;
                }

                OnQueueChanged();
            }
        }

        private void TickArrival(double dt)
        {
            m_arrivalElapsed = Math.Min(m_arrivalElapsed + dt, m_config.ArrivalSeconds);

            if (m_arrivalElapsed < m_config.ArrivalSeconds || m_customers.Count >= m_config.MaxCustomers)
            {
                return;
            }

            m_arrivalElapsed = 0d;
            int slot = PickSlot();
            int row = slot / 2;
            Customer customer = new Customer(++m_nextCustomerId, m_unlocked[slot], slot, m_config.EnterSeconds + row * m_config.RowWalkSeconds);
            m_customers.Add(customer);
            OnCustomerArrived(customer);
        }

        private int PickSlot()
        {
            int weightSum = 0;

            for (int i = 0; i < m_unlocked.Count; i++)
            {
                weightSum += m_unlocked[i].Weight;
            }

            double roll = m_random.NextDouble() * weightSum;

            for (int i = 0; i < m_unlocked.Count; i++)
            {
                roll -= m_unlocked[i].Weight;

                if (roll < 0d)
                {
                    return i;
                }
            }

            return m_unlocked.Count - 1;
        }

        private double Effect(string upgradeId)
        {
            return GetUpgrade(upgradeId).EffectPerLevel * UpgradeLevel(upgradeId);
        }

        private ShopUpgradeRecord GetUpgrade(string upgradeId)
        {
            for (int i = 0; i < m_tables.ShopUpgrades.Count; i++)
            {
                if (m_tables.ShopUpgrades[i].Id == upgradeId)
                {
                    return m_tables.ShopUpgrades[i];
                }
            }

            throw new KeyNotFoundException($"업그레이드 ID '{upgradeId}'가 shop_upgrades.json에 없다.");
        }

        private void OnCustomerArrived(Customer customer)
        {
            CustomerArrived?.Invoke(customer);
        }

        private void OnCustomerPicked(Customer customer)
        {
            CustomerPicked?.Invoke(customer);
        }

        private void OnQueueChanged()
        {
            QueueChanged?.Invoke();
        }

        private void OnCustomerPaid(Customer customer, double coins)
        {
            CustomerPaid?.Invoke(customer, coins);
        }

        private void OnCustomerGaveUp(Customer customer)
        {
            CustomerGaveUp?.Invoke(customer);
        }

        private void OnStockChanged(string breadId)
        {
            StockChanged?.Invoke(breadId);
        }

        private void OnOvenChanged(int ovenIndex)
        {
            OvenChanged?.Invoke(ovenIndex);
        }

        private void OnLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private void OnUpgradesChanged()
        {
            UpgradesChanged?.Invoke();
        }
    }
}
