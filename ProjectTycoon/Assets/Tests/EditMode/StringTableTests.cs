using System.Collections.Generic;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 8.5
    public sealed class StringTableTests
    {
        [Test]
        public void Get_ReturnsRawText()
        {
            StringTable table = TestTables.Build().Strings;

            Assert.That(table.Get("topbar_progress_max"), Is.EqualTo("MAX"));
        }

        [Test]
        public void Format_SubstitutesArguments()
        {
            StringTable table = TestTables.Build().Strings;

            Assert.That(table.Format("topbar_progress", "0", "2,000"), Is.EqualTo("0/2,000"));
        }

        [Test]
        public void Get_WhenKeyMissing_Throws()
        {
            StringTable table = TestTables.Build().Strings;

            Assert.Throws<KeyNotFoundException>(() => table.Get("nosuchkey"));
        }
    }
}
