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
            Assert.That(plaza.Things.OfType<DigInteractable>().Count(), Is.EqualTo(3));
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
            Assert.That(game.Mall.Plaza.TryBuy("bench_log", new Vector2(0f, -6.2f)), Is.True);
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
            Assert.That(plaza.DigsTo(BakeryArea.k_Id), Is.EqualTo(quests.Current.Count), "걸음 수 = 가장 짧은 길");

            DigInteractable side = plaza.Things.OfType<DigInteractable>().First(dig => dig.Cell.Equals(new Cell(0, 2)));
            DigInteractable toward = plaza.Things.OfType<DigInteractable>().First(dig => dig.Cell.Equals(new Cell(-1, 2)));
            Assert.That(GoalCounter.Suits(GoalCounter.k_Dig, BakeryArea.k_Id, side), Is.False);
            Assert.That(GoalCounter.Suits(GoalCounter.k_Dig, BakeryArea.k_Id, toward), Is.True);

            plaza.Grid.Dig(side.Cell);
            game.Mall.Farm.Grid.Dig(game.Mall.Farm.Grid.Frontier().First());
            Assert.That(quests.Count, Is.EqualTo(0), "옆 칸 · 농장 칸은 안 센다");

            plaza.Grid.Dig(toward.Cell);
            Assert.That(quests.Count, Is.EqualTo(1));
            plaza.Grid.Dig(Pocket(game, PlazaPocketData.k_Door, BakeryArea.k_Id).Cell);
            Assert.That(quests.IsDone, Is.True);
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
