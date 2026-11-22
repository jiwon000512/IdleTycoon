using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 64 가지 굴 광장: 마당 2 × 2에서 시작해 굴길 · 주머니를 파며 문 · 석상 · 행상 · 장식이 열린다
    public sealed class PlazaDigTests
    {
        private const double k_Dt = 0.02;

        private static PlazaConfigTable Config(TestGame game)
        {
            return game.Tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
        }

        private static PlazaPocketData Pocket(TestGame game, string use, string to = null)
        {
            return Config(game).Pockets.First(p => p.Use == use && (to == null || p.To == to));
        }

        // 굴길 위에서 아래로 한 줄씩, 그 줄의 주머니까지 판다(값 · 문 조건 없이)
        private static void DigTo(TestGame game, PlazaPocketData pocket)
        {
            PlazaArea plaza = game.Mall.Plaza;
            int first = -Config(game).ShaftCols / 2;

            for (int row = 1; row <= pocket.Row; row++)
            {
                for (int col = first; col < first + Config(game).ShaftCols; col++)
                {
                    Cell cell = new Cell(col, row);

                    if (!plaza.Grid.Contains(cell))
                    {
                        plaza.Grid.Dig(cell);
                    }
                }
            }

            plaza.Grid.Dig(pocket.Cell);
        }

        // 처음: 굴길 startRows줄만 있고, 팔 수 있는 칸은 다음 굴길 줄 둘과 밭 주머니뿐. 문은 걷는 땅 밖, 석상 · 행상은 사물이 아니다
        [Test]
        public void NewPlaza_IsSmallYard_WithFarmPocketOnFrontier()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            PlazaConfigTable config = Config(game);
            PlazaPocketData farm = Pocket(game, PlazaPocketData.k_Door, FarmArea.k_Id);

            Assert.That(plaza.Grid.Cells.Count, Is.EqualTo(config.ShaftCols * config.StartRows));
            Assert.That(plaza.Grid.Frontier().ToList(), Is.EquivalentTo(new[] { new Cell(-1, 2), new Cell(0, 2), farm.Cell }));
            Assert.That(plaza.Things.OfType<DigInteractable>().Select(dig => dig.Cell), Is.EqualTo(new[] { farm.Cell }), "굴길 줄은 흙 칸이 아니라 둑(설계 65)");
            Assert.That(plaza.Dam.Row, Is.EqualTo(config.StartRows));
            Assert.That(plaza.Things, Has.Member(plaza.Dam));
            Assert.That(plaza.Layout.Nav.IsWalkable(plaza.BakeryDoor.Floor), Is.False, "빵집 문은 주머니를 파야 닿는다");
            Assert.That(plaza.Layout.Nav.IsWalkable(plaza.Layout.StairsFloor), Is.True);
            Assert.That(plaza.Things, Has.No.Member(plaza.Statue));
            Assert.That(plaza.Things, Has.No.Member(plaza.MerchantThing));
            Assert.That(plaza.StatueOpen, Is.False);
            Assert.That(plaza.Editable, Is.False, "장식 편집은 횟집 뒤");
        }

        // 밭 주머니를 파면 농장 문이 걷는 땅에 닿고 문 그림 자리(띠 밑변)는 그 줄 윗변
        [Test]
        public void DiggingPocket_ReachesItsDoor()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            PlazaPocketData farm = Pocket(game, PlazaPocketData.k_Door, FarmArea.k_Id);
            PlazaDoor door = plaza.Layout.DoorTo(FarmArea.k_Id);

            Assert.That(plaza.Layout.Nav.IsWalkable(door.Floor), Is.False);
            plaza.Grid.Dig(farm.Cell);

            Assert.That(plaza.Layout.Nav.IsWalkable(door.Floor), Is.True);
            Assert.That(door.WallBottom, Is.EqualTo(-plaza.Layout.Cells.RowTop(farm.Row)).Within(1e-4f));
            Assert.That(door.Floor.Y, Is.EqualTo(door.WallBottom - PlazaLayout.k_DoorFloorDrop).Within(1e-4f));
            Assert.That(plaza.Things.OfType<DigInteractable>().Any(dig => dig.Cell.Equals(farm.Cell)), Is.False, "판 칸은 파기 사물에서 빠진다");
        }

        // 웜뱃이 파기 칸 곁에서 시트로 값을 치르면 칸이 열린다(빵집 파기와 같은 행동)
        [Test]
        public void Wombat_DigsPlazaCell_WithDigAction()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            game.State.AddCoins(10000d);
            game.Mall.StartNew();
            PlazaPocketData farm = Pocket(game, PlazaPocketData.k_Door, FarmArea.k_Id);
            NavRect rect = plaza.Layout.Cells.CellRect(farm.Cell);
            game.Mall.Wombat.Mover.Place(new Vector2(rect.XMin - 0.5f, (rect.YMin + rect.YMax) * 0.5f));
            game.Mall.Tick(k_Dt);

            Assert.That(plaza.Target, Is.InstanceOf<DigInteractable>());
            Assert.That(((DigInteractable)plaza.Target).Cell, Is.EqualTo(farm.Cell));
            Assert.That(plaza.TryChoose(ActionTable.k_Dig, plaza.Target, null), Is.True);
            Assert.That(plaza.Grid.Contains(farm.Cell), Is.True);
        }

        // 석상 · 행상 주머니는 횟집이 열리기 전엔 못 파고, 열리면 팔 수 있다. 파면 사물이 서고 행상 시계가 돈다
        [Test]
        public void StatueAndMerchantPockets_OpenAfterRestaurant()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            PlazaPocketData statue = Pocket(game, PlazaPocketData.k_Statue);
            PlazaPocketData merchant = Pocket(game, PlazaPocketData.k_Merchant);
            DigTo(game, Pocket(game, PlazaPocketData.k_Door, RestaurantArea.k_Id));

            Assert.That(plaza.Grid.CanDig(statue.Cell), Is.False);
            Assert.That(plaza.Editable, Is.False);

            // 횟집 문 열기(별 다섯 + 코인)
            game.State.AddCoins(100000d);

            while (game.Mall.Restaurant.StarLocked)
            {
                game.Mall.Bakery.Evaluation.PassNow();
            }

            Assert.That(plaza.TryChoose(ActionTable.k_OpenShop, plaza.Gates.First(gate => gate.Shop.Id == RestaurantArea.k_Id), null), Is.True);

            Assert.That(plaza.Grid.CanDig(statue.Cell), Is.True);
            Assert.That(plaza.Editable, Is.True);

            // 연 뒤 첫 틱에 주머니가 파기 사물로 선다(리뷰 B2). 값을 치르고 파는 행동 그대로
            game.Mall.Tick(k_Dt);
            DigInteractable statueDig = plaza.Things.OfType<DigInteractable>().FirstOrDefault(dig => dig.Cell.Equals(statue.Cell));
            Assert.That(statueDig, Is.Not.Null, "횟집을 열면 석상 주머니가 팔 수 있는 칸이 된다");
            Assert.That(plaza.TryChoose(ActionTable.k_Dig, statueDig, null), Is.True);
            DigTo(game, merchant);

            Assert.That(plaza.Things, Has.Member(plaza.Statue));
            Assert.That(plaza.Statue.Position, Is.EqualTo(plaza.Layout.StatueBase));
            Assert.That(plaza.Things, Has.Member(plaza.MerchantThing));
            Assert.That(plaza.CanPlace("bench_log", plaza.Statue.Position), Is.EqualTo(PlacementCheck.Overlaps));

            double before = plaza.Merchant.UntilNext;
            game.Mall.Tick(k_Dt);
            Assert.That(plaza.Merchant.UntilNext, Is.LessThan(before), "좌판 주머니를 판 뒤에야 행상 시계가 돈다");
        }

        // q37 장식 놓기: 광장 장식은 사물 목록이 아니라 장식 목록에 있어도 세어진다(리뷰 B1)
        [Test]
        public void PlaceQuest_CountsPlazaDecor()
        {
            TestGame game = new TestGame();
            game.State.AddCoins(100000d);

            Assert.That(GoalCounter.Current(GoalCounter.k_Place, "bench_log", game.Mall), Is.EqualTo(0));
            Assert.That(game.Mall.Plaza.TryBuy("bench_log", new Vector2(2f, -6.2f)), Is.True);
            Assert.That(GoalCounter.Current(GoalCounter.k_Place, "bench_log", game.Mall), Is.EqualTo(1));
        }

        // 행상 주머니를 파기 전엔 행상 시계가 멈춰 있다
        [Test]
        public void Merchant_DoesNotCount_BeforeItsPocket()
        {
            TestGame game = new TestGame(openPlaza: false);
            double before = game.Mall.Plaza.Merchant.UntilNext;
            game.Mall.Tick(k_Dt);

            Assert.That(game.Mall.Plaza.Merchant.UntilNext, Is.EqualTo(before));
        }

        // 손님은 판 굴길을 따라 빵집 주머니 문으로 들어간다
        [Test]
        public void Visitors_WalkDownTheShaft_IntoBakeryPocket()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            DigTo(game, Pocket(game, PlazaPocketData.k_Door, BakeryArea.k_Id));
            int entered = 0;
            game.Bus.Subscribe<Events.BakeryVisitorArrived>(_ => entered++);

            for (double t = 0d; t < 60d && entered == 0; t += k_Dt)
            {
                game.Mall.Tick(k_Dt);
            }

            Assert.That(entered, Is.GreaterThan(0));
        }

        // 저장은 판 칸을 지키고, 불러오면 석상 · 행상 주머니가 그대로 열려 있다
        [Test]
        public void Save_KeepsPlazaCells()
        {
            TestGame game = new TestGame(openPlaza: false);
            DigTo(game, Pocket(game, PlazaPocketData.k_Door, BakeryArea.k_Id));
            string json = JsonConvert.SerializeObject(GameSave.Capture(game.State, game.Mall));

            TestGame loaded = new TestGame(openPlaza: false);
            GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(json), loaded.State, loaded.Mall, loaded.Tables);

            Assert.That(loaded.Mall.Plaza.Grid.Cells, Is.EquivalentTo(game.Mall.Plaza.Grid.Cells));
            Assert.That(loaded.Mall.Plaza.Layout.Nav.IsWalkable(loaded.Mall.Plaza.BakeryDoor.Floor), Is.True);
        }

        // 설계 64 사슬: 1장 밭 주머니 파기 → 밭, 7장 끝은 마당 파기 → 석상 · 행상 · 똥 · 장식
        // 파기 걸음은 그 곳에 팔 칸이 없으면 바로 된 것(먼저 더 파 두면 「마당 파기」 넷이 남은 칸보다 많아 막히던 것)
        [Test]
        public void DigStep_IsDone_WhenNothingLeftToDig()
        {
            TestGame game = new TestGame(openPlaza: false);
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q36";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            QuestLog quests = game.Mall.Quests;
            game.Mall.Tick(k_Dt);
            Assert.That(quests.Current.Id, Is.EqualTo("q36"));
            Assert.That(quests.IsDone, Is.False, "팔 칸이 남았으면 센다");

            game.Mall.Plaza.DigAll();
            game.Mall.Tick(k_Dt);
            Assert.That(game.Mall.Plaza.Things.OfType<DigInteractable>(), Is.Empty);
            Assert.That(quests.Count, Is.EqualTo(quests.Current.Count));
            Assert.That(quests.IsDone, Is.True, "팔 칸이 없으면 바로 된 것");
        }

        // 2026-10-28 플레이: 주머니 파기 걸음은 그 주머니로 가는 칸만 센다(옆 칸 · 다른 곳은 안 센다). 길잡이도 그 길의 칸
        [Test]
        public void PocketDigStep_CountsOnlyCellsTowardItsPocket()
        {
            TestGame game = new TestGame(openPlaza: false);
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q33";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            game.Mall.Start(PlazaArea.k_Id);
            PlazaArea plaza = game.Mall.Plaza;
            QuestLog quests = game.Mall.Quests;
            Assert.That(quests.Current.Id, Is.EqualTo("q33"));
            Assert.That(plaza.DigsTo(BakeryArea.k_Id), Is.EqualTo(quests.Current.Count), "걸음 수 = 남은 손질 수(둑 + 주머니)");

            DigInteractable farmCell = plaza.Things.OfType<DigInteractable>().Single();
            Assert.That(GoalCounter.Suits(GoalCounter.k_Dig, BakeryArea.k_Id, farmCell), Is.False, "밭 주머니는 빵집 가는 길이 아니다");
            Assert.That(GoalCounter.Suits(GoalCounter.k_Dig, BakeryArea.k_Id, plaza.Dam), Is.True, "둑을 터야 빵집 주머니에 닿는다(설계 65)");

            plaza.Grid.Dig(farmCell.Cell);
            game.Mall.Farm.Grid.Dig(game.Mall.Farm.Grid.Frontier().First());
            Assert.That(quests.Count, Is.EqualTo(0), "밭 주머니 · 농장 칸은 안 센다");

            Burst(plaza);
            Assert.That(quests.Count, Is.EqualTo(1), "둑 하나 = 한 걸음");
            plaza.Grid.Dig(Pocket(game, PlazaPocketData.k_Door, BakeryArea.k_Id).Cell);
            Assert.That(quests.IsDone, Is.True);
        }

        // 둑이 터진 것처럼 그 줄의 굴길 칸을 모두 판다(값 · 횟수 없이)
        private static void Burst(PlazaArea plaza)
        {
            foreach (Cell cell in plaza.Dam.Cells.ToArray())
            {
                if (!plaza.Grid.Contains(cell))
                {
                    plaza.Grid.Dig(cell);
                }
            }
        }

        // 주머니 파기 걸음 넷의 수 = 그때 남은 손질 수(둑 + 주머니). 표나 주머니 자리를 고치면 여기서 걸린다(설계 65 리뷰: q35가 3이라 1/3로 시작했다)
        [Test]
        public void PocketDigSteps_CountsMatchActionsLeft()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;

            foreach (string id in new[] { "q32", "q33", "q34", "q35" })
            {
                QuestTable step = game.Tables.Get<QuestTable>(id);
                PlazaPocketData pocket = Config(game).Pockets.First(p => (p.To ?? p.Use) == step.Param);
                Assert.That(plaza.DigsTo(step.Param), Is.EqualTo(step.Count), id);

                while (!plaza.Grid.CanDig(pocket.Cell))
                {
                    Assert.That(plaza.DamOnWayTo(step.Param), Is.True, id);
                    Assert.That(plaza.OnWayTo(step.Param, pocket.Cell), Is.False, id);
                    Burst(plaza);
                }

                Assert.That(plaza.DamOnWayTo(step.Param), Is.False, id);
                Assert.That(plaza.OnWayTo(step.Param, pocket.Cell), Is.True, id);
                plaza.Grid.Dig(pocket.Cell);
                Assert.That(plaza.DigsTo(step.Param), Is.EqualTo(0), id);
            }
        }

        // 설계 64 때의 저장(굴길 줄을 반만 판 것)에서도 둑을 가리키고, 남은 손질 수는 터야 할 둑 + 주머니(설계 65 리뷰)
        [Test]
        public void HalfDugRows_FromOldSaves_StillCountDams()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;

            for (int row = 2; row <= 4; row++)
            {
                plaza.Grid.Dig(new Cell(-1, row));
            }

            Assert.That(plaza.Dam.Row, Is.EqualTo(2), "둑은 다 안 판 첫 줄에");
            Assert.That(plaza.DigsTo(RestaurantArea.k_Id), Is.EqualTo(5), "둑 넷(줄 2~5) + 주머니");
            Assert.That(plaza.DamOnWayTo(RestaurantArea.k_Id), Is.True);
            Assert.That(plaza.DigsTo(FishingArea.k_Id), Is.EqualTo(1), "왼쪽 주머니는 바로 팔 수 있다");
            Assert.That(plaza.DamOnWayTo(FishingArea.k_Id), Is.False);

            Burst(plaza);
            Assert.That(plaza.Grid.Contains(new Cell(0, 2)), Is.True, "터지면 남은 칸만 열린다");
            Assert.That(plaza.Dam.Row, Is.EqualTo(3));
            Assert.That(plaza.DigsTo(RestaurantArea.k_Id), Is.EqualTo(4));
        }

        private static void StandAtDam(TestGame game)
        {
            PlazaArea plaza = game.Mall.Plaza;
            game.Mall.Wombat.Mover.Place(plaza.Dam.Position + new Vector2(0f, 0.3f));
            game.Mall.Tick(k_Dt);
        }

        // 파는 동작이 끝날 때까지 흘린다
        private static void Rest(TestGame game)
        {
            for (int i = 0; i < 100 && game.Mall.Wombat.Busy; i++)
            {
                game.Mall.Tick(k_Dt);
            }
        }

        // 설계 65 냇물 마을: 둑을 damHits번 치면 그 줄 굴길 칸이 한 번에 열리고 냇물이 그 줄 끝까지 내려간다. 값은 첫 타에 한 번
        [Test]
        public void Dam_BreaksAfterHits_OpensTheRow_AndStreamFollows()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            PlazaConfigTable config = Config(game);
            game.State.AddCoins(10000d);
            game.Mall.StartNew();
            int row = plaza.Dam.Row;
            double cost = plaza.Grid.DigCost;
            double coins = game.State.Coins;
            Assert.That(plaza.Layout.Stream.YMin, Is.EqualTo(-plaza.Layout.Cells.RowTop(row)).Within(1e-4f), "냇물은 판 줄 끝까지");
            Assert.That(plaza.Dam.Position.Y, Is.EqualTo(plaza.Layout.Stream.YMin).Within(1e-4f));
            StandAtDam(game);
            Assert.That(plaza.Target, Is.SameAs(plaza.Dam));

            for (int hit = 1; hit < config.DamHits; hit++)
            {
                Assert.That(plaza.TryInteract(), Is.True);
                Assert.That(plaza.TryInteract(), Is.False, "파는 동작 중에는 못 친다");
                Rest(game);
                Assert.That(plaza.Dam.Hits, Is.EqualTo(hit));
                Assert.That(plaza.Grid.Contains(new Cell(0, row)), Is.False);
            }

            Assert.That(game.State.Coins, Is.EqualTo(coins - cost), "값은 첫 타에 한 번");
            Assert.That(plaza.TryInteract(), Is.True);
            Assert.That(plaza.Grid.Contains(new Cell(-1, row)) && plaza.Grid.Contains(new Cell(0, row)), Is.True, "그 줄 굴길 칸이 한 번에");
            Assert.That(game.State.Coins, Is.EqualTo(coins - cost));
            Assert.That(plaza.Layout.Stream.YMin, Is.EqualTo(-plaza.Layout.Cells.RowTop(row + 1)).Within(1e-4f), "물이 한 줄 내려갔다");
            Assert.That(plaza.Dam.Row, Is.EqualTo(row + 1));
            Assert.That(plaza.Dam.Hits, Is.EqualTo(0));
        }

        // 코인이 모자라면 첫 타를 못 친다. 굴길 칸은 편집 모드의 흙 탭으로 못 판다
        [Test]
        public void Dam_NeedsCoins_AndShaftCellsAreNotDugByHand()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            game.Mall.StartNew();
            game.State.TrySpendCoins(game.State.Coins);
            StandAtDam(game);

            Assert.That(plaza.Target, Is.SameAs(plaza.Dam));
            Assert.That(plaza.TryInteract(), Is.False);
            Assert.That(plaza.Dam.Hits, Is.EqualTo(0));
            Assert.That(plaza.DigAt(plaza.Layout.Cells.CellCenter(new Cell(0, plaza.Dam.Row))), Is.Null);
            Assert.That(plaza.DigAt(plaza.Layout.Cells.CellCenter(Pocket(game, PlazaPocketData.k_Door, FarmArea.k_Id).Cell)), Is.Not.Null);
        }

        // 치던 둑은 저장에 남는다(값을 이미 냈다)
        [Test]
        public void Save_KeepsDamHits()
        {
            TestGame game = new TestGame(openPlaza: false);
            game.State.AddCoins(10000d);
            game.Mall.StartNew();
            StandAtDam(game);
            Assert.That(game.Mall.Plaza.TryInteract(), Is.True);
            string json = JsonConvert.SerializeObject(GameSave.Capture(game.State, game.Mall));

            TestGame loaded = new TestGame(openPlaza: false);
            GameSave.Apply(JsonConvert.DeserializeObject<SaveData>(json), loaded.State, loaded.Mall, loaded.Tables);

            Assert.That(loaded.Mall.Plaza.Dam.Hits, Is.EqualTo(1));
            Assert.That(loaded.Mall.Plaza.Dam.Cost, Is.EqualTo(0d));
            Assert.That(loaded.Mall.Plaza.Things, Has.Member(loaded.Mall.Plaza.Dam));
        }

        // 냇물 위에는 똥이 안 생긴다(둑 옆 땅에는 생긴다)
        [Test]
        public void Stream_TakesNoPoop()
        {
            TestGame game = new TestGame(openPlaza: false);
            PlazaArea plaza = game.Mall.Plaza;
            NavRect stream = plaza.Layout.Stream;
            float y = (stream.YMin + stream.YMax) / 2f;

            Assert.That(plaza.TryDropPoop(new Vector2(0f, y)), Is.False);
            Assert.That(plaza.TryDropPoop(new Vector2(stream.XMax + 1f, y)), Is.True);
        }

        // param 없는 파기 걸음(마당)도 그 곳에서 판 칸만
        [Test]
        public void YardDigStep_IgnoresOtherAreas()
        {
            TestGame game = new TestGame(openPlaza: false);
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q36";
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            game.Mall.Start(PlazaArea.k_Id);
            QuestLog quests = game.Mall.Quests;
            game.Mall.Tick(k_Dt);
            Assert.That(game.Mall.Active.Id, Is.EqualTo(PlazaArea.k_Id));
            Assert.That(quests.Current.Id, Is.EqualTo("q36"));
            Assert.That(quests.Count, Is.EqualTo(0));

            game.Mall.Start(game.Mall.Farm.Id);
            game.Mall.Farm.Grid.Dig(game.Mall.Farm.Grid.Frontier().First());
            Assert.That(quests.Count, Is.EqualTo(0), "농장에서 판 칸");
            game.Mall.Start(PlazaArea.k_Id);
            game.Mall.Plaza.Grid.Dig(game.Mall.Plaza.Grid.Frontier().First());
            Assert.That(quests.Count, Is.EqualTo(1));
        }

        [Test]
        public void QuestChain_DigStepsSitBeforeEachPocket()
        {
            TestGame game = new TestGame(openPlaza: false);
            string[] ids = game.Tables.GetAll<QuestTable>().Select(step => step.Id).ToArray();

            Assert.That(ids.Take(3), Is.EqualTo(new[] { "q27", "q32", "q04" }));
            Assert.That(ids.SkipWhile(id => id != "q25").ToArray(), Is.EqualTo(new[] { "q25", "q36", "q14", "q12", "q13", "q37" }));
            Assert.That(game.Tables.Get<QuestTable>("q33").Count, Is.EqualTo(2), "줄 2 왼쪽 + 빵집 주머니");
            Assert.That(game.Tables.Get<LetterTable>("l12").When, Is.EqualTo("start:q36"));
        }
    }
}
