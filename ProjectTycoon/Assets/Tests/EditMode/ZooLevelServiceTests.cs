using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4 동물원 레벨 표
    public sealed class ZooLevelServiceTests
    {
        [TestCase(0d, 1, 3)]
        [TestCase(1_999d, 1, 3)]
        [TestCase(2_000d, 2, 4)]
        [TestCase(8_000d, 3, 5)]
        [TestCase(25_000d, 4, 6)]
        [TestCase(70_000d, 5, 7)]
        [TestCase(200_000d, 6, 8)]
        [TestCase(500_000d, 7, 9)]
        public void Level_ByTotalCoinsEarned_MatchesTable(double totalEarned, int expectedLevel, int expectedCages)
        {
            ZooLevelService service = ServiceWith(totalEarned);

            Assert.That(service.Level, Is.EqualTo(expectedLevel));
            Assert.That(service.CageCount, Is.EqualTo(expectedCages));
        }

        [TestCase(0d, false)]
        [TestCase(2_000d, false)]
        [TestCase(8_000d, true)]
        [TestCase(500_000d, true)]
        public void IsPromotionUnlocked_FromLevel3_IsTrue(double totalEarned, bool expected)
        {
            ZooLevelService service = ServiceWith(totalEarned);

            Assert.That(service.IsPromotionUnlocked, Is.EqualTo(expected));
        }

        [Test]
        public void NextThreshold_AtFirstLevel_IsSecondLevelRequirement()
        {
            ZooLevelService service = ServiceWith(0d);

            Assert.That(service.NextThreshold, Is.EqualTo(2_000d));
        }

        [Test]
        public void NextThreshold_AtLastLevel_IsNull()
        {
            ZooLevelService service = ServiceWith(500_000d);

            Assert.That(service.NextThreshold, Is.Null);
        }

        [Test]
        public void Refresh_WhenThresholdPassed_RaisesZooLevelReached()
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            ZooLevelService service = new ZooLevelService(tables, state);
            int reached = 0;
            service.ZooLevelReached += level => reached = level;

            state.AddCoins(8_000d);
            service.Refresh();

            Assert.That(service.Level, Is.EqualTo(3));
            Assert.That(reached, Is.EqualTo(3));
        }

        private static ZooLevelService ServiceWith(double totalCoinsEarned)
        {
            GameTables tables = TestTables.Build();
            ZooState state = ZooState.CreateNew(tables.Config);
            state.AddCoins(totalCoinsEarned);

            return new ZooLevelService(tables, state);
        }
    }
}
