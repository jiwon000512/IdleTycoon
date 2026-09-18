using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 시작 상태, 코인
    public sealed class ZooStateTests
    {
        [Test]
        public void CreateNew_StartsWithConfiguredCoinsAndNothingElse()
        {
            ZooState state = ZooState.CreateNew(TestTables.LoadConfig());

            Assert.That(state.Coins, Is.EqualTo(350d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(0d));
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
    }
}
