using System;
using System.Numerics;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 08 v0.5 · 손님 동선 설계 v0.2 검증 3: 매 프레임 틱(0.02초)으로 돌린다. 실제 JSON 값(도착 4 · 걷기 2.2/초 · 톡 0.3 · 집기 0.4 ·
    // 두리번 2 · 인내 6 · 계산 1.5 · 식빵 8초 6개 10코인 가중치 3 · 크루아상 15초 4개 25코인 가중치 2). 걷는 시간은 A* 길 길이 ÷ 2.2
    public sealed class ShopSimTests
    {
        private const double k_Dt = 0.02;
        private const double k_Tolerance = 0.06;

        private ZooState m_state;
        private GameConfig.ShopConfig m_config;
        private double m_time;

        private ShopSim Create(Action<GameConfig.ShopConfig> tweak = null)
        {
            GameConfig config = TestTables.LoadConfig();
            m_config = config.Shop;
            tweak?.Invoke(config.Shop);
            GameTables tables = TestTables.Build(config: config);
            m_state = ZooState.CreateNew(config);
            m_time = 0d;
            return new ShopSim(m_state, tables, new SequenceRandom(new double[200]));
        }

        private void Run(ShopSim shop, double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                shop.Tick(k_Dt);
                m_time += k_Dt;
            }
        }

        // 조건이 참이 될 때까지 돌리고 그때 시각을 돌려준다
        private double RunUntil(ShopSim shop, Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return m_time;
                }

                shop.Tick(k_Dt);
                m_time += k_Dt;
            }

            Assert.Fail("시간 안에 조건이 참이 되지 않았다.");
            return m_time;
        }

        // 손님을 막아 두고 빵을 채운다
        private void Stock(ShopSim shop, string breadId, int count)
        {
            int before = m_config.MaxCustomers;
            m_config.MaxCustomers = 0;
            int oven = shop.OvenAt(new Cell(-1, 3));
            Assert.That(shop.TryBake(oven, breadId), Is.True);
            RunUntil(shop, () => shop.Stock(breadId) >= count && shop.WombatAtCounter);
            m_config.MaxCustomers = before;
        }

        private static float PathSeconds(ShopSim shop, Vector2 from, Vector2 to, double speed)
        {
            return (float)(BurrowNav.Length(from, shop.Layout.Nav.FindPath(from, to)) / speed);
        }

        [Test]
        public void UpgradeValue_ByUpgradeAndLevel_MatchesConfigPlusEffect()
        {
            ShopSim shop = Create();

            Assert.That(shop.UpgradeValue(ShopSim.k_ShelfCapacity, 0), Is.EqualTo(8d));
            Assert.That(shop.UpgradeValue(ShopSim.k_ShelfCapacity, 1), Is.EqualTo(12d));
            Assert.That(shop.UpgradeValue(ShopSim.k_OvenCount, 2), Is.EqualTo(3d));
            Assert.That(shop.UpgradeValue(ShopSim.k_OvenSpeed, 2), Is.EqualTo(1.4d).Within(1e-9));
        }

        [Test]
        public void Arrival_FirstOnFirstTick_ThenEveryArrivalSeconds()
        {
            ShopSim shop = Create(c => c.PatienceSeconds = 1000d);
            int arrived = 0;
            shop.CustomerArrived += _ => arrived++;

            Run(shop, 9d);

            Assert.That(arrived, Is.EqualTo(3));
        }

        [Test]
        public void Arrival_StopsAtMaxCustomers()
        {
            ShopSim shop = Create(c =>
            {
                c.PatienceSeconds = 1000d;
                c.ArrivalSeconds = 1d;
            });

            Run(shop, 20d);

            Assert.That(shop.Customers.Count, Is.EqualTo(8));
        }

        // 구멍에서 톡(0.3초) → A* 길을 2.2/초로 → 도착하자마자 집기
        [Test]
        public void Customer_WithStock_PicksAfterHopAndPathWalk()
        {
            ShopSim shop = Create();
            Stock(shop, "b01", 6);
            double arrivedAt = -1d, pickedAt = -1d;
            Customer first = null;
            shop.CustomerArrived += c =>
            {
                first ??= c;
                arrivedAt = m_time;
            };
            shop.CustomerPicked += c => pickedAt = m_time;

            RunUntil(shop, () => pickedAt >= 0d);

            Vector2 spot = shop.Layout.ShelfSpots(new Cell(-1, 1))[0];
            double expected = m_config.HopSeconds + PathSeconds(shop, shop.Layout.HoleFloor, spot, m_config.WalkSpeed);
            Assert.That(pickedAt - arrivedAt, Is.EqualTo(expected).Within(k_Tolerance * 2));
            Assert.That(Vector2.Distance(first.Position, spot), Is.LessThan(0.01f));
            Assert.That(shop.Stock("b01"), Is.EqualTo(5));
        }

        [Test]
        public void Customer_Paid_WalksOutThroughHoleAndLeaves()
        {
            ShopSim shop = Create(c => c.ArrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            double coins = m_state.Coins;
            int exited = 0;
            bool paid = false;
            shop.CustomerPaid += (c, amount) => paid = true;
            shop.CustomerExited += c => exited++;

            RunUntil(shop, () => exited == 1);

            Assert.That(paid, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + 10d));
            Assert.That(shop.Customers.Count, Is.EqualTo(0));
        }

        // 빵을 집은 뒤 줄 머리 자리까지 걸어가 선 다음에야 계산(1.5초)이 시작된다
        [Test]
        public void Checkout_StartsOnlyAfterHeadStandsAtHeadSlot()
        {
            ShopSim shop = Create(c => c.ArrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            double coins = m_state.Coins;
            Customer customer = null;
            double paidAt = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerPaid += (c, amount) => paidAt = m_time;

            double queuedAt = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Queued);
            Assert.That(m_state.Coins, Is.EqualTo(coins));
            Assert.That(Vector2.Distance(customer.Position, shop.Layout.QueueSlots[0]), Is.LessThan(0.01f));

            RunUntil(shop, () => paidAt >= 0d);
            Assert.That(paidAt - queuedAt, Is.EqualTo(m_config.CheckoutSeconds).Within(k_Tolerance));
        }

        // 원하는 빵(식빵)이 없으면 2초 두리번 → 다른 빵(크루아상) 진열대로 가서 집는다
        [Test]
        public void Customer_EmptyFirstShelf_LooksThenGoesToNextBread()
        {
            ShopSim shop = Create(c => c.ArrivalSeconds = 1000d);
            m_state.AddCoins(10000d);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.True);
            Stock(shop, "b02", 4);
            Customer customer = null;
            double picked = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerPicked += c => picked = m_time;

            double lookStart = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            Assert.That(customer.Bread.Id, Is.EqualTo("b01"));

            double leftAt = RunUntil(shop, () => customer.Phase == CustomerPhase.Walking);
            Assert.That(leftAt - lookStart, Is.EqualTo(m_config.LookSeconds).Within(k_Tolerance));
            Assert.That(customer.Bread.Id, Is.EqualTo("b02"));
            Assert.That(customer.Cell, Is.EqualTo(new Cell(0, 1)));

            RunUntil(shop, () => picked >= 0d);
            Assert.That(shop.Stock("b02"), Is.EqualTo(3));
        }

        // 빵이 하나뿐이면 그 진열대에서 인내(6초)가 다 할 때까지 두리번하고 「!!」로 떠난다
        [Test]
        public void Customer_OnlyBreadEmpty_LooksForPatienceThenGivesUp()
        {
            ShopSim shop = Create(c => c.ArrivalSeconds = 1000d);
            Customer customer = null;
            double gaveUpAt = -1d;
            int exited = 0;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerGaveUp += c => gaveUpAt = m_time;
            shop.CustomerExited += c => exited++;

            double lookStart = RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            RunUntil(shop, () => gaveUpAt >= 0d);

            Assert.That(gaveUpAt - lookStart, Is.EqualTo(m_config.PatienceSeconds).Within(k_Tolerance * 3));
            Assert.That(customer.Angry, Is.True);
            RunUntil(shop, () => exited == 1);
        }

        // 두리번하는 사이 빵이 채워지면 바로 집는다
        [Test]
        public void Customer_StockAppearsWhileLooking_PicksImmediately()
        {
            ShopSim shop = Create(c =>
            {
                c.ArrivalSeconds = 1000d;
                c.PatienceSeconds = 100d;
            });
            Customer customer = null;
            int gaveUp = 0;
            double picked = -1d;
            shop.CustomerArrived += c => customer = c;
            shop.CustomerGaveUp += c => gaveUp++;
            shop.CustomerPicked += c => picked = m_time;

            RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Looking);
            Assert.That(shop.TryBake(0, "b01"), Is.True);
            double stockedAt = RunUntil(shop, () => shop.Ovens[0].Remaining <= 0d || picked >= 0d);
            RunUntil(shop, () => picked >= 0d);

            Assert.That(picked - stockedAt, Is.LessThan(k_Tolerance));
            Assert.That(gaveUp, Is.EqualTo(0));
        }

        // 줄 머리가 계산을 마치면 뒤 손님이 머리 자리로 걸어간다
        [Test]
        public void Queue_ShiftsForwardAfterPayment()
        {
            ShopSim shop = Create(c =>
            {
                c.ArrivalSeconds = 0.5d;
                c.CheckoutSeconds = 3d;
            });
            Stock(shop, "b01", 6);
            m_config.MaxCustomers = 2;

            RunUntil(shop, () => shop.Queue.Count == 2 && shop.Queue[1].Phase == CustomerPhase.Queued);
            Customer second = shop.Queue[1];
            Assert.That(Vector2.Distance(second.Position, shop.Layout.QueueSlots[1]), Is.LessThan(0.01f));

            RunUntil(shop, () => shop.Queue.Count == 1);
            Assert.That(shop.Queue[0], Is.SameAs(second));
            RunUntil(shop, () => !second.Moving);
            Assert.That(Vector2.Distance(second.Position, shop.Layout.QueueSlots[0]), Is.LessThan(0.01f));
        }

        [Test]
        public void Oven_WhenShelfFull_KeepsRestAndCannotBake()
        {
            ShopSim shop = Create(c =>
            {
                c.MaxCustomers = 0;
                c.ShelfCapacity = 4;
            });
            shop.TryBake(0, "b01");

            RunUntil(shop, () => shop.Ovens[0].Ready > 0 && shop.WombatAtCounter);

            Assert.That(shop.Stock("b01"), Is.EqualTo(4));
            Assert.That(shop.Ovens[0].Ready, Is.EqualTo(2));
            Assert.That(shop.TryBake(0, "b01"), Is.False);
        }

        [Test]
        public void Oven_WhenAllMoved_IsEmptyAgain()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            Assert.That(shop.TryBake(0, "b01"), Is.True);
            Assert.That(shop.TryBake(0, "b01"), Is.False);

            RunUntil(shop, () => shop.Ovens[0].IsEmpty && shop.WombatAtCounter);

            Assert.That(shop.Stock("b01"), Is.EqualTo(6));
            Assert.That(shop.TryBake(0, "b01"), Is.True);
        }

        [Test]
        public void Bake_LockedBread_Fails()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryBake(0, "b02"), Is.False);
        }

        [Test]
        public void Unlock_NeedsEmptySlot()
        {
            ShopSim shop = Create();
            m_state.AddCoins(10000d);

            Assert.That(shop.TryUnlockNextBread(new Cell(-1, 1)), Is.False);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 2)), Is.False);
            Assert.That(shop.TryUnlockNextBread(new Cell(1, 1)), Is.False);
            Assert.That(shop.Grid.TryDig(new Cell(1, 1)), Is.True);
            Assert.That(shop.TryUnlockNextBread(new Cell(1, 1)), Is.True);
            Assert.That(shop.Shelves[new Cell(1, 1)].Id, Is.EqualTo("b02"));
        }

        [Test]
        public void Unlock_FollowsTableOrderAndFillsSlots()
        {
            ShopSim shop = Create();
            int layoutChanged = 0;
            shop.LayoutChanged += () => layoutChanged++;

            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.False);

            m_state.AddCoins(10000d);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.True);
            Assert.That(shop.UnlockedBreads[1].Id, Is.EqualTo("b02"));
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 1)), Is.False);

            Assert.That(shop.TryUnlockNextBread(new Cell(0, 3)), Is.True);
            Assert.That(shop.Shelves[new Cell(0, 3)].Id, Is.EqualTo("b03"));
            Assert.That(shop.NextBread, Is.Null);
            Assert.That(shop.TryUnlockNextBread(new Cell(0, 3)), Is.False);
            Assert.That(layoutChanged, Is.EqualTo(2));
            Assert.That(shop.EmptySlots, Is.Empty);
        }

        [Test]
        public void AddOven_NeedsEmptySlotAndChargesGrowingCost()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryAddOven(new Cell(-1, 3)), Is.False);
            Assert.That(shop.TryAddOven(new Cell(0, 3)), Is.True);
            Assert.That(shop.Ovens.Count, Is.EqualTo(2));
            Assert.That(shop.Ovens[1].Cell, Is.EqualTo(new Cell(0, 3)));
            Assert.That(shop.OvenAt(new Cell(0, 3)), Is.EqualTo(1));
            Assert.That(m_state.Coins, Is.EqualTo(50d));
            Assert.That(shop.UpgradeCost(ShopSim.k_OvenCount), Is.EqualTo(900d));
            Assert.That(shop.TryAddOven(new Cell(0, 1)), Is.False);

            m_state.AddCoins(100000d);
            Assert.That(shop.TryAddOven(new Cell(0, 1)), Is.True);
            shop.Grid.TryDig(new Cell(1, 3));
            Assert.That(shop.TryAddOven(new Cell(1, 3)), Is.True);
            Assert.That(shop.IsMaxed(ShopSim.k_OvenCount), Is.True);
            shop.Grid.TryDig(new Cell(1, 1));
            Assert.That(shop.TryAddOven(new Cell(1, 1)), Is.False);
        }

        [Test]
        public void Upgrade_OvenSpeedAndShelfCapacity_Apply()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            shop.TryUpgrade(ShopSim.k_OvenSpeed);
            shop.TryUpgrade(ShopSim.k_ShelfCapacity);
            shop.TryBake(0, "b01");
            double started = RunUntil(shop, () => shop.Ovens[0].Started);
            double done = RunUntil(shop, () => shop.Stock("b01") == 6);

            // 굽기 8초 ÷ 1.2배
            Assert.That(done - started, Is.EqualTo(8d / 1.2d).Within(k_Tolerance));
            Assert.That(shop.ShelfCapacity, Is.EqualTo(12));
        }

        // 웜뱃이 A* 길로 오븐 옆에 닿아야 굽기가 시작되고, 같은 길로 돌아온다. 그동안 다른 굽기는 못 한다
        [Test]
        public void Errand_BakingStartsWhenWombatReachesOven()
        {
            ShopSim shop = Create(c => c.MaxCustomers = 0);
            m_state.AddCoins(10000d);
            Assert.That(shop.TryAddOven(new Cell(0, 3)), Is.True);
            Vector2 spot = shop.Layout.OvenSpot(new Cell(-1, 3));
            double walk = PathSeconds(shop, shop.Layout.WombatHome, spot, m_config.WalkSpeed);

            Assert.That(shop.TryBake(0, "b01"), Is.True);
            Assert.That(shop.WombatAtCounter, Is.False);
            Assert.That(shop.TryBake(1, "b01"), Is.False);

            double started = RunUntil(shop, () => shop.Ovens[0].Started);
            Assert.That(started, Is.EqualTo(walk).Within(k_Tolerance));
            Assert.That(Vector2.Distance(shop.WombatPosition, spot), Is.LessThan(0.01f));

            double back = RunUntil(shop, () => shop.WombatAtCounter);
            Assert.That(back - started, Is.EqualTo(walk).Within(k_Tolerance * 2));
            Assert.That(Vector2.Distance(shop.WombatPosition, shop.Layout.WombatHome), Is.LessThan(0.01f));
            Assert.That(shop.TryBake(1, "b01"), Is.True);
        }

        [Test]
        public void Errand_PausesCheckoutUntilWombatReturns()
        {
            ShopSim shop = Create(c => c.ArrivalSeconds = 1000d);
            Stock(shop, "b01", 6);
            m_state.AddCoins(10000d);
            Assert.That(shop.TryAddOven(new Cell(0, 3)), Is.True);
            Customer customer = null;
            shop.CustomerArrived += c => customer = c;
            RunUntil(shop, () => customer != null && customer.Phase == CustomerPhase.Queued);

            // 줄 머리에 선 손님을 두고 계산대 아래 오븐(왕복 약 2.2초)으로 심부름을 보낸다
            double coins = m_state.Coins;
            Assert.That(shop.TryBake(1, "b01"), Is.True);
            Run(shop, m_config.CheckoutSeconds + 0.1d);
            Assert.That(shop.WombatAtCounter, Is.False);
            Assert.That(m_state.Coins, Is.EqualTo(coins));

            double back = RunUntil(shop, () => shop.WombatAtCounter);
            double paid = RunUntil(shop, () => m_state.Coins > coins);
            Assert.That(paid - back, Is.EqualTo(m_config.CheckoutSeconds).Within(k_Tolerance));
        }
    }
}
