using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 52 검증: 강가 낚시 한 판. 둑에 서서 버튼(TryInteract)으로 던지고 · 낚아채고, 버튼 누름(SetHolding)으로 감는 실제 길로 다룬다.
    // 낚시터 난수는 늘 0.3(구간 0 물고기 = 붕어 · 크기 = 작은 · 무게 = 1.5 + 0.3 × 1.0, 물기까지 = min + 0.3 × (max − min), 가짜 입질 0번).
    // 설계 53 → 설계 58 → 설계 60 검증: 층계 웅덩이 — 집 층 물 3열로 시작 · 비버에게 물고기를 주면 물이 한 열(물속 발견) · 다 넓히면 댐 헐기(아래 층) · 바위 뚫기(위 층) · 잔해는 코인만 · 층별 물고기 · 좌대 손님 · 젖은 벽 · 저장
    public sealed class FishingTests
    {
        private const double k_Dt = 0.05;
        private const string k_Item = "fish_crucian";
        private const double k_Weight = 1.8;

        private sealed class FixedRandom : IRandom
        {
            private readonly double m_value;

            public FixedRandom(double value)
            {
                m_value = value;
            }

            public double NextDouble()
            {
                return m_value;
            }
        }

        private TableSet m_tables;
        private EventBus m_bus;
        private ZooState m_state;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private FishingArea m_fishing;
        private Mall m_mall;

        private FishingConfigTable Config => m_tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
        private double BiteWait => BiteWaitFor(0.3);

        private double BiteWaitFor(double roll)
        {
            return Config.BiteWaitMin + roll * (Config.BiteWaitMax - Config.BiteWaitMin);
        }

        private void Create(double roll = 0.3)
        {
            m_tables = TestTables.Load();
            m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery).MaxCustomers = 0;
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            m_state.AddCoins(100000d);
            Wombat wombat = new Wombat(m_tables, m_state);
            m_shop = new BakeryArea(m_state, m_tables, new SequenceRandom(new double[4000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 40000).ToArray()), wombat, m_bus);
            m_fishing = new FishingArea(m_tables, new FixedRandom(roll), wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, new FarmArea(m_tables, new FixedRandom(0.99), wombat, m_bus), m_bus, m_fishing);
            m_bus.Publish(new Events.Passed(m_shop, PlazaArea.k_Id));
            m_bus.Publish(new Events.Passed(m_plaza, FishingArea.k_Id));
            Run(k_Dt);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        // 둑 위(물 바로 위, 가운데 댐 띠 밖)에 선다
        private void AtWater()
        {
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.Layout.CenterX + 1.8f, m_fishing.Water.Rect.YMax + 0.4f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(m_fishing.Water), "물가가 대상");
        }

        private void Press(string action)
        {
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(action), "버튼 행동");
            Assert.That(m_fishing.TryInteract(), Is.True, "버튼");
        }

        private void Cast()
        {
            AtWater();
            Press(ActionTable.k_Cast);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Waiting));
        }

        private void Hook()
        {
            Cast();
            Run(BiteWait + k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Bite), "물었다");
            Press(ActionTable.k_Strike);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Tug), "걸렸다");
        }

        // 줄다리기: 버튼을 holdWhileRunning이면 늘, 아니면 쉴 때만 누른 채 끝날 때까지
        private double Tug(bool holdWhileRunning, double maxSeconds = 60d)
        {
            double t = 0d;

            while (m_fishing.Phase == FishingPhase.Tug && t < maxSeconds)
            {
                m_mall.Wombat.SetHolding(holdWhileRunning || !m_fishing.Running);
                m_mall.Tick(k_Dt);
                t += k_Dt;
            }

            m_mall.Wombat.SetHolding(false);
            return t;
        }

        [Test]
        public void Cast_WaitsThenBites_StrikeHooks()
        {
            Create();
            Fish hooked = null;
            m_bus.Subscribe<Events.Hooked>(e => hooked = e.Fish);
            Cast();
            Run(BiteWait - 0.2);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Waiting), "아직 기다린다");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Strike), "기다리는 동안 버튼은 낚아채기");
            Run(0.3);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Bite), "물었다");
            Press(ActionTable.k_Strike);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Tug));
            Assert.That(hooked.Kind.Id, Is.EqualTo("crucian"), "구간 0 물고기 비중에서");
            Assert.That(hooked.Weight, Is.EqualTo(k_Weight).Within(1e-9), "크기 표 범위 안 무게");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Reel), "줄다리기 동안 버튼은 감기");
        }

        [Test]
        public void Bite_WhenNotStruck_IsMissed()
        {
            Create();
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            Cast();
            Run(BiteWait + Config.BiteWindow + 0.1);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(ended, Is.EqualTo(FishingEnd.Missed));
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems), "재료는 그대로");
        }

        // 놓친 직후의 버튼은 던지기가 아니다(늦은 낚아채기가 곧바로 다시 던지지 않게). 잠깐 선 뒤부터 다시 던진다
        [Test]
        public void Missed_ThenButtonRightAway_DoesNotCastUntilHoldEnds()
        {
            Create();
            Cast();
            Run(BiteWait + Config.BiteWindow + 0.1);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(m_fishing.TargetAction, Is.Null, "잠깐 서 있는 동안 버튼은 비어 있다");
            Assert.That(m_fishing.TryInteract(), Is.False);
            Run(0.6);
            Press(ActionTable.k_Cast);
        }

        [Test]
        public void Tug_HoldingOnlyWhileResting_LandsFish()
        {
            Create();
            Fish landed = null;
            m_bus.Subscribe<Events.FishLanded>(e => landed = e.Fish);
            Hook();
            double seconds = Tug(false);
            Assert.That(landed, Is.Not.Null, "낚았다");
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems + 1), "창고에 한 마리");
            Assert.That(m_fishing.Log["crucian"].Caught, Is.EqualTo(1));
            Assert.That(m_fishing.Log["crucian"].Best, Is.EqualTo(k_Weight).Within(1e-9));
            Assert.That(seconds, Is.GreaterThan(Config.RestSeconds).And.LessThan(20d), "쉼 두 번 안쪽에 낚인다");
            Assert.That(m_mall.Wombat.Busy, Is.True, "물고기를 들고 만세");
        }

        [Test]
        public void Tug_HoldingThroughRun_SnapsLine()
        {
            Create();
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            Hook();
            Tug(true);
            Assert.That(ended, Is.EqualTo(FishingEnd.Snapped), "달릴 때 계속 감으면 끊긴다");
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems), "재료 없음");
            Assert.That(m_fishing.Fish, Is.Null);
        }

        [Test]
        public void Tug_ReleasingDuringRun_GivesLineBack_AndTensionFalls()
        {
            Create();
            Hook();
            m_mall.Wombat.SetHolding(true);
            Run(Config.RestSeconds - k_Dt);
            double reeled = m_fishing.Distance;
            Assert.That(reeled, Is.LessThan(m_fishing.DistanceStart), "쉴 때 감았다");
            Run(k_Dt * 2);
            Assert.That(m_fishing.Running, Is.True, "달린다");
            Run(0.3);
            Assert.That(m_fishing.Tension, Is.GreaterThan(0d), "달릴 때 누르면 긴장");
            m_mall.Wombat.SetHolding(false);
            Run(0.5);
            Assert.That(m_fishing.Tension, Is.LessThan(0.3 * Config.TensionRise), "놓으면 긴장이 내린다");
            Assert.That(m_fishing.Distance, Is.GreaterThan(reeled), "놓으면 물고기가 줄을 되찾는다");
            Assert.That(m_fishing.Distance, Is.LessThanOrEqualTo(m_fishing.DistanceStart));
        }

        [Test]
        public void Joystick_WhileWaiting_RetrievesLine()
        {
            Create();
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            Cast();
            m_mall.Wombat.SetInput(new Vector2(1f, 0f));
            Run(k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(ended, Is.EqualTo(FishingEnd.Retrieved));
            Vector2 at = m_mall.Wombat.Mover.Position;
            Run(k_Dt);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Assert.That(m_mall.Wombat.Mover.Position, Is.Not.EqualTo(at), "거둔 즉시 걷는다");
        }

        [Test]
        public void Strike_WhileWaiting_RetrievesLine()
        {
            Create();
            Cast();
            Press(ActionTable.k_Strike);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Cast), "다시 던질 수 있다");
        }

        [Test]
        public void Wombat_StaysPutFacingTheWell_WhileFishing()
        {
            Create();
            Hook();
            Vector2 at = m_mall.Wombat.Mover.Position;
            m_mall.Wombat.SetInput(new Vector2(1f, 0f));
            Run(0.5);
            Assert.That(m_mall.Wombat.Mover.Position, Is.EqualTo(at), "줄다리기 중에는 걷지 않는다");
            Assert.That(m_mall.Wombat.Mover.Facing, Is.EqualTo(Facing.Down), "물을 본다");
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Tug));
        }

        [Test]
        public void Leaving_RetrievesLine()
        {
            Create();
            Cast();
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));
            Run(k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(m_mall.Active, Is.SameAs(m_plaza));
        }
        // 가짜 입질: 난수 0.6이면 한 번(물기까지의 66% 지점에서 찌만 까딱)
        [Test]
        public void Waiting_NibblesBeforeTheBite()
        {
            Create(0.6);
            double wait = BiteWaitFor(0.6);
            int nibbles = 0;
            m_bus.Subscribe<Events.Nibbled>(_ => nibbles++);
            Cast();
            Run(wait * 0.5);
            Assert.That(nibbles, Is.EqualTo(0), "아직");
            Run(wait * 0.25);
            Assert.That(nibbles, Is.EqualTo(1), "가짜 입질 한 번");
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Waiting), "아직 물지 않았다");
            Run(wait * 0.3);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Bite));
        }

        [Test]
        public void Joystick_DuringBite_RetrievesLine()
        {
            Create();
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            Cast();
            Run(BiteWait + k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Bite));
            m_mall.Wombat.SetInput(new Vector2(0f, 1f));
            Run(k_Dt);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Assert.That(ended, Is.EqualTo(FishingEnd.Retrieved));
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
        }

        // 저장은 어종 기록만: 낚은 뒤 저장 · 새 게임에 불러오면 기록이 남고 던진 줄은 없다
        [Test]
        public void Save_KeepsTheCatchLog()
        {
            Create();
            Hook();
            Tug(false);
            Assert.That(m_fishing.Log["crucian"].Caught, Is.EqualTo(1));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(GameSave.Capture(m_state, m_mall));
            Create();
            GameSave.Apply(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json), m_state, m_mall, m_tables);
            Assert.That(m_fishing.Log["crucian"].Caught, Is.EqualTo(1));
            Assert.That(m_fishing.Log["crucian"].Best, Is.EqualTo(k_Weight).Within(1e-9));
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle), "던진 줄은 저장하지 않는다");
        }

        // ---------- 설계 58 층계 웅덩이 ----------

        private FishingStretchTable Stretch(int index)
        {
            return m_tables.GetAll<FishingStretchTable>().First(stretch => stretch.Index == index);
        }

        private float ColumnWidth => m_fishing.Layout.ColumnWidth;

        // 설계 60: 비버 앞에 서면 비버가 대상 · 버튼은 시트 열기
        private BeaverInteractable AtBeaver(int stretch)
        {
            BeaverInteractable beaver = m_fishing.Things.OfType<BeaverInteractable>().Single(b => b.Index == stretch);
            m_mall.Wombat.Mover.Place(beaver.Position + new Vector2(0f, -0.3f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(beaver), "비버가 대상");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Open), "버튼은 시트 열기");
            return beaver;
        }

        // 바위 앞에 서서 바위 시트의 뚫기 줄을 고른다
        private void BreakRock()
        {
            RockInteractable rock = m_fishing.Things.OfType<RockInteractable>().Single();
            m_mall.Wombat.Mover.Place(rock.Position + new Vector2(0f, -0.3f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(rock), "바위가 대상");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_OpenDig), "버튼은 시트 열기");
            Assert.That(m_fishing.TryChoose(ActionTable.k_BreakRock, rock, null), Is.True, "뚫기");
            Run(m_mall.Wombat.DigSeconds + k_Dt);
        }

        // 집 층: 물 startCols열 × waterRows줄, 물은 못 걷고 둑은 걷는다. 물 오른쪽 둑에 비버
        [Test]
        public void Home_StartsSmall_WaterIsNotWalkable()
        {
            Create();
            FishingLayout layout = m_fishing.Layout;
            Assert.That(m_fishing.Opened, Is.EquivalentTo(new[] { 0 }));
            Assert.That(m_fishing.Waters[0].Cells.Count, Is.EqualTo(Stretch(0).StartCols));
            NavRect water = m_fishing.Water.Rect;
            Assert.That(water.XMax - water.XMin, Is.EqualTo(Stretch(0).StartCols * ColumnWidth).Within(1e-4f), "물 폭");
            Assert.That(water.YMax - water.YMin, Is.EqualTo(Config.WaterRows * layout.Cells.CellHeight).Within(1e-4f), "물 깊이(아래 층이 닫혀 폭포 없음)");
            Assert.That(layout.Nav.IsWalkable(new Vector2(layout.CenterX, water.YMax + 0.6f)), Is.True, "물 위 둑");
            Assert.That(layout.Nav.IsWalkable(new Vector2(layout.CenterX, water.YMax - 1f)), Is.False, "물은 못 걷는다");
            Assert.That((water.XMin + water.XMax) * 0.5f, Is.EqualTo(layout.CenterX).Within(1e-4f), "물은 가운데");
            Assert.That(layout.Nav.IsWalkable(new Vector2(water.XMin - ColumnWidth * 0.5f, water.YMax - 1f)), Is.True, "물 옆 둑 칸");
            BeaverInteractable beaver = m_fishing.Things.OfType<BeaverInteractable>().Single();
            Assert.That(beaver.Index, Is.EqualTo(0), "집 층 비버");
            Assert.That(beaver.Position.X, Is.GreaterThan(water.XMax), "물 오른쪽");
            Assert.That(layout.Nav.IsWalkable(beaver.Position), Is.True, "둑 위");
            Assert.That(beaver.NextWiden.Count, Is.EqualTo(Stretch(0).Feeds[0].Count));
            Assert.That(beaver.NextDamFeed, Is.Null, "다 넓히기 전엔 댐 헐기 없음");
            Assert.That(m_fishing.Things.OfType<RockInteractable>().Single().Stretch.Index, Is.EqualTo(-1), "위 층 바위");
            Assert.That(m_fishing.Things.OfType<DamInteractable>().Single().Stretch.Index, Is.EqualTo(1), "아래 층 댐");
            Assert.That(layout.Nav.IsWalkable(layout.HoleFloor), Is.True, "구멍 앞");
        }

        // 설계 60: 비버에게 물고기를 주면(시트 줄) 물고기가 빠지고 물이 한 열 넓어진다(가운데에 가까운 왼쪽부터). 난수 0.3이면 물속 발견은 반짝돌(코인 25% 다음 12%). 다음 먹이는 표의 다음 칸
        [Test]
        public void Feed_WidensWaterByOneColumn_SpendsFish_AndFinds()
        {
            Create();
            Events.WaterWidened? widened = null;
            m_bus.Subscribe<Events.WaterWidened>(e => widened = e);
            BeaverInteractable beaver = AtBeaver(0);
            FishingFeedData feed = Stretch(0).Feeds[0];
            int fish = m_state.Count(feed.Item);
            double coins = m_state.Coins;
            int gems = m_state.Count("gem");
            float right = beaver.Position.X;
            Assert.That(m_fishing.SheetActions(beaver).Select(a => a.Table.Id), Is.EquivalentTo(new[] { ActionTable.k_FeedWiden, ActionTable.k_FeedDam }));
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedWiden, beaver, null), Is.True, "먹이");
            Assert.That(m_state.Count(feed.Item), Is.EqualTo(fish - feed.Count), "물고기가 빠진다");
            Assert.That(m_state.Coins, Is.EqualTo(coins), "코인은 그대로");
            Assert.That(m_fishing.Waters[0].Cells.Count, Is.EqualTo(Stretch(0).StartCols + 1));
            Assert.That(m_fishing.Water.Rect.XMin, Is.EqualTo(-2 * ColumnWidth).Within(1e-4f), "왼쪽으로 한 열");
            Assert.That(beaver.NextWiden, Is.SameAs(Stretch(0).Feeds[1]), "다음 먹이");
            Assert.That(beaver.Position.X, Is.EqualTo(right).Within(1e-4f), "물이 왼쪽으로 늘면 비버는 그대로");
            Assert.That(m_fishing.Layout.Nav.IsWalkable(m_mall.Wombat.Mover.Position), Is.True);
            Assert.That(widened?.Col, Is.EqualTo(-2));
            Assert.That(widened?.Find?.Id, Is.EqualTo("gem"));
            Assert.That(m_state.Count("gem"), Is.EqualTo(gems + 1), "반짝돌 하나");

            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedWiden, beaver, null), Is.True);
            Assert.That(m_fishing.Water.Rect.XMax, Is.EqualTo(3 * ColumnWidth).Within(1e-4f), "다음은 오른쪽");
            Assert.That(beaver.Position.X, Is.EqualTo(right + ColumnWidth).Within(1e-4f), "비버가 따라간다");
        }

        // 물고기가 모자라면 못 준다(Poor). 발견 확률이 1이면 늘 나온다(코인은 표의 고정값)
        [Test]
        public void Feed_NeedsFish_FindCoinsWhenChanceIsCertain()
        {
            Create();
            m_tables.Get<FishingFindTable>("coins").Chance = 1d;
            m_tables.Get<FishingFindTable>("gem").Chance = 0d;
            Events.WaterWidened? widened = null;
            m_bus.Subscribe<Events.WaterWidened>(e => widened = e);
            BeaverInteractable beaver = AtBeaver(0);
            FishingFeedData feed = Stretch(0).Feeds[0];
            m_state.TrySpendItem(feed.Item, m_state.Count(feed.Item) - feed.Count + 1);
            Assert.That(m_fishing.SheetActions(beaver).Single(a => a.Table.Id == ActionTable.k_FeedWiden).Options(m_mall.Wombat.Worker, beaver)[0].State, Is.EqualTo(SheetOptionState.Lacking));
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedWiden, beaver, null), Is.False, "물고기가 모자라다");
            m_state.AddItem(feed.Item, 1);
            double coins = m_state.Coins;
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedWiden, beaver, null), Is.True);
            double found = m_tables.Get<FishingFindTable>("coins").Coins;
            Assert.That(found, Is.GreaterThan(0d), "표에 코인 값이 있다");
            Assert.That(widened?.Coins, Is.EqualTo(found));
            Assert.That(m_state.Coins, Is.EqualTo(coins + found));
        }

        // 물은 maxCols까지만: 다 넓히면 둑 갉기 줄이 닫히고(먹이 없음) 댐 헐기가 열린다
        [Test]
        public void Feed_StopsAtMaxCols_ThenDamFeedOpens()
        {
            Create();
            BeaverInteractable beaver = m_fishing.Things.OfType<BeaverInteractable>().Single();

            for (int i = Stretch(0).StartCols; i < Stretch(0).MaxCols; i++)
            {
                Assert.That(beaver.NextDamFeed, Is.Null, "넓히는 동안은 댐 헐기 없음");
                Assert.That(m_fishing.WidenFree(), Is.True);
            }

            Assert.That(m_fishing.Waters[0].Cells.Count, Is.EqualTo(Stretch(0).MaxCols));
            Assert.That(beaver.CanWiden, Is.False);
            Assert.That(beaver.NextWiden, Is.Null);
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedWiden, beaver, null), Is.False, "더 넓힐 둑이 없다");
            Assert.That(m_fishing.WidenFree(), Is.False);
            Assert.That(beaver.NextDamFeed, Is.SameAs(Stretch(1).DamFeeds[0]), "댐 헐기 먹이");
        }

        // 바위를 뚫으면 위 층이 제 시작 크기의 물로 열리고, 위 층 물 아랫부분은 폭포(던지는 네모에서 뺀다)
        [Test]
        public void BreakRock_OpensTheUpperTerrace()
        {
            Create();
            int opened = 0;
            m_bus.Subscribe<Events.StretchOpened>(e => opened += e.Stretch == -1 ? 1 : 0);
            double coins = m_state.Coins;
            BreakRock();
            Assert.That(m_state.Coins, Is.EqualTo(coins - Stretch(-1).RockCost), "바위 값");
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(m_fishing.Opened, Is.EquivalentTo(new[] { -1, 0 }));
            Assert.That(m_fishing.Waters[-1].Cells.Count, Is.EqualTo(Stretch(-1).StartCols));
            FishingLayout layout = m_fishing.Layout;
            Assert.That(layout.WaterOf(-1).YMin, Is.EqualTo(layout.WaterBottomOf(-1) + FishingLayout.k_Cliff).Within(1e-4f), "위 층 물 아래는 폭포");
            Assert.That(layout.WaterOf(0).YMin, Is.EqualTo(layout.WaterBottomOf(0)).Within(1e-4f), "집 층 아래는 아직 땅");
            Assert.That(layout.Nav.IsWalkable(new Vector2(layout.CenterX, layout.WaterTopOf(-1) + 0.6f)), Is.True, "위 층 둑");
            Assert.That(layout.Nav.IsWalkable(new Vector2(layout.WaterOf(0).XMin - ColumnWidth * 0.5f, layout.WaterTopOf(0) - 1f)), Is.True, "옆 둑으로 오르내린다");
            Assert.That(m_fishing.Things.OfType<RockInteractable>(), Is.Empty, "표에 더 위 층이 없다");
            Assert.That(m_fishing.Seats.Count(s => s.Stretch.Index == -1), Is.EqualTo(Stretch(-1).Seats.Length), "위 층 좌대 자리");
        }

        // 설계 60: 물을 다 넓힌 뒤 비버에게 댐 먹이를 조각 수만큼 주면 댐이 무너져 아래 층이 열린다. 낚싯대로 잔해를 끌어내도 조각은 줄지 않는다(코인만)
        [Test]
        public void Dam_FeedingEveryPiece_OpensTheLowerTerrace_PullingDebrisKeepsPieces()
        {
            Create();
            int broken = 0;
            int pulled = 0;
            m_bus.Subscribe<Events.DamBroken>(_ => broken++);
            m_bus.Subscribe<Events.DebrisPulled>(_ => pulled++);
            DamInteractable dam = m_fishing.Things.OfType<DamInteractable>().Single();
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.Layout.CenterX, m_fishing.Layout.BankY + 0.4f));
            double coins = m_state.Coins;
            Run(0.5);
            Assert.That(m_fishing.Target, Is.InstanceOf<DamInteractable>(), "댐이 물보다 먼저 대상");
            Press(ActionTable.k_PullDebris);
            Assert.That(m_fishing.Debris, Is.Not.Null, "잔해에 줄을 걸었다");
            Tug(true);
            Assert.That(pulled, Is.EqualTo(1), "잔해 한 조각");
            Assert.That(m_state.Coins, Is.EqualTo(coins + Config.DebrisCoins), "잔해는 코인");
            Assert.That(dam.PiecesLeft, Is.EqualTo(Stretch(1).DamPieces), "잔해로는 댐 조각이 줄지 않는다");

            while (m_fishing.WidenFree())
            {
            }

            BeaverInteractable beaver = AtBeaver(0);

            for (int i = 0; i < Stretch(1).DamPieces; i++)
            {
                FishingFeedData feed = Stretch(1).DamFeeds[i];
                Assert.That(beaver.NextDamFeed, Is.SameAs(feed));
                int fish = m_state.Count(feed.Item);
                Assert.That(m_fishing.TryChoose(ActionTable.k_FeedDam, beaver, null), Is.True, "댐 먹이");
                Assert.That(m_state.Count(feed.Item), Is.EqualTo(fish - feed.Count));
                Assert.That(broken, Is.EqualTo(i == Stretch(1).DamPieces - 1 ? 1 : 0));
            }

            Assert.That(broken, Is.EqualTo(1), "댐이 무너졌다");
            Assert.That(m_fishing.Opened, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(m_fishing.Waters[1].Cells.Count, Is.EqualTo(Stretch(1).StartCols));
            Assert.That(m_fishing.Things.OfType<DamInteractable>(), Is.Empty);
            Assert.That(beaver.Dam, Is.Null);
            Assert.That(beaver.NextDamFeed, Is.Null, "더 헐 댐이 없다");
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedDam, beaver, null), Is.False);
            Assert.That(m_fishing.Things.OfType<BeaverInteractable>().Select(b => b.Index), Is.EquivalentTo(new[] { 0, 1 }), "아래 층에도 비버");
            FishingLayout layout = m_fishing.Layout;
            Assert.That(layout.WaterOf(0).YMin, Is.EqualTo(layout.WaterBottomOf(0) + FishingLayout.k_Cliff).Within(1e-4f), "집 층 물 아래는 폭포");
            Assert.That(layout.Nav.IsWalkable(new Vector2(layout.CenterX, layout.WaterTopOf(1) + 0.6f)), Is.True, "아래 층 둑");
            Assert.That(m_fishing.Seats.Count(s => s.Stretch.Index == 1), Is.EqualTo(Stretch(1).Seats.Length), "아래 층 좌대 자리");
        }

        // 잔해에 줄을 건 채 조이스틱을 움직이면 줄을 놓고 바로 걷는다(조각은 그대로)
        [Test]
        public void Joystick_WhilePullingDebris_LetsGo()
        {
            Create();
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.Layout.CenterX, m_fishing.Layout.BankY + 0.4f));
            Run(0.5);
            Press(ActionTable.k_PullDebris);
            Run(k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Tug));
            m_mall.Wombat.SetInput(new Vector2(-1f, 0f));
            Run(k_Dt);
            Assert.That(ended, Is.EqualTo(FishingEnd.Retrieved));
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle));
            Assert.That(m_fishing.Debris, Is.Null);
            Vector2 at = m_mall.Wombat.Mover.Position;
            Run(k_Dt);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Assert.That(m_mall.Wombat.Mover.Position, Is.Not.EqualTo(at), "놓은 즉시 걷는다");
            Assert.That(m_fishing.PulledFrom(1), Is.EqualTo(0), "조각은 그대로");
        }

        // 위 층 둑에서 던지면 그 층 물고기만(상류는 메기만) · 무게는 그 크기 범위
        [Test]
        public void Cast_InTheUpperTerrace_HooksItsFish()
        {
            Create();
            Assert.That(m_fishing.OpenNextFree(), Is.True);
            Fish hooked = null;
            m_bus.Subscribe<Events.Hooked>(e => hooked = e.Fish);
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.Layout.CenterX, m_fishing.Layout.WaterTopOf(-1) + 0.4f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.InstanceOf<WaterInteractable>());
            Assert.That(((WaterInteractable)m_fishing.Target).Index, Is.EqualTo(-1), "위 층 물");
            Press(ActionTable.k_Cast);
            Run(BiteWait + k_Dt);
            Press(ActionTable.k_Strike);
            Assert.That(hooked.Kind.Id, Is.EqualTo("catfish"));
            FishSizeData small = hooked.Kind.Sizes[0];
            Assert.That(hooked.Weight, Is.InRange(small.Min, small.Max));
        }

        // 광장 낚시터 문은 젖은 벽(횟집 문과 같은 시트)이 막고 있다: 값을 내고 파면 통로가 열린다
        [Test]
        public void WetWall_GatesThePlazaDoor_UntilDug()
        {
            Create();
            m_bus.Publish(new Events.Passed(m_fishing, PlazaArea.k_Id));
            ShopGateInteractable wall = m_plaza.Gates.Single(gate => gate.Shop == (IGated)m_fishing);
            PassageInteractable door = m_plaza.Things.OfType<PassageInteractable>().Single(p => p.To == FishingArea.k_Id);
            Assert.That(wall.Table.Id, Is.EqualTo(ShopGateInteractable.k_WetWall));
            Assert.That(door.IsOpen, Is.False, "닫힌 통로");
            Assert.That(GoalCounter.IsAreaOpen(FishingArea.k_Id, m_mall), Is.False);
            int opened = 0;
            m_bus.Subscribe<Events.ShopOpened>(e => opened += e.Shop == m_fishing ? 1 : 0);
            double coins = m_state.Coins;
            Assert.That(m_plaza.TryChoose(ActionTable.k_OpenShop, wall, null), Is.True, "젖은 벽 파기");
            Run(k_Dt);
            Assert.That(m_state.Coins, Is.EqualTo(coins - Config.WallCost));
            Assert.That(m_fishing.GateOpen, Is.True);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.True, "통로가 열렸다");
            Assert.That(m_plaza.Gates.Any(gate => gate.Shop == (IGated)m_fishing), Is.False, "벽 사물이 빠진다");
            Assert.That(GoalCounter.IsAreaOpen(FishingArea.k_Id, m_mall), Is.True);
            Assert.That(m_plaza.TryChoose(ActionTable.k_OpenShop, wall, null), Is.False, "두 번은 없다");
        }

        // 좌대를 놓으면 낚시터가 손님을 받는 가게가 되고, 손님은 좌대에 앉아 간격마다 한 마리(코인) 낚고 나가 광장으로
        [Test]
        public void Seat_Placed_CustomerSitsFishesPaysAndLeaves()
        {
            Create();
            Assert.That(m_fishing.OpenGateFree(), Is.True, "젖은 벽이 열려야 가게");
            SeatInteractable seat = m_fishing.Seats.First(s => s.Stretch.Index == 0);
            m_mall.Wombat.Mover.Place(seat.Position);
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(seat), "좌대 자리가 대상");
            Assert.That(m_fishing.IsOpen, Is.False);
            double coins = m_state.Coins;
            Assert.That(m_fishing.TryChoose(ActionTable.k_PlaceSeat, seat, null), Is.True, "좌대 놓기");
            Assert.That(m_state.Coins, Is.EqualTo(coins - seat.Row.Cost));
            Assert.That(m_fishing.IsOpen, Is.True, "손님을 받는다");
            Assert.That(m_plaza.Shops, Does.Contain(m_fishing), "광장이 손님을 보내는 가게");
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.Layout.CenterX + 4.5f, m_fishing.Layout.BankY + 1.8f));

            m_fishing.Admit(m_tables.GetAll<VisitorTable>().First(v => v.Role == VisitorRole.Customer));
            FishingVisitor visitor = m_fishing.Visitors.Single();
            Assert.That(visitor.Seat, Is.SameAs(seat), "빈 좌대를 잡았다");
            Assert.That(m_fishing.CanAdmit, Is.False, "좌대 하나 = 손님 하나");
            int caught = 0;
            double paid = 0d;
            bool left = false;
            m_bus.Subscribe<Events.FishingVisitorCaught>(e =>
            {
                if (e.Visitor == visitor)
                {
                    caught++;
                    paid += e.Coins;
                }
            });
            m_bus.Subscribe<Events.FishingVisitorLeft>(e => left |= e.Visitor == visitor);

            bool stoodBehind = false;
            float maxLift = 0f;

            for (double t = 0d; t < 20d && !visitor.Seated; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
                // 다가서는 마지막 틱에 바로 뛰어오르니 한 틱 걸음(0.15) 안이면 뒤에 선 것으로
                stoodBehind |= !visitor.OnSeat && Vector2.Distance(visitor.Position, seat.Behind) < 0.15f;
                maxLift = Math.Max(maxLift, visitor.Lift);
            }

            Assert.That(visitor.Seated, Is.True, "좌대에 앉았다");
            Assert.That(stoodBehind, Is.True, "좌대 뒤에 섰다가");
            Assert.That(maxLift, Is.GreaterThan(SeatInteractable.k_Top), "깡충 뛰어");
            Assert.That(visitor.OnSeat, Is.True);
            Assert.That(visitor.Position, Is.EqualTo(seat.Position), "발끝은 좌대 자리");
            Assert.That(visitor.Lift, Is.EqualTo(SeatInteractable.k_Top).Within(1e-4f), "윗판 높이에 앉았다");

            for (double t = 0d; t < Config.CustomerSecondsMax + 20d && !left; t += k_Dt)
            {
                m_mall.Tick(k_Dt);

                if (visitor.Phase == VisitorPhase.Leaving && visitor.Moving)
                {
                    Assert.That(visitor.OnSeat, Is.False, "깡충 내려와서 걷는다");
                    Assert.That(visitor.Lift, Is.EqualTo(0f).Within(1e-4f));
                }
            }

            Assert.That(left, Is.True, "다 낚고 나갔다");
            double seconds = Config.CustomerSecondsMin + 0.3 * (Config.CustomerSecondsMax - Config.CustomerSecondsMin);
            int expected = (int)Math.Floor(seconds / Config.CustomerCatchEvery);
            Assert.That(caught, Is.EqualTo(expected), "간격마다 한 마리");
            Assert.That(paid, Is.EqualTo(expected * seat.Stretch.Pay));
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems), "손님 물고기는 창고에 안 든다");
        }

        // 저장: 젖은 벽 · 열린 층 · 넓힌 물(값 성장까지) · 뺀 잔해 · 놓은 좌대가 남는다
        [Test]
        public void Save_KeepsGateTerracesWaterDebrisAndSeats()
        {
            Create();
            Assert.That(m_fishing.OpenGateFree(), Is.True);
            while (m_fishing.WidenFree())
            {
            }

            Assert.That(m_fishing.OpenNextFree(), Is.True, "위 층(집 층을 다 넓힌 뒤라 위 층은 시작 크기)");

            BeaverInteractable beaver = AtBeaver(0);
            Assert.That(m_fishing.TryChoose(ActionTable.k_FeedDam, beaver, null), Is.True, "댐 한 조각");
            Assert.That(m_fishing.PulledFrom(1), Is.EqualTo(1));
            Assert.That(m_fishing.PlaceSeatFree(), Is.True);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(GameSave.Capture(m_state, m_mall));
            Create();
            int widened = 0;
            m_bus.Subscribe<Events.WaterWidened>(_ => widened++);
            GameSave.Apply(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json), m_state, m_mall, m_tables);
            Run(k_Dt);
            Assert.That(m_fishing.GateOpen, Is.True);
            Assert.That(m_fishing.Opened, Is.EquivalentTo(new[] { -1, 0 }));
            Assert.That(m_fishing.Waters[0].Cells.Count, Is.EqualTo(Stretch(0).MaxCols));
            Assert.That(m_fishing.Things.OfType<BeaverInteractable>().Single(b => b.Index == 0).NextDamFeed, Is.SameAs(Stretch(1).DamFeeds[1]), "댐 먹이 차례도 이어진다");
            Assert.That(m_fishing.Waters[-1].Cells.Count, Is.EqualTo(Stretch(-1).StartCols));
            Assert.That(widened, Is.EqualTo(0), "되살릴 때는 발견 · 사건이 없다");
            Assert.That(m_fishing.PulledFrom(1), Is.EqualTo(1));
            Assert.That(m_fishing.Things.OfType<DamInteractable>().Single().PiecesLeft, Is.EqualTo(Stretch(1).DamPieces - 1));
            Assert.That(m_fishing.Seats.Count(s => s.Placed), Is.EqualTo(1));
            Assert.That(m_fishing.IsOpen, Is.True);
            Assert.That(m_plaza.Gates.Any(gate => gate.Shop == (IGated)m_fishing), Is.False, "열린 저장이면 젖은 벽이 없다");
        }
    }
}
