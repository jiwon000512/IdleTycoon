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
        [TestCase("animals")]
        [TestCase("grades")]
        [TestCase("zoo_levels")]
        [TestCase("strings")]
        public void Envelope_OfRowTable_MatchesFileNameAndVersion(string table)
        {
            TableFile<object> file = TestTables.LoadFile<object>(table);

            Assert.That(file.Table, Is.EqualTo(table));
            Assert.That(file.Version, Is.EqualTo(1));
        }

        [Test]
        public void Envelope_OfGameConfig_MatchesFileNameAndVersion()
        {
            TableFile<object> file = TestTables.LoadFile<object>("game_config");

            Assert.That(file.Table, Is.EqualTo("game_config"));
            Assert.That(file.Version, Is.EqualTo(3));
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
        public void Validate_WhenAnimalGradeMissing_ReportsError()
        {
            List<AnimalRecord> animals = TestTables.LoadRows<AnimalRecord>("animals");
            animals[0].Grade = "nosuchgrade";

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
        public void Validate_WhenNoLevelUnlocksPromotion_ReportsError()
        {
            List<ZooLevelRecord> levels = TestTables.LoadRows<ZooLevelRecord>("zoo_levels");
            foreach (ZooLevelRecord level in levels)
            {
                level.UnlocksPromotion = false;
            }

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(zooLevels: levels));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenStartCoinsBelowFirstPullCost_ReportsError()
        {
            GameConfig config = TestTables.LoadConfig();
            config.Start.Coins = config.Gacha.BaseCost - 1;

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
