using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 22 외출(2026-09-26 사용자): 딴짓 시간은 일머리로, 딴짓 중 광장에 나가 놀 수 있고 웜뱃이 광장에서 깨운다. 빵집 + 광장을 Mall로 같이 돌린다
    public sealed class OutingTests
    {
        private const double k_Dt = 0.02;

        private sealed class ConstantRandom : IRandom
        {
            private readonly double m_value;

            public ConstantRandom(double value)
            {
                m_value = value;
            }

            public double NextDouble()
            {
                return m_value;
            }
        }

        private EventBus m_bus;
        private ZooState m_state;
        private TableSet m_tables;
        private Mall m_mall;

        // 난수 0: 일머리 1, 매 바퀴 딴짓, 종류 난수 0 < outingChance → 외출
        private BakeryArea Create(Action<ClerkConfigTable> tweak = null)
        {
            m_tables = TestTables.Load();
            tweak?.Invoke(m_tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main));
            m_bus = new EventBus();
            m_state = ZooState.CreateNew(m_tables, m_bus);
            IRandom random = new ConstantRandom(0d);
            Wombat wombat = new Wombat(m_tables, m_state);
            BakeryArea bakery = new BakeryArea(m_state, m_tables, random, wombat, m_bus);
            PlazaArea plaza = new PlazaArea(m_tables, bakery, random, wombat, m_bus);
            m_mall = new Mall(bakery, plaza, m_bus);
            wombat.Mover.Place(bakery.Layout.HoleFloor + new Vector2(2f, -1f));
            return bakery;
        }

        private bool RunUntil(Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return true;
                }

                m_mall.Tick(k_Dt);
            }

            return done();
        }

        private Clerk Hire(BakeryArea shop)
        {
            Candidate candidate = shop.Candidates[0];
            Assert.That(shop.TryHire(candidate, shop.Ovens[0], shop.WageFor(candidate, shop.Ovens[0])), Is.True);
            return shop.ClerkOf(shop.Ovens[0]);
        }

        private PlazaVisitor FigureOf(Clerk clerk)
        {
            foreach (PlazaVisitor visitor in m_mall.Plaza.Visitors)
            {
                if (visitor.Clerk == clerk)
                {
                    return visitor;
                }
            }

            return null;
        }

        // 웜뱃이 빵집 구멍 앞 띠에 서면 저절로 광장으로 넘어간다
        private void WombatToPlaza(BakeryArea shop)
        {
            shop.Wombat.Mover.Place(shop.Layout.HoleFloor + new Vector2(0f, 0.3f));
            Assert.That(RunUntil(() => m_mall.Active == m_mall.Plaza, 2d), Is.True);
            m_mall.Tick(k_Dt);
        }

        // 딴짓 시간은 일머리 1이면 1분 넘게, 100이면 최소
        [Test]
        public void IdleSeconds_FollowSkill()
        {
            ClerkConfigTable config = TestTables.Load().Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            double low = config.IdleSecondsMin + (config.IdleSecondsMax - config.IdleSecondsMin) * 0.99;

            Assert.That(low * 0.75, Is.GreaterThan(60d), "일머리 1이면 흔들림 최소여도 1분 넘게");
            Assert.That(config.IdleSecondsMin, Is.LessThan(10d));
        }

        // 외출: 구멍으로 나가면 광장에 그 점원 그림이 서고(말풍선 공유), 광장 사물 목록에 점원이 들어간다. 시간이 다 되면 문으로 돌아와 다시 일한다
        [Test]
        public void Outing_GoesToPlaza_AndReturnsWhenIdleEnds()
        {
            BakeryArea shop = Create(config =>
            {
                config.IdleSecondsMin = 6d;
                config.IdleSecondsMax = 6d;
            });
            Clerk clerk = Hire(shop);

            Assert.That(RunUntil(() => clerk.Away), Is.True, "외출");
            Assert.That(clerk.Idle, Is.EqualTo(IdleKind.Outing));
            PlazaVisitor figure = FigureOf(clerk);
            Assert.That(figure, Is.Not.Null);
            Assert.That(figure.Look, Is.SameAs(clerk.Look));
            Assert.That(figure.Bubble, Is.SameAs(clerk.Bubble));
            Assert.That(shop.Things, Has.None.InstanceOf<ClerkInteractable>(), "빵집에서는 사물이 아니다");
            Assert.That(m_mall.Plaza.Things, Has.Some.InstanceOf<ClerkInteractable>(), "광장에서 사물이다");
            m_mall.Tick(k_Dt);
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Note));

            Assert.That(RunUntil(() => !clerk.Away, 40d), Is.True, "돌아온다");
            Assert.That(FigureOf(clerk), Is.Null);
            Assert.That(m_mall.Plaza.Things, Has.None.InstanceOf<ClerkInteractable>());
            Assert.That(RunUntil(() => clerk.Working, 20d), Is.True, "돌아와 일한다");
        }

        // 광장에서 깨우기: 웜뱃이 광장 그림 옆에 가면 「?」와 wake 버튼, 누르면 그림이 문으로 들어오고 점원이 구멍에서 나와 자리로. 대화 줄은 외출 중인 점원 이름으로 나온다
        [Test]
        public void Wake_InPlaza_BringsClerkBack()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop);
            Assert.That(RunUntil(() => clerk.Away), Is.True);
            List<Events.DialogueLine> lines = new List<Events.DialogueLine>();
            m_bus.Subscribe<Events.DialogueLine>(e => lines.Add(e));

            WombatToPlaza(shop);
            PlazaVisitor figure = FigureOf(clerk);
            Assert.That(RunUntil(() => !figure.Hopping, 5d), Is.True);
            shop.Wombat.Mover.Place(figure.Position + new Vector2(0.5f, 0f));
            m_mall.Tick(k_Dt);
            Assert.That(m_mall.Plaza.TargetAction?.Id, Is.EqualTo(ActionTable.k_Wake));
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Question));

            Assert.That(m_mall.Plaza.TryInteract(), Is.True);
            Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Alert));
            m_mall.Tick(k_Dt);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0].SpeakerObject, Is.SameAs(clerk));
            Assert.That(clerk.Away, Is.True, "돌아오는 동안은 아직 외출 중");

            Assert.That(RunUntil(() => !clerk.Away, 30d), Is.True, "문으로 들어와 구멍에서 나온다");
            Assert.That(RunUntil(() => clerk.Working, 20d), Is.True);
        }

        // 2026-09-26 버그: 광장에서 깨운 점원이 문으로 돌아가는 동안 「?」·「♪」와 깨우기 버튼이 다시 떴다. 돌아오는 길엔 딴짓이 아니다
        [Test]
        public void Wake_InPlaza_NotIdlingWhileReturning()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop);
            Assert.That(RunUntil(() => clerk.Away), Is.True);
            WombatToPlaza(shop);
            PlazaVisitor figure = FigureOf(clerk);
            Assert.That(RunUntil(() => !figure.Hopping, 5d), Is.True);
            shop.Wombat.Mover.Place(figure.Position + new Vector2(0.5f, 0f));
            m_mall.Tick(k_Dt);
            Assert.That(m_mall.Plaza.TryInteract(), Is.True);

            for (int i = 0; i < 30; i++)
            {
                shop.Wombat.Mover.Place(figure.Position + new Vector2(0.5f, 0f));
                m_mall.Tick(k_Dt);
                Assert.That(clerk.Idling, Is.False, "돌아오는 길");
                Assert.That(clerk.Bubble.Id, Is.EqualTo(BubbleTable.k_Alert), "「?」·「♪」로 덮이지 않는다");
                Assert.That(m_mall.Plaza.Things, Has.None.InstanceOf<ClerkInteractable>(), "다시 깨울 수 없다");
            }

            bool idledBeforeWork = false;
            Assert.That(RunUntil(() =>
            {
                idledBeforeWork |= clerk.Idling;
                return clerk.Working;
            }, 40d), Is.True, "돌아와 일한다");
            Assert.That(idledBeforeWork, Is.False, "자리에 닿기 전에 다시 놀지 않는다");
            Assert.That(shop.Things, Has.None.InstanceOf<ClerkInteractable>());
        }

        // 구멍으로 뛰어드는 중에 깨우면 광장에 가지 않고 바로 다시 나와 자리로
        [Test]
        public void Wake_WhileHoppingOut_ComesStraightBack()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop);
            int wentOut = 0;
            m_bus.Subscribe<Events.ClerkWentOut>(_ => wentOut++);
            Assert.That(RunUntil(() => clerk.Idle == IdleKind.Outing && clerk.Hopping && !clerk.Away), Is.True);

            clerk.WakeUp();
            Assert.That(clerk.Idling, Is.False);
            Assert.That(RunUntil(() => clerk.Working, 20d), Is.True, "바로 다시 나와 일한다");
            Assert.That(wentOut, Is.EqualTo(0), "광장에 가지 않는다");
            Assert.That(FigureOf(clerk), Is.Null);
        }

        // 외출 중 해고되면 점원은 바로 사라지고 광장 그림은 계단으로 나간다
        [Test]
        public void Fire_WhileAway_RemovesClerkAndFigureLeaves()
        {
            BakeryArea shop = Create();
            Clerk clerk = Hire(shop);
            Assert.That(RunUntil(() => clerk.Away), Is.True);
            int left = 0;
            m_bus.Subscribe<Events.ClerkLeft>(_ => left++);

            shop.Fire(clerk, FireReason.Fired);
            Assert.That(RunUntil(() => left == 1, 2d), Is.True);
            Assert.That(RunUntil(() => FigureOf(clerk) == null, 30d), Is.True, "그림이 계단으로 나간다");
            Assert.That(m_mall.Plaza.Things, Has.None.InstanceOf<ClerkInteractable>());
        }
    }
}
