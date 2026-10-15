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
    // 설계 53 검증: 바위 뚫기(시트 줄) · 댐 잔해 끌어내기(버튼 + 줄다리기) · 물이 찬 구간의 물고기 · 좌대 손님 · 저장
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

        // 갈대섬 뭍 아랫변(물 바로 위)에 선다
        private void AtWater()
        {
            m_mall.Wombat.Mover.Place(new Vector2(0f, m_fishing.Layout.LandOf(0).YMin + 0.4f));
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

        // ---------- 설계 61 지하 호수의 섬들 ----------

        private FishingIslandTable Island(int index)
        {
            return m_tables.GetAll<FishingIslandTable>().First(island => island.Index == index);
        }

        private static Vector2 Center(NavRect rect)
        {
            return new Vector2((rect.XMin + rect.XMax) / 2f, (rect.YMin + rect.YMax) / 2f);
        }

        // 앞 섬 뭍에서 다리 자리 곁에 서서 다리 시트의 놓기 줄을 고른다
        private void PlaceBridge(int island)
        {
            NavRect bridge = m_fishing.Layout.BridgeOf(island);
            NavRect from = m_fishing.Layout.LandOf(island - 1);
            Vector2 center = Center(bridge);
            m_mall.Wombat.Mover.Place(new Vector2(Math.Clamp(center.X, from.XMin + 0.4f, from.XMax - 0.4f), Math.Clamp(center.Y, from.YMin + 0.4f, from.YMax - 0.4f)));
            Run(k_Dt);
            BridgeInteractable target = m_fishing.Target as BridgeInteractable;
            Assert.That(target, Is.Not.Null, "다리 자리가 대상");
            Assert.That(target.Island.Index, Is.EqualTo(island));
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Open), "버튼은 시트 열기");
            Assert.That(m_fishing.TryChoose(ActionTable.k_PlaceBridge, target, null), Is.True, "다리 놓기");
            Run(m_mall.Wombat.DigSeconds + k_Dt);
        }

        private void HookAt(Vector2 at, out Fish hooked)
        {
            Fish got = null;
            IDisposable sub = m_bus.Subscribe<Events.Hooked>(e => got = e.Fish);
            m_mall.Wombat.Mover.Place(at);
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(m_fishing.Water), "물가가 대상");
            Press(ActionTable.k_Cast);
            Run(BiteWait + k_Dt);
            Press(ActionTable.k_Strike);
            sub.Dispose();
            hooked = got;
        }

        // 처음엔 갈대섬 + 보이기만 하는 바위섬. 다리를 놓으면 값이 빠지고 바위섬 · 다리를 걷고 유목섬이 보인다
        [Test]
        public void Bridge_Placed_ReachesIsland_AndRevealsNext()
        {
            Create();
            Assert.That(m_fishing.Reached, Is.EquivalentTo(new[] { 0 }));
            Assert.That(m_fishing.Visible, Is.EquivalentTo(new[] { 0, 1 }), "처음엔 갈대섬 + 보이는 바위섬");
            Vector2 rock = Center(m_fishing.Layout.LandOf(1));
            Assert.That(m_fishing.Layout.Nav.IsWalkable(rock), Is.False, "닿기 전엔 못 걷는다");
            Assert.That(GoalCounter.Current(GoalCounter.k_Open, "rock_island", m_mall), Is.EqualTo(0), "퀘스트: 아직 안 닿음");
            Assert.That(m_fishing.Seats.All(seat => seat.Island.Index == 0), Is.True, "좌대 자리는 닿은 섬에만");
            double coins = m_state.Coins;
            int reached = 0;
            m_bus.Subscribe<Events.IslandReached>(e => reached = e.Island);

            PlaceBridge(1);

            Assert.That(reached, Is.EqualTo(1));
            Assert.That(m_state.Coins, Is.EqualTo(coins - Island(1).Bridge.Cost), "다리 값");
            Assert.That(m_fishing.Reached, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(m_fishing.Visible, Is.EquivalentTo(new[] { 0, 1, 2 }), "유목섬이 보인다");
            Assert.That(m_fishing.Layout.Nav.IsWalkable(rock), Is.True, "바위섬을 걷는다");
            Assert.That(m_fishing.Layout.Nav.IsWalkable(Center(m_fishing.Layout.BridgeOf(1))), Is.True, "다리를 걷는다");
            Assert.That(m_fishing.Layout.Nav.IsWalkable(Center(m_fishing.Layout.LandOf(2))), Is.False, "유목섬은 보이기만");
            Assert.That(m_fishing.Layout.Nav.FindPath(m_fishing.Layout.HoleFloor, rock), Is.Not.Empty, "구멍에서 바위섬까지 길");
            Assert.That(m_fishing.Things.OfType<BridgeInteractable>().Single().Island.Index, Is.EqualTo(2), "다음 다리 자리");
            Assert.That(m_fishing.Seats.Count(seat => seat.Island.Index == 1), Is.EqualTo(Island(1).Seats.Length), "닿은 섬에 좌대 자리");
            Assert.That(GoalCounter.Current(GoalCounter.k_Open, "rock_island", m_mall), Is.EqualTo(1), "퀘스트: 닿았다");
        }

        // 선 땅에 따라 어종 표가 바뀐다: 바위섬 뭍에서는 섬 물고기(메기 2 · 붕어 2 → 난수 0.3은 첫째), 다리 위에서는 다리 물고기(붕어 1 · 메기 2 → 첫째)
        [Test]
        public void Cast_OnIslandOrBridge_HooksThatPlacesFish()
        {
            Create();
            PlaceBridge(1);
            NavRect land = m_fishing.Layout.LandOf(1);
            HookAt(new Vector2((land.XMin + land.XMax) / 2f, land.YMin + 0.4f), out Fish onIsland);
            Assert.That(onIsland.Kind.Id, Is.EqualTo(Island(1).Fish[0].Id));
            Tug(false);
            Run(Config.LandSeconds + k_Dt);

            HookAt(Center(m_fishing.Layout.BridgeOf(1)), out Fish onBridge);
            Assert.That(onBridge.Kind.Id, Is.EqualTo(Island(1).Bridge.Fish[0].Id));
            Assert.That(onBridge.Kind.Id, Is.Not.EqualTo(onIsland.Kind.Id));
        }

        // 그림자: shadowEvery 뒤 바위섬 아래 물에 나타나 오가고, 그 위 둑에서 던지면 그림자가 들어가며 명물이 입질 없이 바로 문다. 낚으면 왕메기 재료
        [Test]
        public void Shadow_Shows_ThenCastNearIt_HooksTheBoss()
        {
            Create();
            PlaceBridge(1);
            bool shown = false;
            m_bus.Subscribe<Events.ShadowShown>(_ => shown = true);
            Run(Config.ShadowEvery + k_Dt);
            Assert.That(shown, Is.True);
            Assert.That(m_fishing.ShadowVisible, Is.True);
            NavRect land = m_fishing.Layout.LandOf(1);
            Assert.That(m_fishing.ShadowPosition.Y, Is.LessThan(land.YMin), "뭍 아래 물");
            float x = m_fishing.ShadowPosition.X;
            Run(1d);
            Assert.That(m_fishing.ShadowPosition.X, Is.Not.EqualTo(x), "헤엄친다");

            for (double t = 0d; t < Config.ShadowSeconds && Math.Abs(m_fishing.ShadowPosition.X - (land.XMin + land.XMax) / 2f) > 0.3f; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }

            Fish hooked = null;
            m_bus.Subscribe<Events.Hooked>(e => hooked = e.Fish);
            m_mall.Wombat.Mover.Place(new Vector2(m_fishing.ShadowPosition.X, land.YMin + 0.4f));
            Run(k_Dt);
            Press(ActionTable.k_Cast);
            Assert.That(m_fishing.ShadowVisible, Is.False, "그림자가 찌로 들어갔다");
            Run(Config.BiteWaitMin + k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Bite), "입질 없이 바로 문다");
            Press(ActionTable.k_Strike);
            Assert.That(hooked.Kind.Id, Is.EqualTo(Island(1).Boss));
            int before = m_state.Count(hooked.Kind.Item);
            Tug(false, 180d);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle), "낚았다");
            Assert.That(m_state.Count(hooked.Kind.Item), Is.EqualTo(before + 1), "왕메기 재료");
        }

        // 그림자가 보여도 멀리서 던지면 보통 물고기가 걸리고 그림자는 남는다
        [Test]
        public void Shadow_CastFarFromIt_HooksNormalFish_AndShadowStays()
        {
            Create();
            PlaceBridge(1);
            Assert.That(m_fishing.ShowShadowNow(), Is.True);
            Run(k_Dt);
            Assert.That(m_fishing.ShadowVisible, Is.True);
            NavRect shore = m_fishing.Layout.LandOf(0);
            HookAt(new Vector2(-2f, shore.YMin + 0.4f), out Fish hooked);
            Assert.That(hooked.Kind.Id, Is.Not.EqualTo(Island(1).Boss), "갈대섬 물고기");
            Assert.That(m_fishing.ShadowVisible, Is.True, "그림자는 그대로");
        }

        // 상자는 crateSeconds가 지나면 사라지고, 끄는 동안은 떠내려가지도 사라지지도 않는다
        [Test]
        public void Crate_FadesAfterSeconds_AndWaitsWhilePulled()
        {
            Create();
            PlaceBridge(1);
            PlaceBridge(2);
            Assert.That(m_fishing.SpawnCrateNow(), Is.True);
            Run(k_Dt);
            Assert.That(m_fishing.Crate, Is.Not.Null);
            Run(Config.CrateSeconds + k_Dt);
            Assert.That(m_fishing.Crate, Is.Null, "머무는 초가 지나면 사라진다");

            Assert.That(m_fishing.SpawnCrateNow(), Is.True);
            Run(k_Dt);
            CrateInteractable crate = m_fishing.Crate;
            NavRect land = m_fishing.Layout.LandOf(2);
            m_mall.Wombat.Mover.Place(new Vector2(land.XMax - 0.4f, land.YMin + 0.4f));
            Run(k_Dt);
            Press(ActionTable.k_PullDebris);
            Vector2 at = crate.Position;
            double left = crate.Left;
            Run(2d);
            Assert.That(crate.Position, Is.EqualTo(at), "끄는 동안 멈춘다");
            Assert.That(crate.Left, Is.EqualTo(left), "끄는 동안 시간이 안 간다");
            Assert.That(m_fishing.Crate, Is.SameAs(crate));
        }

        // 줄이 나가 있으면 상자가 둑 앞을 지나도 물이 대상이라 낚아채기 · 감기 단추가 남는다(리뷰 2026-10-14). 빈 줄이면 다시 상자가 먼저
        [Test]
        public void Crate_WhileLineIsOut_DoesNotStealTheTarget()
        {
            Create();
            PlaceBridge(1);
            PlaceBridge(2);
            NavRect land = m_fishing.Layout.LandOf(2);
            m_mall.Wombat.Mover.Place(new Vector2(land.XMax - 0.4f, land.YMin + 0.4f));
            Run(k_Dt);
            Press(ActionTable.k_Cast);
            Assert.That(m_fishing.SpawnCrateNow(), Is.True);
            Run(k_Dt);
            CrateInteractable crate = m_fishing.Crate;
            Assert.That(crate.DistanceTo(m_mall.Wombat.Mover.Position), Is.LessThanOrEqualTo((float)crate.Table.Range), "상자가 범위 안");
            Assert.That(m_fishing.Target, Is.SameAs(m_fishing.Water), "줄이 나가 있으면 물이 대상");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Strike));
            Run(BiteWait + k_Dt);
            Press(ActionTable.k_Strike);
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Reel));
            Tug(false);
            Run(Config.LandSeconds + k_Dt);
            // 그동안 상자는 왼쪽으로 떠내려갔다: 그 위 둑으로 따라가면 빈 줄이라 다시 상자가 먼저
            m_mall.Wombat.Mover.Place(new Vector2(Math.Clamp(crate.Position.X, land.XMin + 0.4f, land.XMax - 0.4f), land.YMin + 0.4f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(crate), "빈 줄이면 다시 상자가 먼저");
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_PullDebris));
        }

        // 상자: crateEvery 뒤 유목섬 오른쪽 아래에 떠내려와 왼쪽으로 가고, 둑에서 끌어내기(물보다 먼저 대상) → 줄다리기 → 코인, 상자는 사라진다
        [Test]
        public void Crate_Drifts_PullingItPaysCoins()
        {
            Create();
            PlaceBridge(1);
            PlaceBridge(2);
            Run(Config.CrateEvery + k_Dt);
            CrateInteractable crate = m_fishing.Crate;
            Assert.That(crate, Is.Not.Null, "상자가 떠내려왔다");
            Assert.That(m_fishing.Things, Does.Contain(crate));
            NavRect land = m_fishing.Layout.LandOf(2);
            float x = crate.Position.X;
            Run(1d);
            Assert.That(crate.Position.X, Is.LessThan(x), "왼쪽으로 떠내려간다");

            for (double t = 0d; t < Config.CrateSeconds && crate.Position.X > land.XMax - 0.6f; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }

            m_mall.Wombat.Mover.Place(new Vector2(crate.Position.X, land.YMin + 0.4f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(crate), "상자가 물보다 먼저 대상");
            double coins = m_state.Coins;
            int pulled = 0;
            m_bus.Subscribe<Events.CratePulled>(_ => pulled++);
            Press(ActionTable.k_PullDebris);
            Assert.That(m_fishing.Pulling, Is.SameAs(crate), "상자에 줄을 걸었다");
            Run(k_Dt);
            Assert.That(m_fishing.TargetAction?.Id, Is.EqualTo(ActionTable.k_Reel), "상자 줄다리기 동안 버튼은 감기");
            Tug(false);
            Assert.That(pulled, Is.EqualTo(1));
            Assert.That(m_state.Coins, Is.EqualTo(coins + Config.CrateCoins), "상자 코인");
            Assert.That(m_fishing.Crate, Is.Null, "상자는 사라진다");
            Assert.That(m_fishing.Things.OfType<CrateInteractable>(), Is.Empty);
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems), "상자는 재료가 아니다");
        }

        // 상자에 줄을 건 채 조이스틱을 움직이면 줄을 놓고 바로 걷는다(상자는 그대로)
        [Test]
        public void Joystick_WhilePullingCrate_LetsGo()
        {
            Create();
            PlaceBridge(1);
            PlaceBridge(2);
            Assert.That(m_fishing.SpawnCrateNow(), Is.True);
            Run(k_Dt);
            CrateInteractable crate = m_fishing.Crate;
            NavRect land = m_fishing.Layout.LandOf(2);
            FishingEnd? ended = null;
            m_bus.Subscribe<Events.FishingEnded>(e => ended = e.Reason);
            m_mall.Wombat.Mover.Place(new Vector2(land.XMax - 0.4f, land.YMin + 0.4f));
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(crate));
            Press(ActionTable.k_PullDebris);
            Run(k_Dt);
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Tug));
            m_mall.Wombat.SetInput(new Vector2(-1f, 0f));
            Run(k_Dt);
            Assert.That(ended, Is.EqualTo(FishingEnd.Retrieved));
            Assert.That(m_fishing.Pulling, Is.Null);
            Assert.That(m_fishing.Crate, Is.SameAs(crate), "상자는 그대로");
            Vector2 at = m_mall.Wombat.Mover.Position;
            Run(k_Dt);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Assert.That(m_mall.Wombat.Mover.Position, Is.Not.EqualTo(at), "놓은 즉시 걷는다");
        }

        // 좌대를 놓으면 낚시터가 손님을 받는 가게가 되고, 손님은 좌대에 앉아 간격마다 한 마리(코인) 낚고 나가 광장으로
        [Test]
        public void Seat_Placed_CustomerSitsFishesPaysAndLeaves()
        {
            Create();
            SeatInteractable seat = m_fishing.Seats.First(s => s.Island.Index == 0);
            m_mall.Wombat.Mover.Place(seat.Position);
            Run(k_Dt);
            Assert.That(m_fishing.Target, Is.SameAs(seat), "좌대 자리가 대상");
            Assert.That(m_fishing.IsOpen, Is.False);
            double coins = m_state.Coins;
            Assert.That(m_fishing.TryChoose(ActionTable.k_PlaceSeat, seat, null), Is.True, "좌대 놓기");
            Assert.That(m_state.Coins, Is.EqualTo(coins - seat.Row.Cost));
            Assert.That(m_fishing.IsOpen, Is.True, "손님을 받는다");
            Assert.That(m_plaza.Shops, Does.Contain(m_fishing), "광장이 손님을 보내는 가게");
            m_mall.Wombat.Mover.Place(new Vector2(2.5f, -3f));

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
                stoodBehind |= !visitor.OnSeat && Vector2.Distance(visitor.Position, seat.Behind) < 0.05f;
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
            Assert.That(paid, Is.EqualTo(expected * seat.Island.Pay));
            Assert.That(m_state.Count(k_Item), Is.EqualTo(TestTables.k_PlentyItems), "손님 물고기는 창고에 안 든다");
        }

        // 바위섬 좌대 손님은 다리를 건너가 앉는다
        [Test]
        public void Seat_OnIsland_CustomerCrossesTheBridge()
        {
            Create();
            PlaceBridge(1);
            SeatInteractable seat = m_fishing.Seats.First(s => s.Island.Index == 1);
            m_mall.Wombat.Mover.Place(seat.Position);
            Run(k_Dt);
            Assert.That(m_fishing.TryChoose(ActionTable.k_PlaceSeat, seat, null), Is.True, "바위섬 좌대 놓기");
            m_mall.Wombat.Mover.Place(new Vector2(2.5f, -3f));
            m_fishing.Admit(m_tables.GetAll<VisitorTable>().First(v => v.Role == VisitorRole.Customer));
            FishingVisitor visitor = m_fishing.Visitors.Single();
            Assert.That(visitor.Seat, Is.SameAs(seat));

            for (double t = 0d; t < 40d && !visitor.Seated; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }

            Assert.That(visitor.Angry, Is.False, "길이 있다");
            Assert.That(visitor.Seated, Is.True, "다리를 건너 바위섬 좌대에 앉았다");
            Assert.That(visitor.Position, Is.EqualTo(seat.Position));
        }

        // 저장: 닿은 섬 · 놓은 좌대가 남는다(보이는 섬 · 다음 다리 자리는 닿은 섬에서 다시 나온다)
        [Test]
        public void Save_KeepsReachedIslandsAndSeats()
        {
            Create();
            PlaceBridge(1);
            Assert.That(m_fishing.PlaceSeatFree(), Is.True);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(GameSave.Capture(m_state, m_mall));
            Create();
            GameSave.Apply(Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json), m_state, m_mall, m_tables);
            Assert.That(m_fishing.Reached, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(m_fishing.Visible, Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(m_fishing.Seats.Count(s => s.Placed), Is.EqualTo(1));
            Assert.That(m_fishing.IsOpen, Is.True);
            Assert.That(m_fishing.Things.OfType<BridgeInteractable>().Single().Island.Index, Is.EqualTo(2));
            Assert.That(m_fishing.Layout.Nav.IsWalkable(Center(m_fishing.Layout.LandOf(1))), Is.True);
        }
    }
}
