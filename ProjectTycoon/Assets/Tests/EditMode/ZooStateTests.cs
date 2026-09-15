using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 시작 상태, 3장 등장, 6.2 마리 수
    public sealed class ZooStateTests
    {
        [Test]
        public void CreateNew_StartsWithConfiguredCoinsAndNothingElse()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            Assert.That(state.Coins, Is.EqualTo(350d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(0d));
            Assert.That(state.OwnedAnimals, Is.Empty);
            Assert.That(state.PullCount, Is.EqualTo(0));
            Assert.That(state.PromotionStage, Is.EqualTo(0));
        }

        [Test]
        public void AddCoins_RaisesTotalEarnedAndNotifies()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());
            double notified = 0d;
            state.CoinsChanged += () => notified = state.Coins;

            state.AddCoins(50d);

            Assert.That(state.Coins, Is.EqualTo(400d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(50d));
            Assert.That(notified, Is.EqualTo(400d));
        }

        [Test]
        public void TrySpendCoins_WhenEnough_SpendsWithoutChangingTotalEarned()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            bool spent = state.TrySpendCoins(100d);

            Assert.That(spent, Is.True);
            Assert.That(state.Coins, Is.EqualTo(250d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(0d));
        }

        [Test]
        public void TrySpendCoins_WhenShort_FailsAndLeavesCoins()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            bool spent = state.TrySpendCoins(351d);

            Assert.That(spent, Is.False);
            Assert.That(state.Coins, Is.EqualTo(350d));
        }

        [Test]
        public void RecordPull_IncrementsPullCount()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            state.RecordPull();

            Assert.That(state.PullCount, Is.EqualTo(1));
        }

        [Test]
        public void AddAnimal_FirstTime_StartsAtOne()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            int count = state.AddAnimal("a01");

            Assert.That(count, Is.EqualTo(1));
            Assert.That(state.OwnedAnimals.Count, Is.EqualTo(1));
            Assert.That(state.OwnedAnimals[0].Count, Is.EqualTo(1));
            Assert.That(state.Owns("a01"), Is.True);
        }

        [Test]
        public void AddAnimal_WhenAlreadyOwned_AddsOneHead()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());
            state.AddAnimal("a01");

            int count = state.AddAnimal("a01");

            Assert.That(count, Is.EqualTo(2));
            Assert.That(state.OwnedAnimals.Count, Is.EqualTo(1));
            Assert.That(state.OwnedAnimals[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void AddAnimal_RaisesAnimalsChangedEveryTime()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());
            int raised = 0;
            state.AnimalsChanged += () => raised++;

            state.AddAnimal("a01");
            state.AddAnimal("a01");

            Assert.That(raised, Is.EqualTo(2));
        }
    }
}
