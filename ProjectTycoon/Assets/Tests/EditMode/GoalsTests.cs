using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 54 검증 1 · 2 · 4 · 6 · 7 · 8: 퀘스트 사슬 · 「가기」 길잡이 · 오늘의 일 · 저장
    public sealed class GoalsTests
    {
        private const double k_Dt = 0.05;
        private static readonly DateTime k_Noon = new DateTime(2026, 10, 8, 12, 0, 0);

        // 퀘스트를 그 걸음으로 옮긴다(저장 → 불러오기, 공개 길)
        private static void JumpTo(TestGame game, string id)
        {
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = id;
            GameSave.Apply(data, game.State, game.Mall, game.Tables);
        }

        private static List<Events.GoalRewarded> Rewards(TestGame game)
        {
            List<Events.GoalRewarded> rewards = new List<Events.GoalRewarded>();
            game.Bus.Subscribe<Events.GoalRewarded>(rewards.Add);
            return rewards;
        }

        // 1: 새 게임 첫 걸음은 식빵 굽기. 굽기를 시작하면 다 되고, 받으면 코인 · 다음 걸음(진행은 처음부터)
        [Test]
        public void Quest_FirstStep_CountsBakeAndClaimAdvances()
        {
            TestGame game = new TestGame();
            QuestLog quests = game.Mall.Quests;
            Assert.That(quests.Current.Id, Is.EqualTo("q01"));
            Assert.That(quests.TryClaim(), Is.False);

            Assert.That(game.Bakery.Ovens[0].TryStart(game.Tables.Get<BreadTable>("b01")), Is.True);

            Assert.That(quests.IsDone, Is.True);
            double coins = game.State.Coins;
            double reward = quests.Current.Coins;
            Assert.That(quests.TryClaim(), Is.True);
            Assert.That(game.State.Coins, Is.EqualTo(coins + reward));
            Assert.That(quests.Current.Id, Is.EqualTo("q02"));
            Assert.That(quests.Count, Is.EqualTo(0));
        }

        // 다른 빵을 구우면 세지 않는다(param), 사건은 걸음이 시작된 뒤부터
        [Test]
        public void Quest_EventStep_CountsOnlyItsTarget()
        {
            TestGame game = new TestGame();
            game.State.AddCoins(10000d);
            Assert.That(game.Bakery.TryChoose(ActionTable.k_Bake, game.Bakery.Ovens[0], "b02"), Is.True);

            Assert.That(game.Mall.Quests.Count, Is.EqualTo(0), "크루아상은 식빵 굽기가 아니다");
        }

        // 4: 받은 뒤 상태로 이미 된 걸음은 넘기고 보상을 모아 한 번에(2026-10-08 사용자)
        [Test]
        public void Quest_Claim_SkipsStepsAlreadyDoneAndPaysTogether()
        {
            TestGame game = new TestGame();
            QuestLog quests = game.Mall.Quests;
            game.State.AddCoins(10000d);
            Assert.That(game.Bakery.TryChoose(ActionTable.k_Bake, game.Bakery.Ovens[0], "b02"), Is.True);
            SaveData starred = GameSave.Capture(game.State, game.Mall);
            starred.Stars["bakery"] = 5;
            GameSave.Apply(starred, game.State, game.Mall, game.Tables);
            JumpTo(game, "q14");
            List<Events.GoalRewarded> rewards = Rewards(game);
            game.Bus.Publish(new Events.PoopsCleaned(game.Bakery, "manure", 1));
            double coins = game.State.Coins;

            Assert.That(quests.TryClaim(), Is.True);

            Assert.That(quests.Current.Id, Is.EqualTo("q16"), "빵집 별 5(q23) · 크루아상 열기(q15)는 이미 됐다");
            Assert.That(rewards.Count, Is.EqualTo(1));
            Assert.That(rewards[0].Coins, Is.EqualTo(new[] { "q14", "q23", "q15" }.Sum(id => game.Tables.Get<QuestTable>(id).Coins)));
            Assert.That(game.State.Coins, Is.EqualTo(coins + rewards[0].Coins));
        }

        // 메뉴가 대상인 걸음의 「가기」는 그 메뉴를 가리키고 웜뱃은 그대로(2026-10-08 사용자)
        [Test]
        public void Quest_Go_MenuStepPointsTheMenu()
        {
            TestGame game = new TestGame();
            JumpTo(game, "q09");
            List<string> menus = new List<string>();
            game.Bus.Subscribe<Events.MenuPointed>(e => menus.Add(e.Menu));

            Assert.That(game.Mall.Quests.Go(), Is.True);

            Assert.That(menus, Is.EqualTo(new[] { QuestTable.k_MenuEdit }));
            Assert.That(game.Mall.Guide.Active, Is.False);
            Assert.That(game.Mall.Wombat.Guided, Is.False);
        }

        // 2: 「가기」로 빵집 → 광장 → 농장 문 → 빈 밭까지 혼자 걷는다(곳이 바뀌어도 다리를 잇는다)
        [Test]
        public void Guide_WalksThroughPlazaToFarmPlot()
        {
            TestGame game = new TestGame();
            JumpTo(game, "q05");
            Mall mall = game.Mall;
            Assert.That(mall.Active, Is.SameAs(game.Bakery));

            Assert.That(mall.Quests.Go(), Is.True);
            Assert.That(mall.Guide.Walking, Is.True);
            Assert.That(mall.Guide.Toward, Is.EqualTo(FarmArea.k_Id), "다른 곳이면 화면 끝 이름표");

            HashSet<string> visited = new HashSet<string> { mall.Active.Id };

            for (double t = 0d; t < 60d && (mall.Guide.Walking || mall.Active != mall.Farm); t += k_Dt)
            {
                mall.Tick(k_Dt);
                visited.Add(mall.Active.Id);
            }

            Assert.That(visited, Does.Contain(PlazaArea.k_Id));
            Assert.That(mall.Active, Is.SameAs(mall.Farm));
            Assert.That(mall.Guide.Walking, Is.False, "도착");
            Assert.That(mall.Guide.Active, Is.True, "화살표는 걸음이 끝날 때까지");
            PlotInteractable plot = mall.Guide.Pointed as PlotInteractable;
            Assert.That(plot, Is.Not.Null);
            Assert.That(plot.IsTilled && plot.IsEmpty, Is.True, "심기 걸음은 빈 밭으로");
            Assert.That(mall.Farm.IsInRange(plot), Is.True);
            Assert.That(mall.Guide.Toward, Is.Null);
        }

        // 2026-10-09 리뷰: 새 게임 첫 걸음 「가기」로 오븐에 닿으면 행동 버튼의 대상은 오븐(곁의 흙 칸이 가로채지 않는다). 조이스틱을 움직이면 다시 가까운 것
        [Test]
        public void Guide_Arrival_TargetsTheGuidedThing()
        {
            TestGame game = new TestGame();
            Mall mall = game.Mall;
            Assert.That(mall.Quests.Go(), Is.True);

            for (double t = 0d; t < 30d && mall.Guide.Walking; t += k_Dt)
            {
                mall.Tick(k_Dt);
            }

            Assert.That(mall.Guide.Walking, Is.False, "도착");
            Assert.That(game.Bakery.Target, Is.SameAs(game.Bakery.Ovens[0]));
        }

        // 2026-10-09 리뷰: 「횟집 열기」는 광장의 닫힌 횟집 문 앞까지 걸어간다(전에는 설 점이 없어 광장 입구에서 멈췄다)
        [Test]
        public void Guide_ShopGateStep_WalksToTheGate()
        {
            TestGame game = new TestGame();
            JumpTo(game, "q24");
            Mall mall = game.Mall;
            Assert.That(mall.Quests.Go(), Is.True);

            for (double t = 0d; t < 60d && (mall.Guide.Walking || mall.Active != mall.Plaza); t += k_Dt)
            {
                mall.Tick(k_Dt);
            }

            Assert.That(mall.Guide.Pointed, Is.InstanceOf<ShopGateInteractable>());
            Assert.That(mall.Plaza.Target, Is.SameAs(mall.Guide.Pointed));
        }

        // 2026-10-09 리뷰: 「바위 뚫기」(down1)는 그 구간의 바위로 데려간다(같은 거리의 반대쪽 바위가 아니라)
        [Test]
        public void Guide_RockStep_PointsTheStepsStretch()
        {
            TestGame game = new TestGame();
            JumpTo(game, "q20");
            Mall mall = game.Mall;
            QuestTable step = mall.Quests.Current;
            game.Bus.Publish(new Events.Passed(mall.Active, FishingArea.k_Id));
            Assert.That(mall.Active, Is.SameAs(mall.Fishing));

            Assert.That(mall.Quests.Go(), Is.True);

            RockInteractable rock = mall.Guide.Pointed as RockInteractable;
            Assert.That(rock, Is.Not.Null);
            Assert.That(rock.Stretch.Id, Is.EqualTo(step.Param));
        }

        // 조이스틱을 건드리면 걷기만 멈추고 가리키기는 남는다. 걸음이 다 되면 꺼진다
        [Test]
        public void Guide_JoystickStopsWalking_StepDoneStopsGuide()
        {
            TestGame game = new TestGame();
            Mall mall = game.Mall;
            mall.Wombat.Mover.Place(game.Bakery.Layout.HoleFloor);
            Assert.That(mall.Quests.Go(), Is.True);
            mall.Tick(k_Dt);
            Assert.That(mall.Guide.Walking, Is.True);

            mall.Wombat.SetInput(new Vector2(1f, 0f));
            mall.Tick(k_Dt);
            mall.Wombat.SetInput(Vector2.Zero);
            mall.Tick(k_Dt);

            Assert.That(mall.Guide.Walking, Is.False);
            Assert.That(mall.Guide.Active, Is.True);
            Assert.That(mall.Guide.Pointed, Is.InstanceOf<OvenInteractable>());

            Assert.That(game.Bakery.Ovens[0].TryStart(game.Tables.Get<BreadTable>("b01")), Is.True);
            mall.Tick(k_Dt);

            Assert.That(mall.Guide.Active, Is.False);
        }

        // 6: 오늘의 일은 열린 곳에서만 다섯, 같은 곳은 둘까지(횟집이 닫혀 있으면 회 미션은 없다)
        [Test]
        public void Daily_PicksOpenAreasOnly_SameAreaCapped()
        {
            TestGame game = new TestGame(clock: () => k_Noon);
            game.Mall.Tick(k_Dt);
            IReadOnlyList<DailyBoard.Mission> missions = game.Mall.Missions.Missions;

            Assert.That(missions.Count, Is.EqualTo(5));
            Assert.That(missions.Select(m => m.Row.Id).Distinct().Count(), Is.EqualTo(5));
            Assert.That(missions.Any(m => m.Row.Area == RestaurantArea.k_Id), Is.False);
            Assert.That(missions.Where(m => m.Row.Area != null).GroupBy(m => m.Row.Area).Max(g => g.Count()), Is.LessThanOrEqualTo(2));
            Assert.That(game.Mall.Missions.Day, Is.EqualTo("2026-10-08"));
        }

        // 같은 날은 같은 다섯(그날의 씨앗)
        [Test]
        public void Daily_SameDay_PicksSameMissions()
        {
            TestGame a = new TestGame(clock: () => k_Noon);
            TestGame b = new TestGame(seed: 7, clock: () => k_Noon.AddHours(3));
            a.Mall.Tick(k_Dt);
            b.Mall.Tick(k_Dt);

            Assert.That(b.Mall.Missions.Missions.Select(m => m.Row.Id), Is.EqualTo(a.Mall.Missions.Missions.Select(m => m.Row.Id)));
        }

        // 하나 다 하면 받기로 점수, 점수가 닿은 상자만 열린다(코인 = 분어치 × 그 장의 1분 수입)
        [Test]
        public void Daily_ClaimGivesPoints_ChestOpensAtThreshold()
        {
            // 후보를 똥 치우기 하나로
            TestGame game = new TestGame(TestTables.Load("MissionTable", rows =>
            {
                foreach (JToken row in rows.Where(r => (string)r["id"] != "m_clean").ToList())
                {
                    row.Remove();
                }

                rows[0]["count"] = 1;
            }), clock: () => k_Noon);
            DailyBoard board = game.Mall.Missions;
            game.Mall.Tick(k_Dt);
            Assert.That(board.Missions.Count, Is.EqualTo(1));
            MissionChestTable first = board.Chests[0];
            Assert.That(board.TryOpen(first.Chest), Is.False, "점수가 모자라다");
            Assert.That(board.TryClaim(0), Is.False, "아직 안 했다");

            game.Bus.Publish(new Events.PoopsCleaned(game.Bakery, "manure", 1));
            Assert.That(board.HasReward, Is.True);
            Assert.That(board.TryClaim(0), Is.True);
            Assert.That(board.TryClaim(0), Is.False, "한 번만");
            Assert.That(board.Points, Is.EqualTo(game.Tables.Get<MissionTable>("m_clean").Points));

            double coins = game.State.Coins;
            Assert.That(board.TryOpen(first.Chest), Is.EqualTo(board.Points >= first.Points));
            Assert.That(game.State.Coins, Is.EqualTo(coins + first.Minutes * first.CoinsPerMinute));
            Assert.That(board.TryOpen(first.Chest), Is.False, "한 번만");
            Assert.That(board.TryOpen(board.Chests[1].Chest), Is.False);
        }

        // 7: 새벽 5시(missionResetHour)를 넘기면 새로 뽑고 점수 · 상자가 처음부터
        [Test]
        public void Daily_NewDayAtResetHour_RollsAgain()
        {
            DateTime now = new DateTime(2026, 10, 9, 4, 59, 0);
            TestGame game = new TestGame(clock: () => now);
            DailyBoard board = game.Mall.Missions;
            game.Mall.Tick(k_Dt);
            Assert.That(board.Day, Is.EqualTo("2026-10-08"), "5시 전은 전날");
            Assert.That(board.SecondsLeft, Is.EqualTo(60d).Within(1e-6));
            game.Bus.Publish(new Events.PoopsCleaned(game.Bakery, "manure", 100));
            game.Bus.Publish(new Events.BlessingChanged(game.State.Blessing, true));

            now = now.AddMinutes(1);
            game.Mall.Tick(k_Dt);

            Assert.That(board.Day, Is.EqualTo("2026-10-09"));
            Assert.That(board.Points, Is.EqualTo(0));
            Assert.That(board.Opened, Is.Empty);
            Assert.That(board.Missions.All(m => m.Progress == 0 && !m.Claimed), Is.True);
        }

        // 8: 퀘스트 걸음 · 진행과 오늘의 일(하루 · 진행 · 받음 · 점수 · 연 상자)이 저장되고 같은 날 다시 열면 이어진다
        [Test]
        public void Save_RoundTrip_KeepsQuestAndMissions()
        {
            TestGame game = new TestGame(clock: () => k_Noon);
            game.Mall.Tick(k_Dt);
            JumpTo(game, "q03");
            game.Bus.Publish(new Events.BakeryVisitorPaid(null, 10d, false));
            DailyBoard board = game.Mall.Missions;
            game.Bus.Publish(new Events.PoopsCleaned(game.Bakery, "manure", 100));
            int claimed = board.Missions.ToList().FindIndex(m => m.IsDone);

            if (claimed >= 0)
            {
                Assert.That(board.TryClaim(claimed), Is.True);
            }

            SaveData data = GameSave.Capture(game.State, game.Mall);
            TestGame loaded = new TestGame(clock: () => k_Noon.AddHours(2));
            GameSave.Apply(data, loaded.State, loaded.Mall, loaded.Tables);
            loaded.Mall.Tick(k_Dt);

            Assert.That(loaded.Mall.Quests.Current.Id, Is.EqualTo("q03"));
            Assert.That(loaded.Mall.Quests.Count, Is.EqualTo(1));
            DailyBoard back = loaded.Mall.Missions;
            Assert.That(back.Day, Is.EqualTo(board.Day));
            Assert.That(back.Missions.Select(m => (m.Row.Id, m.Progress, m.Claimed)), Is.EqualTo(board.Missions.Select(m => (m.Row.Id, m.Progress, m.Claimed))));
            Assert.That(back.Points, Is.EqualTo(board.Points));
        }

        // 2026-10-09: 걸음은 id로 돌아온다(행 번호가 어긋나도 — 표 순서를 바꾼 뒤의 저장)
        [Test]
        public void Save_RestoresQuestById_EvenIfIndexMoved()
        {
            TestGame game = new TestGame();
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = "q23";
            data.QuestIndex = 3;

            GameSave.Apply(data, game.State, game.Mall, game.Tables);

            Assert.That(game.Mall.Quests.Current.Id, Is.EqualTo("q23"));
        }

        // 2026-10-09 리뷰: 유물을 다 모았으면 「유물 뽑기」 걸음은 할 수 없으니 다 된 것(옛 저장이 막히지 않게)
        [Test]
        public void Quest_ImpossibleStep_CountsAsDone()
        {
            TestGame game = new TestGame();
            JumpTo(game, "q13");
            Assert.That(game.Mall.Quests.IsDone, Is.False);

            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.Relics = game.Tables.GetAll<RelicTable>().ToDictionary(relic => relic.Id, relic => relic.Values.Length);
            GameSave.Apply(data, game.State, game.Mall, game.Tables);

            Assert.That(game.State.Relics.Complete, Is.True);
            Assert.That(game.Mall.Quests.IsDone, Is.True);
        }

        // 옛 저장(퀘스트 · 오늘의 일 칸 없음)은 사슬 처음부터, 오늘의 일은 새로 뽑는다
        [Test]
        public void Save_OldFileWithoutGoals_StartsChainAndRolls()
        {
            TestGame game = new TestGame(clock: () => k_Noon);
            SaveData data = GameSave.Capture(game.State, game.Mall);
            data.QuestId = null;
            data.QuestIndex = 0;
            data.QuestProgress = 0;
            data.Missions = null;

            GameSave.Apply(data, game.State, game.Mall, game.Tables);
            game.Mall.Tick(k_Dt);

            Assert.That(game.Mall.Quests.Current.Id, Is.EqualTo("q01"));
            Assert.That(game.Mall.Missions.Missions.Count, Is.EqualTo(5));
        }
    }
}
