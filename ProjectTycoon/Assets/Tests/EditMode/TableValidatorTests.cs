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
        [TestCase("visitors", 5)]
        [TestCase("strings", 9)]
        [TestCase("breads", 1)]
        [TestCase("shop_upgrades", 4)]
        [TestCase("actions", 1)]
        [TestCase("interactables", 1)]
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
            Assert.That(file.Version, Is.EqualTo(16));
        }

        [Test]
        public void Validate_WithShippedTables_ReportsNoError()
        {
            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build());

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void Validate_WhenLookSecondsNotPositive_ReportsError()
        {
            GameConfig config = TestTables.LoadConfig();
            config.Shop.LookSeconds = 0d;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(config: config));

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
        public void Validate_WhenShopUpgradeTargetUnknown_ReportsError()
        {
            List<ShopUpgradeRecord> upgrades = TestTables.LoadRows<ShopUpgradeRecord>("shop_upgrades");
            upgrades[0].Target = "wall";

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(shopUpgrades: upgrades));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenShopUpgradeLookLevelExceedsMax_ReportsError()
        {
            List<ShopUpgradeRecord> upgrades = TestTables.LoadRows<ShopUpgradeRecord>("shop_upgrades");
            upgrades[0].LookLevel = upgrades[0].MaxLevel + 1;

            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(shopUpgrades: upgrades));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorsEmpty_ReportsError()
        {
            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Build(visitors: new List<VisitorRecord>()));

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

        // 설계 09 v0.4: 행동 표 — manual인데 아이콘 없음, 시트 행동을 auto, 코드가 아는 행동 빠짐
        [TestCase("take_out", "manual", null)]
        [TestCase("open", "auto", "Sprites/Actions/open")]
        [TestCase("fill", "sometimes", null)]
        public void Validate_WhenActionRowInvalid_ReportsError(string id, string mode, string icon)
        {
            List<ActionRecord> actions = TestTables.LoadRows<ActionRecord>("actions");
            ActionRecord row = actions.Find(a => a.Id == id);
            row.Mode = mode;
            row.Icon = icon;

            Assert.That(TableValidator.Validate(TestTables.Build(actions: actions)), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenActionMissing_ReportsError()
        {
            List<ActionRecord> actions = TestTables.LoadRows<ActionRecord>("actions");
            actions.RemoveAll(a => a.Id == "serve");

            Assert.That(TableValidator.Validate(TestTables.Build(actions: actions)), Is.Not.Empty);
        }

        // 설계 09 v0.4: 사물 표 — 없는 행동 참조, range 0, 사물 종류 빠짐
        [Test]
        public void Validate_WhenInteractableInvalid_ReportsError()
        {
            List<InteractableRecord> unknownAction = TestTables.LoadRows<InteractableRecord>("interactables");
            unknownAction[0].Actions.Add("juggle");
            List<InteractableRecord> zeroRange = TestTables.LoadRows<InteractableRecord>("interactables");
            zeroRange[1].Range = 0d;
            List<InteractableRecord> missing = TestTables.LoadRows<InteractableRecord>("interactables");
            missing.RemoveAll(e => e.Id == "dig");

            Assert.That(TableValidator.Validate(TestTables.Build(interactables: unknownAction)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.Build(interactables: zeroRange)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.Build(interactables: missing)), Is.Not.Empty);
        }
    }
}
