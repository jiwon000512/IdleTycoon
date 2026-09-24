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
        [TestCase("visitors", 6)]
        [TestCase("strings", 12)]
        [TestCase("breads", 1)]
        [TestCase("shop_upgrades", 4)]
        [TestCase("actions", 2)]
        [TestCase("interactables", 2)]
        [TestCase("decorations", 2)]
        [TestCase("sounds", 1)]
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
            Assert.That(file.Version, Is.EqualTo(17));
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

        // 2026-09-23: 손님이 물건을 드는 자리는 hand·head만
        [TestCase(null)]
        [TestCase("tail")]
        public void Validate_WhenVisitorCarryAtInvalid_ReportsError(string carryAt)
        {
            List<VisitorRecord> visitors = TestTables.LoadRows<VisitorRecord>("visitors");
            visitors[0].CarryAt = carryAt;

            Assert.That(TableValidator.Validate(TestTables.Build(visitors: visitors)), Is.Not.Empty);
        }

        // 설계 10: 효과음 표 — 음량 범위 밖, 피치 상한 1 미만, 코드가 아는 소리 빠짐
        [TestCase("pay", 1.5, 1.3)]
        [TestCase("pay", 0.6, 0.9)]
        public void Validate_WhenSoundRowInvalid_ReportsError(string id, double volume, double pitchMax)
        {
            List<SoundRecord> sounds = TestTables.LoadRows<SoundRecord>("sounds");
            SoundRecord row = sounds.Find(s => s.Id == id);
            row.Volume = volume;
            row.PitchMax = pitchMax;

            Assert.That(TableValidator.Validate(TestTables.Build(sounds: sounds)), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenSoundMissing_ReportsError()
        {
            List<SoundRecord> sounds = TestTables.LoadRows<SoundRecord>("sounds");
            sounds.RemoveAll(s => s.Id == "give_up");

            Assert.That(TableValidator.Validate(TestTables.Build(sounds: sounds)), Is.Not.Empty);
        }

        // 설계 11: 곳을 옮기는 행동은 manual만, 장식 표 — 그림 경로 없음·모르는 방향, 광장에 없는 장식
        [TestCase("exit")]
        [TestCase("enter")]
        public void Validate_WhenMoveActionAuto_ReportsError(string id)
        {
            List<ActionRecord> actions = TestTables.LoadRows<ActionRecord>("actions");
            actions.Find(a => a.Id == id).Mode = ActionRecord.k_Auto;

            Assert.That(TableValidator.Validate(TestTables.Build(actions: actions)), Is.Not.Empty);
        }

        [TestCase("", "up")]
        [TestCase("Sprites/Decor/bench_log", "north")]
        public void Validate_WhenDecorationInvalid_ReportsError(string sprite, string face)
        {
            List<DecorationRecord> decorations = TestTables.LoadRows<DecorationRecord>("decorations");
            decorations[1].Sprite = sprite;
            decorations[1].Spots[0].Face = face;

            Assert.That(TableValidator.Validate(TestTables.Build(decorations: decorations)), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenPlacedDecorUnknown_ReportsError()
        {
            GameConfig config = TestTables.LoadConfig();
            config.Plaza.Decor[0].Id = "statue";

            Assert.That(TableValidator.Validate(TestTables.Build(config: config)), Is.Not.Empty);
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
