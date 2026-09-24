using System.Collections.Generic;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 8.7
    public sealed class TextsTests
    {
        [Test]
        public void Text_ReturnsRawText()
        {
            Assert.That(TestTables.Load().Text("sheet_oven_empty"), Is.EqualTo("비어 있음"));
        }

        [Test]
        public void Format_SubstitutesArguments()
        {
            Assert.That(TestTables.Load().Format("coin_popup", "10"), Is.EqualTo("+10"));
        }

        [Test]
        public void Text_WhenKeyMissing_Throws()
        {
            TableSet tables = TestTables.Load();

            Assert.Throws<KeyNotFoundException>(() => tables.Text("nosuchkey"));
        }
    }
}
