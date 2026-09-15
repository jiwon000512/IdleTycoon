using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 시설: 홍보 입간판 f01 = Lv.3 해금, 10단계, 500 × 1.8^k
    public sealed class FacilityServiceTests
    {
        [Test]
        public void Cost_FollowsGrowthCurve()
        {
            Fixture f = Fixture.AtLevel(3);

            Assert.That(f.Service.Cost(f.Sign), Is.EqualTo(500d));

            f.State.UpgradeFacility("f01");

            Assert.That(f.Service.Cost(f.Sign), Is.EqualTo(900d).Within(1e-9d));
        }

        [Test]
        public void CanUpgrade_WhenLocked_IsFalse()
        {
            Fixture f = Fixture.AtLevel(1);
            f.State.AddCoins(1_000d);

            Assert.That(f.Service.IsUnlocked(f.Sign), Is.False);
            Assert.That(f.Service.CanUpgrade(f.Sign), Is.False);
            Assert.That(f.Service.TryUpgrade(f.Sign), Is.False);
        }

        [Test]
        public void CanUpgrade_WhenMaxed_IsFalse()
        {
            Fixture f = Fixture.AtLevel(3);
            f.State.AddCoins(1e12d);

            for (int i = 0; i < f.Sign.MaxStage; i++)
            {
                f.State.UpgradeFacility("f01");
            }

            Assert.That(f.Service.IsMaxed(f.Sign), Is.True);
            Assert.That(f.Service.CanUpgrade(f.Sign), Is.False);
            Assert.That(f.Service.TryUpgrade(f.Sign), Is.False);
        }

        [Test]
        public void TryUpgrade_WhenShort_FailsAndLeavesCoins()
        {
            Fixture f = Fixture.AtLevel(3);
            double coins = f.State.Coins;

            Assert.That(f.Service.CanUpgrade(f.Sign), Is.False);
            Assert.That(f.Service.TryUpgrade(f.Sign), Is.False);
            Assert.That(f.State.Coins, Is.EqualTo(coins));
        }

        [Test]
        public void TryUpgrade_WhenAffordable_SpendsAndRaisesStageOnce()
        {
            Fixture f = Fixture.AtLevel(3);
            f.State.AddCoins(1_000d);
            double coins = f.State.Coins;
            int raised = 0;
            f.State.FacilitiesChanged += () => raised++;

            Assert.That(f.Service.TryUpgrade(f.Sign), Is.True);
            Assert.That(f.State.Coins, Is.EqualTo(coins - 500d));
            Assert.That(f.State.GetFacilityStage("f01"), Is.EqualTo(1));
            Assert.That(raised, Is.EqualTo(1));
        }

        private sealed class Fixture
        {
            public GameTables Tables;
            public ZooState State;
            public FacilityService Service;
            public FacilityRecord Sign;

            // 레벨은 누적 획득 코인으로 정해지므로 기준 코인만큼 벌고 다시 0으로 쓴다
            public static Fixture AtLevel(int level)
            {
                Fixture f = new Fixture();
                f.Tables = TestTables.Build();
                f.State = ZooState.CreateNew(f.Tables.Config);
                double required = f.Tables.ZooLevels[level - 1].RequiredTotalCoins;
                f.State.AddCoins(required);
                f.State.TrySpendCoins(required);
                f.Service = new FacilityService(f.State, new ZooLevelService(f.Tables, f.State));
                f.Sign = f.Tables.Facilities[0];
                return f;
            }
        }
    }
}
