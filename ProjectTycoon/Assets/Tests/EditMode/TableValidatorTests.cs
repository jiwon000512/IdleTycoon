using System.Collections.Generic;
using System.Linq;
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
        [TestCase("VisitorTable", 15)]
        [TestCase("StringTable", 46)]
        [TestCase("ClerkTable", 3)]
        [TestCase("ClerkConfigTable", 6)]
        [TestCase("BubbleTable", 4)]
        [TestCase("DialogueTable", 5)]
        [TestCase("BreadTable", 4)]
        [TestCase("ActionTable", 21)]
        [TestCase("InteractableTable", 20)]
        [TestCase("DecorationTable", 4)]
        [TestCase("SoundTable", 16)]
        [TestCase("BgmTable", 1)]
        [TestCase("ConfigTable", 6)]
        [TestCase("BakeryConfigTable", 9)]
        [TestCase("PlazaConfigTable", 7)]
        [TestCase("PlazaDecorTable", 1)]
        [TestCase("ItemTable", 7)]
        [TestCase("CropTable", 4)]
        [TestCase("FarmConfigTable", 7)]
        [TestCase("FarmFloorTable", 2)]
        [TestCase("StarConfigTable", 1)]
        [TestCase("StarMilestoneTable", 1)]
        [TestCase("BlessingTable", 1)]
        [TestCase("RelicTable", 2)]
        [TestCase("FishingConfigTable", 3)]
        [TestCase("RodTable", 1)]
        [TestCase("FishTable", 1)]
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

        // 설계 24 · 37: 똥 확률은 0~1(ConfigTable)
        [TestCase(1.5)]
        [TestCase(-0.1)]
        public void Validate_WhenPoopChanceOutOfRange_ReportsError(double chance)
        {
            TableSet tables = TestTables.Load();
            tables.Get<ConfigTable>(ConfigTable.k_PoopChance).Value = chance;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 44: 낚시터 물길은 축 정렬 · 낚싯대 slow는 1 이하 · 물고기 재료는 ItemTable에
        [Test]
        public void Validate_WhenFishingStreamDiagonal_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<FishingConfigTable>(FishingConfigTable.k_Main).Stream[1].Y += 1d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenRodSlowAboveOne_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.Get<RodTable>(RodTable.k_Bait).Slow = 1.5d;

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        [Test]
        public void Validate_WhenFishItemMissing_ReportsError()
        {
            TableSet tables = TestTables.Load();
            tables.GetAll<FishTable>()[0].Item = "no_such_item";

            Assert.That(TableValidator.Validate(tables), Is.Not.Empty);
        }

        // 설계 44: 광장 문은 칸 안 · 겹치지 않음 · 빵집 문이 있다
        [TestCase(4, "fishing")]
        [TestCase(2, "fishing")]
        [TestCase(0, "bakery")]
        public void Validate_WhenPlazaDoorBad_ReportsError(int col, string to)
        {
            TableSet tables = TestTables.Load();
            PlazaConfigTable plaza = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            plaza.Doors = plaza.Doors.Append(new PlazaDoorData { To = to, Col = col }).ToArray();

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
            // 설계 35: 해금 값이 음수, 다 익음 표시 없음
            TableSet negativeUnlock = TestTables.Load();
            negativeUnlock.GetAll<CropTable>()[1].UnlockCost = -1d;
            TableSet noReadyMark = TestTables.Load();
            noReadyMark.GetAll<CropTable>()[0].ReadyMark = null;
            TableSet tallStart = TestTables.Load();
            FarmFloorTable farm = tallStart.Get<FarmFloorTable>(FarmArea.k_Id);
            farm.StartRows = farm.FloorRows + 1;
            TableSet freeTill = TestTables.Load();
            freeTill.Get<FarmFloorTable>(FarmArea.k_Id).TillCost = 0d;
            TableSet wideInset = TestTables.Load();
            wideInset.Get<FarmConfigTable>(FarmConfigTable.k_Main).FieldInset = 1d;
            // 설계 28: 거름 재료가 표에 없음, 덤 확률 1 초과, 거름 배율 0, 획득처 글 없음
            TableSet noManure = TestTables.Load();
            noManure.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureItem = "milk";
            TableSet sureBonus = TestTables.Load();
            sureBonus.Get<FarmConfigTable>(FarmConfigTable.k_Main).BonusChance = 1.5d;
            TableSet frozenManure = TestTables.Load();
            frozenManure.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureGrowScale = 0d;
            TableSet noSourceText = TestTables.Load();
            noSourceText.GetAll<ItemTable>()[0].Source = "inventory_nowhere";

            Assert.That(TableValidator.Validate(noManure), Is.Not.Empty);
            Assert.That(TableValidator.Validate(sureBonus), Is.Not.Empty);
            Assert.That(TableValidator.Validate(frozenManure), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noSourceText), Is.Not.Empty);
            Assert.That(TableValidator.Validate(wideInset), Is.Not.Empty);
            Assert.That(TableValidator.Validate(unknownItem), Is.Not.Empty);
            Assert.That(TableValidator.Validate(oneStage), Is.Not.Empty);
            Assert.That(TableValidator.Validate(negativeUnlock), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noReadyMark), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.Load("CropTable", rows => rows.Clear())), Is.Not.Empty);
            Assert.That(TableValidator.Validate(tallStart), Is.Not.Empty);
            Assert.That(TableValidator.Validate(freeTill), Is.Not.Empty);
        }

        // 설계 39: 첫 층이 농장이 아님 · 첫 층에 여는 값 · 아래층이 공짜 · 점원 탭 글 없음 · 흙 바뀌는 층 간격 0
        [Test]
        public void Validate_WhenFarmFloorDataInvalid_ReportsError()
        {
            TableSet paidFirst = TestTables.Load();
            paidFirst.Get<FarmFloorTable>(FarmArea.k_Id).OpenCost = 10d;
            TableSet freeLower = TestTables.Load();
            freeLower.GetAll<FarmFloorTable>()[1].OpenCost = 0d;
            TableSet noEvery = TestTables.Load();
            noEvery.Get<FarmConfigTable>(FarmConfigTable.k_Main).EarthEvery = 0;
            TableSet noTab = TestTables.Load();
            noTab.GetAll<FarmFloorTable>()[1].Id = "farm9";

            Assert.That(TableValidator.Validate(TestTables.LoadWithout("FarmFloorTable", FarmArea.k_Id)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(paidFirst), Is.Not.Empty);
            Assert.That(TableValidator.Validate(freeLower), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noEvery), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noTab), Is.Not.Empty);
        }

        // 설계 40: 가게 별 설정이 없음 · 몰림 1 미만 · 평가단장이 judge 행이 아님 · star 0 마일스톤 없음 · 마일스톤 글이 없는 키
        [Test]
        public void Validate_WhenStarDataInvalid_ReportsError()
        {
            TableSet slowRush = TestTables.Load();
            slowRush.Get<StarConfigTable>(BakeryArea.k_Id).Rush = 0.5d;
            TableSet customerJudge = TestTables.Load();
            customerJudge.Get<StarConfigTable>(BakeryArea.k_Id).JudgeLook = "v01";
            TableSet badText = TestTables.Load();
            badText.Get<StarMilestoneTable>("bakery_3").Text = "no_such_key";

            Assert.That(TableValidator.Validate(TestTables.LoadWithout("StarConfigTable", BakeryArea.k_Id)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(slowRush), Is.Not.Empty);
            Assert.That(TableValidator.Validate(customerJudge), Is.Not.Empty);
            Assert.That(TableValidator.Validate(TestTables.LoadWithout("StarMilestoneTable", "bakery_0")), Is.Not.Empty);
            Assert.That(TableValidator.Validate(badText), Is.Not.Empty);
        }

        // 설계 30: 코드가 거는 축복이 표에 없음, 시간이 0, 효과 글이 없음, 쉬는 시간이 0
        [Test]
        public void Validate_WhenBlessingDataInvalid_ReportsError()
        {
            TableSet noTime = TestTables.Load();
            noTime.Get<BlessingTable>(BlessingTable.k_Bake).Seconds = 0d;
            TableSet noText = TestTables.Load();
            noText.Get<BlessingTable>(BlessingTable.k_Bake).Format = "no_such_key";
            TableSet noRest = TestTables.Load();
            noRest.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).BlessingCooldown = 0d;

            Assert.That(TableValidator.Validate(TestTables.LoadWithout("BlessingTable", BlessingTable.k_Grow)), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noTime), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noText), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noRest), Is.Not.Empty);
        }

        // 설계 31: 코드에 없는 효과, 별마다 세지지 않는 값, 없는 뽑기 재료, 칸 0, 행상이 간격보다 오래 머묾 · 손님 외형 · 좌판 뒤 자리 없음
        [Test]
        public void Validate_WhenRelicDataInvalid_ReportsError()
        {
            TableSet noEffect = TestTables.Load();
            noEffect.Get<RelicTable>("bellows").Effect = "fly";
            TableSet flat = TestTables.Load();
            flat.Get<RelicTable>("bellows").Values = new[] { 0.2d, 0.2d, 0.6d };
            TableSet clockUp = TestTables.Load();
            clockUp.Get<RelicTable>("clock").Values = new[] { 10d, 20d, 30d };
            TableSet noItem = TestTables.Load();
            noItem.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).RelicItem = "no_such_item";
            TableSet noSlots = TestTables.Load();
            noSlots.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).RelicSlots = 0;
            TableSet longStay = TestTables.Load();
            longStay.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).MerchantStay = 900d;
            TableSet customerMerchant = TestTables.Load();
            customerMerchant.Get<PlazaConfigTable>(PlazaConfigTable.k_Main).MerchantLook = "v01";

            Assert.That(TableValidator.Validate(TestTables.Load()), Is.Empty);
            Assert.That(TableValidator.Validate(noEffect), Is.Not.Empty);
            Assert.That(TableValidator.Validate(flat), Is.Not.Empty);
            Assert.That(TableValidator.Validate(clockUp), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noItem), Is.Not.Empty);
            Assert.That(TableValidator.Validate(noSlots), Is.Not.Empty);
            Assert.That(TableValidator.Validate(longStay), Is.Not.Empty);
            Assert.That(TableValidator.Validate(customerMerchant), Is.Not.Empty);
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
