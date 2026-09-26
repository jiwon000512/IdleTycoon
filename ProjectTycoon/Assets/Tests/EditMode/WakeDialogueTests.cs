using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 22 검증 1~6: 딴짓 점원 = 사물·깨우기 버튼, 깨운 뒤 한 바퀴 건너뜀, 산책·수다, 대화 줄·끝, 말풍선 시간, 검증기
    public sealed class WakeDialogueTests
    {
        private const double k_Dt = 0.02;

        // 차례로 돌려주다가 다 쓰면 마지막 값을 계속(고정 난수)
        private sealed class ScriptRandom : IRandom
        {
            private readonly double[] m_values;
            private int m_index;

            public ScriptRandom(params double[] values)
            {
                m_values = values;
            }

            public double NextDouble()
            {
                return m_values[Math.Min(m_index++, m_values.Length - 1)];
            }
        }

        private EventBus m_bus;
        private ZooState m_state;
        private TableSet m_tables;

        private BakeryArea Create(IRandom random, Action<TableSet> edit = null)
        {
            m_tables = TestTables.Load();
            edit?.Invoke(m_tables);
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            BakeryArea shop = new BakeryArea(m_state, m_tables, random, new Wombat(m_tables, m_state), m_bus);
            shop.Wombat.Mover.Place(shop.Layout.HoleFloor);
            return shop;
        }

        private static bool RunUntil(BakeryArea shop, Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return true;
                }

                shop.Tick(k_Dt);
            }

            return done();
        }

        private static void Run(BakeryArea shop, double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                shop.Tick(k_Dt);
            }
        }

        private static Clerk Hire(BakeryArea shop, Interactable thing)
        {
            Candidate candidate = shop.Candidates[0];
            Assert.That(shop.TryHire(candidate, thing, shop.WageFor(candidate, thing)), Is.True);
            return shop.ClerkOf(thing);
        }

        private static ClerkInteractable ThingOf(BakeryArea shop, Clerk clerk)
        {
            foreach (Interactable thing in shop.Things)
            {
                if (thing is ClerkInteractable wrapped && wrapped.Clerk == clerk)
                {
                    return wrapped;
                }
            }

            return null;
        }

        // 난수 전부 0(일머리 1, 매 바퀴 딴짓)이고 외출·산책 확률 0이라 늘 멍. 손님의 빵 고르기 난수와 순서가 섞여도 같다
        private static void NoStroll(TableSet tables)
        {
            ClerkConfigTable config = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            config.StrollChance = 0d;
            config.OutingChance = 0d;
        }

        // 외출 확률 0: 난수 0이면 산책. 딴짓 시간은 최소로
        private static void NoOuting(TableSet tables)
        {
            ClerkConfigTable config = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            config.OutingChance = 0d;
            config.IdleSecondsMax = config.IdleSecondsMin;
        }

        // 검증 1: 딴짓 중인 점원만 사물이고, 웜뱃이 range 안이면 버튼 행동이 wake. 일하는 점원은 사물이 아니다
        [Test]
        public void IdlingClerk_IsInteractable_WakeIsTheButton()
        {
            BakeryArea shop = Create(new ScriptRandom(0d), NoStroll);
            Clerk clerk = Hire(shop, shop.Counter);
            Assert.That(RunUntil(shop, () => clerk.Working), Is.True);
            Assert.That(ThingOf(shop, clerk), Is.Null, "일하는 점원은 사물이 아니다");

            shop.Shelves[0].Put(m_tables.Get<BreadTable>("b01"), 4);
            shop.Admit(FirstCustomer());
            Assert.That(RunUntil(shop, () => clerk.Idling), Is.True);
            shop.Tick(k_Dt);
            ClerkInteractable thing = ThingOf(shop, clerk);
            Assert.That(thing, Is.Not.Null);
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Wait));

            shop.Wombat.Mover.Place(clerk.Position + new Vector2(0.5f, 0f));
            shop.Tick(k_Dt);
            Assert.That(shop.Target, Is.SameAs(thing));
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ActionTable.k_Wake));
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Question), "웜뱃이 가까우면 ?");
            Assert.That(clerk.Facing, Is.EqualTo(Facing.Right), "웜뱃 쪽을 본다");
        }

        // 검증 2: 깨우면 딴짓이 끝나고 자리로, 다음 바퀴는 판정 없이 일하고 그 다음 바퀴는 다시 딴짓
        [Test]
        public void Wake_EndsIdle_SkipsNextIdleOnce()
        {
            BakeryArea shop = Create(new ScriptRandom(0d), NoStroll);
            Clerk clerk = Hire(shop, shop.Counter);
            shop.Shelves[0].Put(m_tables.Get<BreadTable>("b01"), 8);
            Assert.That(RunUntil(shop, () => clerk.Working), Is.True);
            int woke = 0;
            m_bus.Subscribe<Events.ClerkWoke>(_ => woke++);

            shop.Admit(FirstCustomer());
            Assert.That(RunUntil(shop, () => clerk.Idling), Is.True);
            shop.Tick(k_Dt);
            shop.Wombat.Mover.Place(clerk.Position + new Vector2(0.5f, 0f));
            shop.Tick(k_Dt);
            Assert.That(shop.TryInteract(), Is.True);

            Assert.That(clerk.Idling, Is.False);
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Alert));
            Assert.That(woke, Is.EqualTo(1));
            Assert.That(shop.Dialogue, Is.Not.Null);
            shop.Tick(k_Dt);
            Assert.That(ThingOf(shop, clerk), Is.Null, "깨어난 점원은 사물에서 빠진다");
            Assert.That(RunUntil(shop, () => clerk.Working), Is.True);

            // 다음 손님 계산 뒤에는 딴짓하지 않고, 그 다음에는 다시 딴짓
            int served = shop.Counter.Served;
            shop.Admit(FirstCustomer());
            Assert.That(RunUntil(shop, () => shop.Counter.Served > served), Is.True);
            Run(shop, 1d);
            Assert.That(clerk.Idling, Is.False, "깨운 뒤 한 바퀴는 딴짓하지 않는다");

            served = shop.Counter.Served;
            shop.Admit(FirstCustomer());
            Assert.That(RunUntil(shop, () => shop.Counter.Served > served), Is.True);
            Assert.That(RunUntil(shop, () => clerk.Idling, 2d), Is.True, "그 다음 바퀴는 다시 딴짓");
        }

        // 검증 3: 산책은 자리를 벗어났다가 돌아오고, 수다는 딴짓 중인 점원 옆에 가서 마주 본다
        [Test]
        public void Stroll_LeavesSpotAndReturns()
        {
            // 난수 전부 0: 딴짓 판정 0 → 시간 → 종류 난수 0(외출 0이라 산책) → 자리 난수 0(자리에서 −3, −3 근처의 걷는 점)
            BakeryArea shop = Create(new ScriptRandom(0d), NoOuting);
            Clerk clerk = Hire(shop, shop.Ovens[0]);
            Assert.That(RunUntil(shop, () => clerk.Idling), Is.True);
            Assert.That(clerk.Idle, Is.EqualTo(IdleKind.Stroll));
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Note));
            Assert.That(RunUntil(shop, () => Vector2.Distance(clerk.Position, clerk.WorkerSpot) > 0.5f, 5d), Is.True, "자리를 벗어난다");
            Assert.That(RunUntil(shop, () => !clerk.Idling && clerk.Working, 20d), Is.True, "돌아와 일한다");
        }

        [Test]
        public void Chat_JoinsIdlingClerkAndFacesThem()
        {
            // 딴짓을 길게(30초) 해서 둘이 겹치게 한다. 난수 전부 0: 첫 점원은 산책, 둘째는 다른 점원이 딴짓 중이라 수다
            BakeryArea shop = Create(new ScriptRandom(0d), tables =>
            {
                ClerkConfigTable config = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
                config.IdleSecondsMin = 30d;
                config.IdleSecondsMax = 30d;
                config.OutingChance = 0d;
            });
            Clerk first = Hire(shop, shop.Ovens[0]);
            Assert.That(RunUntil(shop, () => first.Idling), Is.True);

            Assert.That(shop.TryFindSpot(OvenInteractable.k_Id, new Vector2(1.5f, -8f), out Vector2 ovenAt), Is.True);
            Assert.That(shop.TryBuy(OvenInteractable.k_Id, ovenAt), Is.True);
            Clerk second = Hire(shop, shop.Ovens[1]);
            Assert.That(RunUntil(shop, () => second.Idling, 40d), Is.True);
            Assert.That(second.Idle, Is.EqualTo(IdleKind.Chat));
            Assert.That(second.Bubble.Id, Is.EqualTo(BubbleTable.k_Chat));
            Assert.That(RunUntil(shop, () => !second.Moving, 10d), Is.True);
            Assert.That(Vector2.Distance(second.Position, first.Position), Is.LessThan(1f));
            Facing toward = second.Position.X > first.Position.X ? Facing.Left : Facing.Right;
            Assert.That(second.Facing, Is.EqualTo(toward));
        }

        // 검증 4: 대화는 줄 순서대로 lineSeconds 간격, 마지막 뒤 끝. 새 대화가 앞 것을 대신한다
        [Test]
        public void Dialogue_EmitsLinesInOrder_ThenEnds()
        {
            BakeryArea shop = Create(new ScriptRandom(0d));
            Clerk clerk = Hire(shop, shop.Counter);
            DialogueTable table = m_tables.Get<DialogueTable>(DialogueTable.k_ClerkWake);
            List<Events.DialogueLine> lines = new List<Events.DialogueLine>();
            int ended = 0;
            m_bus.Subscribe<Events.DialogueLine>(e => lines.Add(e));
            m_bus.Subscribe<Events.DialogueEnded>(_ => ended++);

            shop.StartDialogue(DialogueTable.k_ClerkWake, clerk);
            shop.Tick(k_Dt);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].Speaker, Is.EqualTo(DialogueSpeaker.Clerk));
            Assert.That(lines[0].SpeakerObject, Is.SameAs(clerk));
            Assert.That(lines[0].TextId, Is.EqualTo(table.Lines[0].Texts[0]));
            Assert.That(lines[0].Seconds, Is.EqualTo(table.LineSeconds));

            Run(shop, table.LineSeconds - 0.1);
            Assert.That(lines.Count, Is.EqualTo(1));
            Run(shop, 0.2);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[1].Speaker, Is.EqualTo(DialogueSpeaker.Wombat));
            Assert.That(lines[1].SpeakerObject, Is.SameAs(shop.Wombat));

            Run(shop, table.LineSeconds + 0.1);
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(shop.Dialogue, Is.Null);

            shop.StartDialogue(DialogueTable.k_ClerkWake, clerk);
            Dialogue first = shop.Dialogue;
            shop.StartDialogue(DialogueTable.k_ClerkWake, clerk);
            Assert.That(shop.Dialogue, Is.Not.SameAs(first));
        }

        // 검증 5: 순간형 말풍선은 seconds 뒤 사라지고 상태형은 남는다. 손님 두리번 = wait, 결제 = heart
        [Test]
        public void Bubble_TimedClears_StateStays_VisitorUsesWaitAndHeart()
        {
            BubbleState bubble = new BubbleState(TestTables.Load());
            bubble.Show(BubbleTable.k_Alert);
            bubble.Tick(0.5);
            Assert.That(bubble.Id, Is.EqualTo(BubbleTable.k_Alert));
            bubble.Tick(0.6);
            Assert.That(bubble.Id, Is.Null);
            bubble.Show(BubbleTable.k_Wait);
            bubble.Tick(100d);
            Assert.That(bubble.Id, Is.EqualTo(BubbleTable.k_Wait));
            Assert.That(bubble.Elapsed, Is.EqualTo(100d).Within(1e-9));

            BakeryArea shop = Create(new ScriptRandom(0d));
            shop.Wombat.Mover.Place(shop.Layout.WombatHome);
            shop.Admit(FirstCustomer());
            BakeryVisitor visitor = shop.Visitors[0];
            Assert.That(RunUntil(shop, () => visitor.Phase == VisitorPhase.Looking), Is.True);
            Assert.That(visitor.Bubble.Id, Is.EqualTo(BubbleTable.k_Wait));
            shop.Shelves[0].Put(m_tables.Get<BreadTable>("b01"), 4);
            Assert.That(RunUntil(shop, () => visitor.Paid), Is.True);
            Assert.That(visitor.Bubble.Id, Is.EqualTo(BubbleTable.k_Heart));
        }

        // 검증 6: 검증기 — 대화 문구·말풍선 id가 없으면 오류
        [Test]
        public void Validator_ReportsMissingDialogueTextAndBubble()
        {
            TableSet missingText = TestTables.Load("DialogueTable", rows => rows[0]["lines"][0]["texts"][0] = "say_nowhere");
            Assert.That(ZooTycoon.Data.TableValidator.Validate(missingText), Has.Some.Contains("say_nowhere"));

            TableSet missingBubble = TestTables.Load("BubbleTable", rows => rows.First(row => (string)row["id"] == BubbleTable.k_Chat).Remove());
            Assert.That(ZooTycoon.Data.TableValidator.Validate(missingBubble), Has.Some.Contains(BubbleTable.k_Chat));
        }

        private VisitorTable FirstCustomer()
        {
            foreach (VisitorTable look in m_tables.GetAll<VisitorTable>())
            {
                if (look.Role == VisitorRole.Customer)
                {
                    return look;
                }
            }

            return null;
        }
    }
}
