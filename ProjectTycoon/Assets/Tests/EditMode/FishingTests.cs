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
    // 낚시터 난수는 늘 0.3(물기까지 = min + 0.3 × (max − min), 가짜 입질 0번)
    public sealed class FishingTests
    {
        private const double k_Dt = 0.05;
        private const string k_Item = "fish_crucian";

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

        // 둑 위(물 바로 위)에 선다
        private void AtWater()
        {
            m_mall.Wombat.Mover.Place(new Vector2(0f, m_fishing.Water.Rect.YMax + 0.4f));
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
            Assert.That(hooked, Is.Not.Null.And.Property("Weight").EqualTo(Config.Weight));
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
            Assert.That(m_fishing.Log["crucian"].Best, Is.EqualTo(Config.Weight));
            Assert.That(seconds, Is.GreaterThan(Config.RestSeconds).And.LessThan(20d), "쉼 두 번 안쪽에 낚인다");
            Assert.That(m_mall.Wombat.Busy, Is.True, "털썩 앉아 있다");
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
            Assert.That(m_fishing.Log["crucian"].Best, Is.EqualTo(Config.Weight));
            Assert.That(m_fishing.Phase, Is.EqualTo(FishingPhase.Idle), "던진 줄은 저장하지 않는다");
        }
    }
}
