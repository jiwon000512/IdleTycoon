using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5: 빵집 규칙의 단일 소유자. 손님 도착 → 진열대 → 줄 → 계산, 오븐 굽기, 업그레이드·빵 해금.
    // 걷는 시간도 여기서 센다. World는 이벤트를 받아 그 시간에 맞춰 걷기만 한다.
    // 굴 격자 설계 v0.5: 진열대·오븐은 굴 칸(BurrowGrid)의 자리에 놓이고, 걷기는 칸 경로 길이 ÷ walkSpeed(방에 들어서는 첫 걸음은 enter·toQueue·wombatWalk에 포함)
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
        private readonly BurrowGrid m_grid;
        private readonly List<BreadRecord> m_unlocked = new List<BreadRecord>();
        private readonly Dictionary<Cell, BreadRecord> m_shelves = new Dictionary<Cell, BreadRecord>();
        private readonly Dictionary<string, int> m_stock = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_levels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Oven> m_ovens = new List<Oven>();
        private readonly List<Customer> m_customers = new List<Customer>();
        private readonly List<Customer> m_queue = new List<Customer>();
        private double m_arrivalElapsed;
        private int m_nextCustomerId;
        private int m_errandOven = -1;
        private bool m_errandReturning;
        private double m_errandTimer;

        public BurrowGrid Grid => m_grid;
        public IReadOnlyList<BreadRecord> UnlockedBreads => m_unlocked;
        public IReadOnlyDictionary<Cell, BreadRecord> Shelves => m_shelves;
        public IReadOnlyList<Oven> Ovens => m_ovens;
        public IReadOnlyList<Customer> Customers => m_customers;
        public IReadOnlyList<Customer> Queue => m_queue;
        public int ShelfCapacity => m_config.ShelfCapacity + (int)Effect(k_ShelfCapacity);
        public BreadRecord NextBread => m_unlocked.Count < m_tables.Breads.Count ? m_tables.Breads[m_unlocked.Count] : null;
        // v0.6: 웜뱃이 계산대에 있나(심부름 중이면 계산이 멈춘다)
        public bool WombatAtCounter => m_errandOven < 0;

        public event Action<Customer> CustomerArrived;
        public event Action<Customer> CustomerPicked;
        public event Action QueueChanged;
        public event Action<Customer, double> CustomerPaid;
        public event Action<Customer> CustomerGaveUp;
        public event Action<string> StockChanged;
        public event Action<int> OvenChanged;
        // 굴을 팠거나 자리에 진열대·오븐이 놓였다
        public event Action LayoutChanged;
        public event Action UpgradesChanged;
        // v0.6 웜뱃 심부름: (오븐, 걸리는 초)
        public event Action<int, double> WombatLeft;
        public event Action<int, double> WombatAtOven;
        public event Action WombatReturned;

        // 첫 손님은 첫 틱에 온다
        public ShopSim(ZooState state, GameTables tables, IRandom random)
        {
            m_state = state;
            m_tables = tables;
            m_config = tables.Config.Shop;
            m_random = random;
            m_grid = new BurrowGrid(state, m_config);
            m_grid.Dug += Grid_Dug;
            m_arrivalElapsed = m_config.ArrivalSeconds;

            // 시작 배치: 왼쪽 열(−1)의 자리 줄 두 곳에 첫 빵과 오븐. 오른쪽 열(0)은 빈 자리
            Unlock(tables.Breads[0], new Cell(-1, 1));
            m_ovens.Add(new Oven { Cell = new Cell(-1, 3) });
        }

        // 자리는 있는데 진열대도 오븐도 없는 칸
        public IEnumerable<Cell> EmptySlots
        {
            get
            {
                foreach (Cell cell in m_grid.Cells)
                {
                    if (IsEmptySlot(cell))
                    {
                        yield return cell;
                    }
                }
            }
        }

        public bool IsEmptySlot(Cell cell)
        {
            return m_grid.HasSlot(cell) && !m_shelves.ContainsKey(cell) && OvenAt(cell) < 0;
        }

        // 그 칸의 오븐 번호. 없으면 −1
        public int OvenAt(Cell cell)
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                if (m_ovens[i].Cell.Equals(cell))
                {
                    return i;
                }
            }

            return -1;
        }

        public Cell ShelfCell(string breadId)
        {
            foreach (KeyValuePair<Cell, BreadRecord> pair in m_shelves)
            {
                if (pair.Value.Id == breadId)
                {
                    return pair.Key;
                }
            }

            throw new KeyNotFoundException($"빵 '{breadId}'의 진열대가 없다.");
        }

        // 칸 사이 걷는 시간(초). 방에 들어서는 마지막 한 걸음(세로 한 칸)은 enter·toQueue·wombatWalk 시간이 맡는다
        public double WalkSeconds(Cell from, Cell to)
        {
            return Math.Max(0d, m_grid.Distance(from, to) - m_config.CellHeight) / m_config.WalkSpeed;
        }

        public int Stock(string breadId)
        {
            return m_stock[breadId];
        }

        // 오븐 외형 2단계(shop_upgrades.oven_speed.lookLevel 이상)
        public bool OvenLookUpgraded
        {
            get
            {
                ShopUpgradeRecord record = GetUpgrade(k_OvenSpeed);
                return record.LookLevel > 0 && UpgradeLevel(k_OvenSpeed) >= record.LookLevel;
            }
        }

        // 사물 시트 효과 전후 표시용: 그 단계일 때의 게임 값(진열대 용량·오븐 수·속도 배수)
        public double UpgradeValue(string upgradeId, int level)
        {
            double effect = GetUpgrade(upgradeId).EffectPerLevel * level;

            switch (upgradeId)
            {
                case k_ShelfCapacity: return m_config.ShelfCapacity + effect;
                case k_OvenCount: return m_config.OvenCount + effect;
                default: return 1d + effect;
            }
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

        // 순서: 웜뱃 심부름 → 오븐(재고 보충) → 손님 단계 → 계산 → 도착
        public void Tick(double dt)
        {
            TickErrand(dt);
            TickOvens(dt);
            TickCustomers(dt);
            TickCheckout(dt);
            TickArrival(dt);
        }

        public bool TryBake(int ovenIndex, string breadId)
        {
            Oven oven = m_ovens[ovenIndex];
            BreadRecord bread = m_unlocked.Find(b => b.Id == breadId);

            if (!oven.IsEmpty || bread == null || !WombatAtCounter)
            {
                return false;
            }

            oven.Bread = bread;
            oven.Started = false;
            oven.Remaining = bread.BakeSeconds;
            m_errandOven = ovenIndex;
            m_errandReturning = false;
            m_errandTimer = ErrandSeconds(ovenIndex);
            OnOvenChanged(ovenIndex);
            OnWombatLeft(ovenIndex, m_errandTimer);
            return true;
        }

        public bool TryUpgrade(string upgradeId)
        {
            if (IsMaxed(upgradeId) || !m_state.TrySpendCoins(UpgradeCost(upgradeId)))
            {
                return false;
            }

            m_levels[upgradeId] = UpgradeLevel(upgradeId) + 1;
            OnUpgradesChanged();
            return true;
        }

        // 오븐 추가(oven_count)는 빈 자리를 골라 산다
        public bool TryAddOven(Cell cell)
        {
            if (!IsEmptySlot(cell) || !TryUpgrade(k_OvenCount))
            {
                return false;
            }

            m_ovens.Add(new Oven { Cell = cell });
            OnLayoutChanged();
            return true;
        }

        // 빵은 breads.json 행 순서대로만 해금한다(설계 08 결정 1). 진열대는 고른 빈 자리에
        public bool TryUnlockNextBread(Cell cell)
        {
            BreadRecord next = NextBread;

            if (next == null || !IsEmptySlot(cell) || !m_state.TrySpendCoins(next.UnlockCost))
            {
                return false;
            }

            Unlock(next, cell);
            OnLayoutChanged();
            OnUpgradesChanged();
            return true;
        }

        private void Unlock(BreadRecord bread, Cell cell)
        {
            m_unlocked.Add(bread);
            m_shelves[cell] = bread;
            m_stock[bread.Id] = 0;
        }

        private void Grid_Dug(Cell cell)
        {
            OnLayoutChanged();
        }

        // v0.6: 오븐까지 걸어가 굽기를 시작하고 같은 시간 걸려 돌아온다
        private void TickErrand(double dt)
        {
            if (WombatAtCounter)
            {
                return;
            }

            m_errandTimer -= dt;

            if (m_errandTimer > 0d)
            {
                return;
            }

            if (!m_errandReturning)
            {
                m_errandReturning = true;
                m_errandTimer = ErrandSeconds(m_errandOven);
                m_ovens[m_errandOven].Started = true;
                OnOvenChanged(m_errandOven);
                OnWombatAtOven(m_errandOven, m_errandTimer);
                return;
            }

            m_errandOven = -1;
            OnWombatReturned();
        }

        private double ErrandSeconds(int ovenIndex)
        {
            return m_config.WombatWalkSeconds + WalkSeconds(m_grid.CounterNear(m_ovens[ovenIndex].Cell), m_ovens[ovenIndex].Cell);
        }

        // 다 구운 빵은 오븐에서 기다리다 진열대에 자리가 나는 만큼 옮겨진다(설계 08 결정 3)
        private void TickOvens(double dt)
        {
            for (int i = 0; i < m_ovens.Count; i++)
            {
                Oven oven = m_ovens[i];

                if (oven.IsEmpty || !oven.Started)
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
                    oven.Started = false;
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
            customer.Timer = m_config.ToQueueSeconds + WalkSeconds(customer.Cell, m_grid.CounterNear(customer.Cell));
            OnStockChanged(customer.Bread.Id);
            OnCustomerPicked(customer);
            return true;
        }

        // 줄 머리만 계산한다. 남은 작업량은 다음 손님에게 넘긴다
        private void TickCheckout(double dt)
        {
            if (m_queue.Count == 0 || !WombatAtCounter)
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
            BreadRecord bread = m_unlocked[PickSlot()];
            Cell cell = ShelfCell(bread.Id);
            Customer customer = new Customer(++m_nextCustomerId, bread, cell, m_config.EnterSeconds + WalkSeconds(m_grid.EntranceNear(cell), cell));
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

        private void OnWombatLeft(int ovenIndex, double seconds)
        {
            WombatLeft?.Invoke(ovenIndex, seconds);
        }

        private void OnWombatAtOven(int ovenIndex, double seconds)
        {
            WombatAtOven?.Invoke(ovenIndex, seconds);
        }

        private void OnWombatReturned()
        {
            WombatReturned?.Invoke();
        }
    }
}
