using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 3장 루프 3·5, 6.2, 6.4
    public sealed class IncomeServiceTests
    {
        [Test]
        public void Tick_WithOneRabbit_AddsIncomeTimesSeconds()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.AddAnimal("a01");
            IncomeService service = new IncomeService(state, tables, new ZooLevelService(tables, state));

            service.Tick(2d);

            Assert.That(state.Coins, Is.EqualTo(352d));
            Assert.That(state.TotalCoinsEarned, Is.EqualTo(2d));
        }

        [Test]
        public void Tick_WithNoAnimal_LeavesCoins()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            IncomeService service = new IncomeService(state, tables, new ZooLevelService(tables, state));

            service.Tick(5d);

            Assert.That(state.Coins, Is.EqualTo(350d));
        }

        [Test]
        public void Tick_WhenTotalCrossesThreshold_RaisesZooLevelReached()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.AddAnimal("a08");
            ZooLevelService zooLevel = new ZooLevelService(tables, state);
            IncomeService service = new IncomeService(state, tables, zooLevel);
            int reached = 0;
            zooLevel.ZooLevelReached += level => reached = level;

            service.Tick(70d);

            Assert.That(state.TotalCoinsEarned, Is.EqualTo(2_100d));
            Assert.That(reached, Is.EqualTo(2));
        }
    }
}
