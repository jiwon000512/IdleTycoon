using NUnit.Framework;
using ZooTycoon.UI;

namespace ZooTycoon.Tests
{
    public sealed class BigNumberFormatterTests
    {
        [TestCase(0d, "0")]
        [TestCase(999d, "999")]
        [TestCase(1_000d, "1K")]
        [TestCase(1_234d, "1.2K")]
        [TestCase(1_500_000d, "1.5M")]
        [TestCase(2_000_000_000d, "2B")]
        [TestCase(3_000_000_000_000d, "3T")]
        public void Format_ByMagnitude_UsesUnitSuffix(double value, string expected)
        {
            Assert.That(BigNumberFormatter.Format(value), Is.EqualTo(expected));
        }

        [TestCase(0d, "0:00")]
        [TestCase(251.2d, "4:12")]
        [TestCase(3599d, "59:59")]
        [TestCase(16244d, "4:30:44")]
        public void Clock_OverAnHour_ShowsHours(double seconds, string expected)
        {
            Assert.That(BigNumberFormatter.Clock(seconds), Is.EqualTo(expected));
        }
    }
}
