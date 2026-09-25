using NUnit.Framework;
using GameKit.Events;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 시작 상태, 코인
    public sealed class ZooStateTests
    {
        [Test]
        public void CreateNew_StartsWithConfiguredCoins()
        {
            ZooState state = ZooState.CreateNew(TestTables.Load(), new EventBus());

            Assert.That(state.Coins, Is.EqualTo(350d));
        }

        [Test]
        public void AddCoins_AddsAndNotifies()
        {
            EventBus bus = new EventBus();
            ZooState state = ZooState.CreateNew(TestTables.Load(), bus);
            double notified = 0d;
            bus.Subscribe<Events.CoinsChanged>(e => notified = e.Wallet.Coins);

            state.AddCoins(50d);

            Assert.That(state.Coins, Is.EqualTo(400d));
            Assert.That(notified, Is.EqualTo(400d));
        }

        [Test]
        public void TrySpendCoins_WhenEnough_Spends()
        {
            ZooState state = ZooState.CreateNew(TestTables.Load(), new EventBus());

            bool spent = state.TrySpendCoins(100d);

            Assert.That(spent, Is.True);
            Assert.That(state.Coins, Is.EqualTo(250d));
        }

        [Test]
        public void TrySpendCoins_WhenShort_FailsAndLeavesCoins()
        {
            ZooState state = ZooState.CreateNew(TestTables.Load(), new EventBus());

            bool spent = state.TrySpendCoins(351d);

            Assert.That(spent, Is.False);
            Assert.That(state.Coins, Is.EqualTo(350d));
        }
    }
}
