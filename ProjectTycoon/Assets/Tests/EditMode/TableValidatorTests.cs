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
        [TestCase("VisitorTable", 12)]
        [TestCase("StringTable", 30)]
        [TestCase("ClerkTable", 1)]
        [TestCase("ClerkConfigTable", 6)]
        [TestCase("BubbleTable", 2)]
        [TestCase("DialogueTable", 3)]
        [TestCase("BreadTable", 3)]
        [TestCase("ActionTable", 12)]
        [TestCase("InteractableTable", 12)]
        [TestCase("DecorationTable", 4)]
        [TestCase("SoundTable", 10)]
        [TestCase("BgmTable", 1)]
        [TestCase("ConfigTable", 3)]
        [TestCase("BakeryConfigTable", 7)]
        [TestCase("PlazaConfigTable", 2)]
        [TestCase("PlazaDecorTable", 1)]
        [TestCase("ItemTable", 3)]
        [TestCase("CropTable", 3)]
        [TestCase("FarmConfigTable", 4)]
        [TestCase("StatueTable", 1)]
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

        // 설계 24: 똥 확률은 0~1
        [TestCase(1.5)]
        [TestCase(-0.1)]
        public void Validate_WhenPoopChanceOutOfRange_ReportsError(double chance)
        {
            TableSet tables = TestTables.Load();
            tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).PoopChance = chance;

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
        [TestCase("FarmConfigTable", FarmConfigTable.k_Main)]
        public void Validate_WhenConfigRowMissing_ReportsError(string table, string id)
        {
            Assert.That(TableValidator.Validate(TestTables.LoadWithout(table, id)), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenVisitorScaleZero_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.GetAll<VisitorTable>()[0].Scale = 0d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 리뷰 R2: 드는 빵 수는 ConfigTable, 정수
        [Test]
        public void Validate_WhenCarryCapacityFraction_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<ConfigTable>(ConfigTable.k_CarryCapacity).Value = 2.5d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 13 v0.6: upgrade 행동이 있는 사물은 업그레이드 데이터가 있어야 한다
        [Test]
        public void Validate_WhenUpgradeActionWithoutData_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<InteractableTable>("oven").Upgrade = null;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenUpgradeLookLevelExceedsMax_ReportsError()
        {
            TableSet tables = TestTables.Load();
            UpgradeInfo upgrade = tables.Get<InteractableTable>("oven").Upgrade;
            upgrade.LookLevel = upgrade.MaxLevel + 1;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 18: 살 수 있는 사물은 바닥 사각형과 자리가 있어야 한다
        [Test]
        public void Validate_WhenPlacedKindHasNoSpots_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<InteractableTable>("shelf").Spots.Clear();

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

        // 설계 09 v0.4: 행동 표 — manual인데 아이콘 없음, 시트 여는 행동을 auto
        [TestCase("take_out", ActionMode.Manual, null)]
        [TestCase("open", ActionMode.Auto, "Sprites/Actions/open")]
        [TestCase("exit", ActionMode.Manual, null)]
        [TestCase("bake", ActionMode.Manual, "Sprites/Actions/open")]
        [TestCase("take_out", ActionMode.Sheet, null)]
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

        // 설계 23: 배경음악 표 — 음량 범위 밖
        [TestCase(1.5)]
        [TestCase(-0.1)]
        public void Validate_WhenBgmVolumeOutOfRange_ReportsError(double volume)
        {
            TableSet tables = TestTables.Load();
            tables.Get<BgmTable>(BgmTable.k_Bakery).Volume = volume;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 코드가 Id로 부르는 행이 빠짐
        [TestCase("SoundTable", "pay")]
        [TestCase("BgmTable", "bakery")]
        [TestCase("ActionTable", "serve")]
        [TestCase("InteractableTable", "dig")]
        [TestCase("InteractableTable", "poop")]
        [TestCase("BubbleTable", "yuck")]
        [TestCase("InteractableTable", "plot")]
        [TestCase("ActionTable", "harvest")]
        [TestCase("ActionTable", "till")]
        [TestCase("SoundTable", "plant")]
        public void Validate_WhenRequiredRowMissing_ReportsError(string table, string id)
        {
            Assert.That(TableValidator.Validate(TestTables.LoadWithout(table, id)), Is.Not.Empty);
        }

        // 설계 25 · 27: 레시피가 없는 재료를 가리킴, 작물 그림 단계 1, 작물 없음, 시작 줄이 층보다 많음, 갈기 값 0
        [Test]
        public void Validate_WhenFarmDataInvalid_ReportsError()
        {
            TableSet unknownItem = TestTables.Load();
            unknownItem.GetAll<BreadTable>()[0].Ingredients[0].Item = "milk";
            TableSet oneStage = TestTables.Load();
            oneStage.GetAll<CropTable>()[0].Stages = 1;
            TableSet tallStart = TestTables.Load();
            FarmConfigTable farm = tallStart.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            farm.StartRows = farm.FloorRows + 1;
            TableSet freeTill = TestTables.Load();
            freeTill.Get<FarmConfigTable>(FarmConfigTable.k_Main).TillCost = 0d;
            TableSet wideInset = TestTables.Load();
            wideInset.Get<FarmConfigTable>(FarmConfigTable.k_Main).FieldInset = 1d;
            // 설계 28: 거름 재료가 표에 없음, 덤 확률 1 초과, 거름 배율 0, 치운 똥 재료 없음, 획득처 글 없음
            TableSet noManure = TestTables.Load();
            noManure.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureItem = "milk";
            TableSet sureBonus = TestTables.Load();
            sureBonus.Get<FarmConfigTable>(FarmConfigTable.k_Main).BonusChance = 1.5d;
            TableSet frozenManure = TestTables.Load();
            frozenManure.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureGrowScale = 0d;
            TableSet noPoopItem = TestTables.Load();
            noPoopItem.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).PoopItem = "milk";
            TableSet noSourceText = TestTables.Load();
            noSourceText.GetAll<ItemTable>()[0].Source = "inventory_nowhere";

            Assert.That(TableValidator.Validate(noManure), Is.Not.Empty);
            Assert.That(TableValidator.Validate(sureBonus), Is.Not.Empty);
            Assert.That(TableValidator.Validate(frozenManure), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noPoopItem), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noSourceText), Is.Not.Empty);
            Assert.That(TableValidator.Validate(wideInset), Is.Not.Empty);
            Assert.That(TableValidator.Validate(unknownItem), Is.Not.Empty);
            Assert.That(TableValidator.Validate(oneStage), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.Load("CropTable", rows => rows.Clear())), Is.Not.Empty);
            Assert.That(TableValidator.Validate(tallStart), Is.Not.Empty);
            Assert.That(TableValidator.Validate(freeTill), Is.Not.Empty);
        }

        // 설계 29: 능력 값이 등급 순서가 아님, 코드가 거는 능력이 표에 없음, 잠금이 세 줄, 바치는 재료가 없음, 등급 비중이 둘
        [Test]
        public void Validate_WhenStatueDataInvalid_ReportsError()
        {
            TableSet falling = TestTables.Load();
            falling.Get<StatueTable>(StatueTable.k_Bake).Values[2] = 0.01d;
            TableSet lockAll = TestTables.Load();
            lockAll.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).StatueMaxLocks = Statue.k_Lines;
            TableSet noItem = TestTables.Load();
            noItem.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).StatueItem = "milk";
            TableSet twoGrades = TestTables.Load();
            twoGrades.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).StatueGradeWeights.RemoveAt(2);

            Assert.That(TableValidator.Validate(falling), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.LoadWithout("StatueTable", StatueTable.k_Grow)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(lockAll), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noItem), Is.Not.Empty);
            Assert.That(TableValidator.Validate(twoGrades), Is.Not.Empty);
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
            tables.GetAll<PlazaDecorTable>()[0].Decoration = "statue";

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
