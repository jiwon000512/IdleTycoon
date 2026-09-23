using System;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 08 v0.5 검증 3~9, v0.6 9-1, 굴 격자 설계 v0.5 검증 3. 1초 틱, 실제 JSON 값(도착 4 · 걷기 1.5 + 칸 경로 ÷ 3 · 줄까지 1 · 인내 6 · 계산 1.5 · 식빵 8초 6개 10코인).
    // 웜뱃 심부름 걷기는 기본 0초로 두고(굽기 타이밍을 v0.5와 같게) 심부름 테스트에서만 켠다
    public sealed class ShopSimTests
    {
        private ZooState m_state;
        private GameConfig.ShopConfig m_config;

        private ShopSim Create(Action<GameConfig.ShopConfig> tweak = null, params double[] rolls)
        {
            GameConfig config = TestTables.LoadConfig();
            m_config = config.Shop;
            config.Shop.WombatWalkSeconds = 0d;
            tweak?.Invoke(config.Shop);
            GameTables tables = TestTables.Build(config: config);
            m_state = ZooState.CreateNew(config);
            return new ShopSim(m_state, tables, new SequenceRandom(rolls.Length > 0 ? rolls : new double[100]));
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

        private static void Ticks(ShopSim shop, int count)
        {
            for (int i = 0; i < count; i++)
            {
                shop.Tick(1d);
            }
        }

        [Test]
        public void Arrival_FirstOnFirstTick_ThenEveryArrivalSeconds()
        {
            ShopSim shop = Create(c => c.PatienceSeconds = 1000d);
            int arrived = 0;
            shop.CustomerArrived += _ => arrived++;

            Ticks(shop, 9);

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

            Ticks(shop, 20);

            Assert.That(shop.Customers.Count, Is.EqualTo(8));
        }

        // 세 번째 빵을 왼쪽으로 판 칸(−2,1)에 놓으면 입구(−1,0) → (−1,1) → (−2,1): 2.4 + 3.375 − 2.4(마지막 세로) = 3.375 ÷ 3 = 1.125
        [Test]
        public void Arrival_WalkTimeGrowsWithDistance()
        {
            ShopSim shop = Create(null, 0.99d);
            m_state.AddCoins(10000d);
            shop.TryUnlockNextBread(new Cell(0, 1));
            shop.Grid.TryDig(new Cell(-2, 1));
            shop.TryUnlockNextBread(new Cell(-2, 1));
            Customer customer = null;
            shop.CustomerArrived += c => customer = c;

            shop.Tick(1d);

            Assert.That(customer.Bread.Id, Is.EqualTo("b03"));
            Assert.That(customer.Cell, Is.EqualTo(new Cell(-2, 1)));
            Assert.That(customer.Timer, Is.EqualTo(2.625d).Within(1e-9));
        }

        // 빈 자리가 없으면 해금 못 한다. 파고 나면 그 칸에
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
        public void Shelf_WithoutStock_GivesUpAfterPatience()
        {
            ShopSim shop = Create();
            int gaveUp = 0;
            shop.CustomerGaveUp += _ => gaveUp++;

            Ticks(shop, 8);
            Assert.That(gaveUp, Is.EqualTo(0));

            shop.Tick(1d);
            Assert.That(gaveUp, Is.EqualTo(1));
        }

        [Test]
        public void Shelf_WaitingCustomersPickWhenBakingFinishes()
        {
            ShopSim shop = Create();
            int picked = 0;
            shop.CustomerPicked += _ => picked++;
            shop.TryBake(0, "b01");

            Ticks(shop, 8);

            Assert.That(picked, Is.EqualTo(2));
            Assert.That(shop.Stock("b01"), Is.EqualTo(4));
        }

        [Test]
        public void Checkout_PaysHeadThenNextWithCarriedTime()
        {
            ShopSim shop = Create();
            shop.TryBake(0, "b01");
            Ticks(shop, 9);
            Assert.That(shop.Queue.Count, Is.EqualTo(2));

            shop.Tick(1d);
            Assert.That(m_state.Coins, Is.EqualTo(360d));
            Assert.That(shop.Queue.Count, Is.EqualTo(1));

            shop.Tick(1d);
            Assert.That(m_state.Coins, Is.EqualTo(370d));
            Assert.That(shop.Queue.Count, Is.EqualTo(0));
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

            Ticks(shop, 8);

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

            Ticks(shop, 8);

            Assert.That(shop.Stock("b01"), Is.EqualTo(6));
            Assert.That(shop.Ovens[0].IsEmpty, Is.True);
            Assert.That(shop.TryBake(0, "b01"), Is.True);
        }

        [Test]
        public void Bake_LockedBread_Fails()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryBake(0, "b02"), Is.False);
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

            Ticks(shop, 7);

            Assert.That(shop.Stock("b01"), Is.EqualTo(6));
            Assert.That(shop.ShelfCapacity, Is.EqualTo(12));
        }

        [Test]
        public void Errand_BakingStartsOnArrivalAndBlocksAnotherBake()
        {
            ShopSim shop = Create(c =>
            {
                c.MaxCustomers = 0;
                c.WombatWalkSeconds = 2d;
            });
            shop.TryAddOven(new Cell(0, 3));

            Assert.That(shop.TryBake(0, "b01"), Is.True);
            Assert.That(shop.WombatAtCounter, Is.False);
            Assert.That(shop.TryBake(1, "b01"), Is.False);

            shop.Tick(1d);
            Assert.That(shop.Ovens[0].Started, Is.False);
            Assert.That(shop.Ovens[0].Remaining, Is.EqualTo(8d));

            shop.Tick(1d);
            Assert.That(shop.Ovens[0].Started, Is.True);
            Assert.That(shop.Ovens[0].Remaining, Is.EqualTo(7d));

            shop.Tick(1d);
            Assert.That(shop.WombatAtCounter, Is.False);
            shop.Tick(1d);
            Assert.That(shop.WombatAtCounter, Is.True);
            Assert.That(shop.TryBake(1, "b01"), Is.True);
        }

        [Test]
        public void Errand_PausesCheckoutUntilWombatReturns()
        {
            ShopSim shop = Create();
            shop.TryBake(0, "b01");
            Ticks(shop, 9);
            Assert.That(shop.Queue.Count, Is.EqualTo(2));

            m_config.WombatWalkSeconds = 3d;
            shop.TryAddOven(new Cell(0, 3));
            shop.TryBake(1, "b01");
            double coins = m_state.Coins;

            Ticks(shop, 5);
            Assert.That(m_state.Coins, Is.EqualTo(coins));

            shop.Tick(1d);
            Assert.That(shop.WombatAtCounter, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + 10d));
        }

        // 오븐을 오른쪽으로 판 칸(1,3)에 두면 계산대(0,2) → (0,3) → (1,3): 2.4 + 3.375 − 2.4 = 3.375 ÷ 3 = 1.125
        [Test]
        public void Errand_WalkTimeGrowsWithDistance()
        {
            ShopSim shop = Create(c =>
            {
                c.MaxCustomers = 0;
                c.WombatWalkSeconds = 1d;
            });
            m_state.AddCoins(100000d);
            shop.Grid.TryDig(new Cell(1, 3));
            shop.TryAddOven(new Cell(1, 3));
            double seconds = 0d;
            shop.WombatLeft += (oven, s) => seconds = s;

            shop.TryBake(1, "b01");

            Assert.That(seconds, Is.EqualTo(2.125d).Within(1e-9));
        }
    }
}
