using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.3 비용 표
    public sealed class GachaCostCalculatorTests
    {
        [TestCase(0, 100d)]
        [TestCase(5, 176d)]
        [TestCase(10, 311d)]
        [TestCase(20, 965d)]
        public void Cost_ByPullCount_MatchesTable(int pullCount, double expected)
        {
            double cost = GachaCostCalculator.Cost(TestTables.LoadConfig().Gacha, pullCount);

            Assert.That(cost, Is.EqualTo(expected));
        }
    }
}
