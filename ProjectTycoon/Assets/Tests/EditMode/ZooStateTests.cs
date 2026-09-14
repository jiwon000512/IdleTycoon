using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 시작 상태
    public sealed class ZooStateTests
    {
        [Test]
        public void CreateNew_StartsWithConfiguredCoinsAndNothingElse()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            Assert.That(state.Coins, Is.EqualTo(300d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(0d));
            Assert.That(state.PlacedAnimals, Is.Empty);
            Assert.That(state.WaitingRoom, Is.Empty);
            Assert.That(state.PullCount, Is.EqualTo(0));
            Assert.That(state.PromotionStage, Is.EqualTo(0));
        }

        [Test]
        public void AddCoins_RaisesTotalEarnedAndNotifies()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());
            double notified = 0d;
            state.CoinsChanged += coins => notified = coins;

            state.AddCoins(50d);

            Assert.That(state.Coins, Is.EqualTo(350d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(50d));
            Assert.That(notified, Is.EqualTo(350d));
        }

        [Test]
        public void TrySpendCoins_WhenEnough_SpendsWithoutChangingTotalEarned()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            bool spent = state.TrySpendCoins(100d);

            Assert.That(spent, Is.True);
            Assert.That(state.Coins, Is.EqualTo(200d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(0d));
        }

        [Test]
        public void TrySpendCoins_WhenShort_FailsAndLeavesCoins()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            bool spent = state.TrySpendCoins(301d);

            Assert.That(spent, Is.False);
            Assert.That(state.Coins, Is.EqualTo(300d));
        }
    }
}
