using System;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 08 v0.5 검증 3~9, v0.6 9-1. 1초 틱, 실제 JSON 값(도착 4 · 걷기 1.5+층×0.8 · 줄까지 1 · 인내 6 · 계산 1.5 · 식빵 8초 6개 10코인).
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

        [Test]
        public void Arrival_WalkTimeGrowsWithShelfRow()
        {
            ShopSim shop = Create(null, 0.99d);
            m_state.AddCoins(10000d);
            shop.TryUnlockNextBread();
            shop.TryUnlockNextBread();
            Customer customer = null;
            shop.CustomerArrived += c => customer = c;

            shop.Tick(1d);

            Assert.That(customer.Bread.Id, Is.EqualTo("b03"));
            Assert.That(customer.Slot, Is.EqualTo(2));
            Assert.That(customer.Timer, Is.EqualTo(2.3d).Within(1e-9));
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
        public void Unlock_FollowsTableOrderAndAddsRowOnThirdBread()
        {
            ShopSim shop = Create();
            int layoutChanged = 0;
            shop.LayoutChanged += () => layoutChanged++;

            Assert.That(shop.TryUnlockNextBread(), Is.False);

            m_state.AddCoins(10000d);
            Assert.That(shop.TryUnlockNextBread(), Is.True);
            Assert.That(shop.UnlockedBreads[1].Id, Is.EqualTo("b02"));
            Assert.That(shop.ShelfRows, Is.EqualTo(1));

            Assert.That(shop.TryUnlockNextBread(), Is.True);
            Assert.That(shop.ShelfRows, Is.EqualTo(2));
            Assert.That(shop.NextBread, Is.Null);
            Assert.That(shop.TryUnlockNextBread(), Is.False);
            Assert.That(layoutChanged, Is.EqualTo(2));
        }

        [Test]
        public void Upgrade_OvenCount_AddsOvenAndChargesGrowingCost()
        {
            ShopSim shop = Create();

            Assert.That(shop.TryUpgrade(ShopSim.k_OvenCount), Is.True);
            Assert.That(shop.Ovens.Count, Is.EqualTo(2));
            Assert.That(m_state.Coins, Is.EqualTo(50d));
            Assert.That(shop.UpgradeCost(ShopSim.k_OvenCount), Is.EqualTo(900d));
            Assert.That(shop.TryUpgrade(ShopSim.k_OvenCount), Is.False);

            m_state.AddCoins(100000d);
            shop.TryUpgrade(ShopSim.k_OvenCount);
            shop.TryUpgrade(ShopSim.k_OvenCount);
            Assert.That(shop.OvenRows, Is.EqualTo(2));
            Assert.That(shop.IsMaxed(ShopSim.k_OvenCount), Is.True);
            Assert.That(shop.TryUpgrade(ShopSim.k_OvenCount), Is.False);
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
            shop.TryUpgrade(ShopSim.k_OvenCount);

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
            shop.TryUpgrade(ShopSim.k_OvenCount);
            shop.TryBake(1, "b01");
            double coins = m_state.Coins;

            Ticks(shop, 5);
            Assert.That(m_state.Coins, Is.EqualTo(coins));

            shop.Tick(1d);
            Assert.That(shop.WombatAtCounter, Is.True);
            Assert.That(m_state.Coins, Is.EqualTo(coins + 10d));
        }

        [Test]
        public void Errand_WalkTimeGrowsWithOvenRow()
        {
            ShopSim shop = Create(c =>
            {
                c.MaxCustomers = 0;
                c.WombatWalkSeconds = 1d;
            });
            m_state.AddCoins(100000d);
            shop.TryUpgrade(ShopSim.k_OvenCount);
            shop.TryUpgrade(ShopSim.k_OvenCount);
            double seconds = 0d;
            shop.WombatLeft += (oven, s) => seconds = s;

            shop.TryBake(2, "b01");

            Assert.That(seconds, Is.EqualTo(1.8d).Within(1e-9));
        }
    }
}
