using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;
using ZooTycoon.Data;

namespace ZooTycoon.Tests
{
    // 데이터-테이블-규칙 7장
    public sealed class TableValidatorTests
    {
        [TestCase("VisitorTable", 7)]
        [TestCase("StringTable", 13)]
        [TestCase("BreadTable", 2)]
        [TestCase("ShopUpgradeTable", 5)]
        [TestCase("ActionTable", 3)]
        [TestCase("InteractableTable", 3)]
        [TestCase("DecorationTable", 3)]
        [TestCase("SoundTable", 2)]
        [TestCase("ConfigTable", 1)]
        [TestCase("BakeryConfigTable", 1)]
        [TestCase("PlazaConfigTable", 1)]
        [TestCase("PlazaDecorTable", 1)]
        public void Envelope_MatchesFileNameAndVersion(string table, int version)
        {
            TableFile<object> file = TestTables.LoadFile(table);

            Assert.That(file.Table, Is.EqualTo(table));
            Assert.That(file.Version, Is.EqualTo(version));
        }

        [Test]
        public void Validate_WithShippedTables_ReportsNoError()
        {
            IReadOnlyList<string> errors = TableValidator.Validate(TestTables.Load());

            Assert.That(errors, Is.Empty);
        }

        // 문자열 enum 칸(mode·target·carryAt·face)에 모르는 값이 있으면 읽을 때 실패한다
        [Test]
        public void Load_WhenEnumValueUnknown_Throws()
        {
            TableSet tables = TestTables.Load("ActionTable", rows => rows[0]["mode"] = "sometimes");

            Assert.Throws<JsonSerializationException>(() => tables.GetAll<ActionTable>());
        }

        [Test]
        public void Load_WhenIdDuplicated_Throws()
        {
            TableSet tables = TestTables.Load("BreadTable", rows => rows.Add(rows[0].DeepClone()));

            Assert.Throws<System.InvalidOperationException>(() => tables.GetAll<BreadTable>());
        }

        [Test]
        public void Validate_WhenLookSecondsNotPositive_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).LookSeconds = 0d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenStartCoinsNegative_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<ConfigTable>(ConfigTable.k_StartCoins).Value = -1d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [TestCase("ConfigTable", ConfigTable.k_WalkSpeed)]
        [TestCase("BakeryConfigTable", BakeryConfigTable.k_Bakery)]
        [TestCase("PlazaConfigTable", PlazaConfigTable.k_Main)]
        public void Validate_WhenConfigRowMissing_ReportsError(string table, string id)
        {
            Assert.That(TableValidator.Validate(TestTables.LoadWithout(table, id)), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorViewSecondsMinExceedsMax_ReportsError()
        {
            TableSet tables = TestTables.Load();
            VisitorTable visitor = tables.GetAll<VisitorTable>()[0];
            visitor.ViewSecondsMin = visitor.ViewSecondsMax + 1d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenShopUpgradeLookLevelExceedsMax_ReportsError()
        {
            TableSet tables = TestTables.Load();
            ShopUpgradeTable upgrade = tables.GetAll<ShopUpgradeTable>()[0];
            upgrade.LookLevel = upgrade.MaxLevel + 1;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorsEmpty_ReportsError()
        {
            Assert.That(TableValidator.Validate(TestTables.Load("VisitorTable", rows => rows.Clear())), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenStringTextEmpty_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.GetAll<StringTable>()[0].Ko = string.Empty;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 09 v0.4: 행동 표 — manual인데 아이콘 없음, 시트·곳 옮기기 행동을 auto(설계 11)
        [TestCase("take_out", ActionMode.Manual, null)]
        [TestCase("open", ActionMode.Auto, "Sprites/Actions/open")]
        [TestCase("exit", ActionMode.Auto, "Sprites/Actions/exit")]
        [TestCase("enter", ActionMode.Auto, "Sprites/Actions/enter")]
        public void Validate_WhenActionRowInvalid_ReportsError(string id, ActionMode mode, string icon)
        {
            TableSet tables = TestTables.Load();
            ActionTable row = tables.Get<ActionTable>(id);
            row.Mode = mode;
            row.Icon = icon;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 10: 효과음 표 — 음량 범위 밖, 피치 상한 1 미만
        [TestCase("pay", 1.5, 1.3)]
        [TestCase("pay", 0.6, 0.9)]
        public void Validate_WhenSoundRowInvalid_ReportsError(string id, double volume, double pitchMax)
        {
            TableSet tables = TestTables.Load();
            SoundTable row = tables.Get<SoundTable>(id);
            row.Volume = volume;
            row.PitchMax = pitchMax;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 코드가 Id로 부르는 행이 빠짐
        [TestCase("SoundTable", "give_up")]
        [TestCase("ActionTable", "serve")]
        [TestCase("InteractableTable", "dig")]
        public void Validate_WhenRequiredRowMissing_ReportsError(string table, string id)
        {
            Assert.That(TableValidator.Validate(TestTables.LoadWithout(table, id)), Is.Not.Empty);
        }

        // 설계 11: 장식 그림 경로 없음, 광장에 없는 장식
        [Test]
        public void Validate_WhenDecorationSpriteEmpty_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.GetAll<DecorationTable>()[1].Sprite = string.Empty;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenPlacedDecorUnknown_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<PlazaDecorTable>(1).Decoration = "statue";

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 09 v0.4: 사물 표 — 없는 행동 참조, range 0
        [Test]
        public void Validate_WhenInteractableInvalid_ReportsError()
        {
            TableSet unknownAction = TestTables.Load();
            unknownAction.GetAll<InteractableTable>()[0].Actions.Add("juggle");
            TableSet zeroRange = TestTables.Load();
            zeroRange.GetAll<InteractableTable>()[1].Range = 0d;

            Assert.That(TableValidator.Validate(unknownAction), Is.Not.Empty);
            Assert.That(TableValidator.Validate(zeroRange), Is.Not.Empty);
        }
    }
}
