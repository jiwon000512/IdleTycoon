using System.Collections.Generic;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;
using ZooTycoon.Data;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장
    public sealed class TableValidatorTests
    {
        [TestCase("animals", 3)]
        [TestCase("zoo_levels", 2)]
        [TestCase("visitors", 1)]
        [TestCase("strings", 3)]
        [TestCase("breads", 1)]
        [TestCase("shop_upgrades", 1)]
        public void Envelope_OfRowTable_MatchesFileNameAndVersion(string table, int version)
        {
            TableFile<object> file = TestTables.LoadFile<object>(table);

            Assert.That(file.Table, Is.EqualTo(table));
            Assert.That(file.Version, Is.EqualTo(version));
        }

        [Test]
        public void Envelope_OfGameConfig_MatchesFileNameAndVersion()
        {
            TableFile<object> file = TestTables.LoadFile<object>("game_config");

            Assert.That(file.Table, Is.EqualTo("game_config"));
            Assert.That(file.Version, Is.EqualTo(9));
        }

        [Test]
        public void Validate_WithShippedTables_ReportsNoError()
        {
            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build());

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void Validate_WhenAnimalIdDuplicated_ReportsError()
        {
            List<AnimalRecord> animals = TestTables.LoadRows<AnimalRecord>("animals");
            animals.Add(animals[0]);

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(animals: animals));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenIdleSecondsMinExceedsMax_ReportsError()
        {
            List<AnimalRecord> animals = TestTables.LoadRows<AnimalRecord>("animals");
            animals[0].IdleSecondsMin = animals[0].IdleSecondsMax + 1d;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(animals: animals));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenZooLevelThresholdGoesBackwards_ReportsError()
        {
            List<ZooLevelRecord> levels = TestTables.LoadRows<ZooLevelRecord>("zoo_levels");
            levels[2].RequiredTotalCoins = 1d;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(zooLevels: levels));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenStartCoinsNegative_ReportsError()
        {
            GameConfig config = TestTables.LoadConfig();
            config.Start.Coins = -1;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(config: config));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorViewSecondsMinExceedsMax_ReportsError()
        {
            List<VisitorRecord> visitors = TestTables.LoadRows<VisitorRecord>("visitors");
            visitors[0].ViewSecondsMin = visitors[0].ViewSecondsMax + 1d;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(visitors: visitors));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorsEmpty_ReportsError()
        {
            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(visitors: new List<VisitorRecord>()));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorMaxCountZero_ReportsError()
        {
            GameConfig config = TestTables.LoadConfig();
            config.Visitors.MaxCount = 0;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(config: config));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenStringTextEmpty_ReportsError()
        {
            List<StringRecord> strings = TestTables.LoadRows<StringRecord>("strings");
            strings[0].Ko = string.Empty;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(strings: strings));

            Assert.That(errors, Is.Not.Empty);
        }
    }
}
