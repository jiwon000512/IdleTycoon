using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 3장 핵심 루프 1~2, 6.3, 6.4(시작 코인 350)
    public sealed class GachaServiceTests
    {
        private const double k_RollRabbit = 0.1d;
        private const double k_RollGoat = 0.2d;
        private const double k_RollTurtle = 0.4d;

        [Test]
        public void TryPull_WhenShortOfCoins_FailsWithoutChangingState()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.TrySpendCoins(state.Coins - 99d);
            GachaService service = NewService(tables, state, k_RollRabbit);

            bool pulled = service.TryPull(out PullResult result);

            Assert.That(pulled, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(state.Coins, Is.EqualTo(99d));
            Assert.That(state.PullCount, Is.EqualTo(0));
            Assert.That(state.OwnedAnimals, Is.Empty);
        }

        [Test]
        public void TryPull_FirstTime_SpendsBaseCostAndAddsAnimal()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit);

            bool pulled = service.TryPull(out PullResult result);

            Assert.That(pulled, Is.True);
            Assert.That(result.Animal.Id, Is.EqualTo("a01"));
            Assert.That(result.Outcome, Is.EqualTo(PullOutcome.Placed));
            Assert.That(result.Level, Is.EqualTo(1));
            Assert.That(result.Cost, Is.EqualTo(100d));
            Assert.That(state.Coins, Is.EqualTo(250d));
            Assert.That(state.PullCount, Is.EqualTo(1));
        }

        [Test]
        public void CurrentCost_AfterOnePull_Is112()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit);

            service.TryPull(out _);

            Assert.That(service.CurrentCost, Is.EqualTo(112d));
        }

        [Test]
        public void TryPull_SameAnimalTwice_AddsOneHead()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit, k_RollRabbit);

            service.TryPull(out _);
            service.TryPull(out PullResult second);

            Assert.That(second.Outcome, Is.EqualTo(PullOutcome.LevelUp));
            Assert.That(second.Level, Is.EqualTo(2));
            Assert.That(state.OwnedAnimals.Count, Is.EqualTo(1));
            Assert.That(state.OwnedAnimals[0].Level, Is.EqualTo(2));
        }

        [Test]
        public void TryPull_RaisesAnimalPulledWithSameResult()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit);
            PullResult notified = null;
            service.AnimalPulled += r => notified = r;

            service.TryPull(out PullResult result);

            Assert.That(notified, Is.SameAs(result));
        }

        [Test]
        public void CanPull_AfterThreeStartingPulls_IsFalse()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit, k_RollGoat, k_RollTurtle);

            service.TryPull(out _);
            service.TryPull(out _);
            service.TryPull(out _);

            Assert.That(state.Coins, Is.EqualTo(13d));
            Assert.That(service.CanPull, Is.False);
        }

        [Test]
        public void SecondsUntilAffordable_AfterThreeStartingPulls_IsShortfallOverIncome()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit, k_RollGoat, k_RollTurtle);

            service.TryPull(out _);
            service.TryPull(out _);
            service.TryPull(out _);

            // 코인 13, 비용 140, 수입 3/초
            Assert.That(service.SecondsUntilAffordable, Is.EqualTo(127d / 3d).Within(1e-9d));
        }

        [Test]
        public void SecondsUntilAffordable_WhenAffordable_IsZero()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            GachaService service = NewService(tables, state, k_RollRabbit);

            Assert.That(service.SecondsUntilAffordable, Is.EqualTo(0d));
        }

        [Test]
        public void SecondsUntilAffordable_WithNoIncome_IsInfinity()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.TrySpendCoins(state.Coins - 99d);
            GachaService service = NewService(tables, state, k_RollRabbit);

            Assert.That(double.IsPositiveInfinity(service.SecondsUntilAffordable), Is.True);
        }

        private static GachaService NewService(GameTables tables, ZooState state, params double[] rolls)
        {
            return new GachaService(tables, state, new SequenceRandom(rolls));
        }
    }
}
