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

            Assert.That(table.Get("sheet_oven_empty"), Is.EqualTo("비어 있음"));
        }

        [Test]
        public void Format_SubstitutesArguments()
        {
            StringTable table = TestTables.Build().Strings;

            Assert.That(table.Format("coin_popup", "10"), Is.EqualTo("+10"));
        }

        [Test]
        public void Get_WhenKeyMissing_Throws()
        {
            StringTable table = TestTables.Build().Strings;

            Assert.Throws<KeyNotFoundException>(() => table.Get("nosuchkey"));
        }
    }
}
