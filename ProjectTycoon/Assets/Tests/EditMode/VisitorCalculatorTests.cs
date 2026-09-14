using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 기획서 6.4(v0.14): 관광객 수 = f(초당 수입). maxCount 12, incomeUnit 1 기준
    public sealed class VisitorCalculatorTests
    {
        [TestCase(0d, 0)]
        [TestCase(0.5d, 1)]
        [TestCase(1d, 2)]
        [TestCase(3d, 3)]
        [TestCase(7d, 4)]
        [TestCase(1000000000d, 12)]
        public void Count_GrowsWithLog2OfIncome_AndCaps(double income, int expected)
        {
            GameConfig config = TestTables.LoadConfig();

            Assert.That(VisitorCalculator.Count(income, config.Visitors), Is.EqualTo(expected));
        }
    }
}
