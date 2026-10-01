using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 설계 24 웜뱃 똥(빵집, 설계 37부터 곳 공용 코드): 걸은 거리로 싸기 · 손님은 둘레를 피해 가고 길이 없으면 포기 · 치우기 버튼. 시작 배치(계산대 좌우 통로 폭 1.8) 그대로
    public sealed class PoopTests
    {
        private const double k_Dt = 0.02;
        // 계산대 옆 통로를 막는 두 자리(계산대 가운데 높이, 벽 ↔ 계산대 끝). 둘레 지름 1.0 두 개가 걷는 폭 1.8을 덮는다
        private static readonly float[] k_PassageX = { 1.65f, 2.55f };

        private TableSet m_tables;
        private EventBus m_bus;

        // 설계 37: 똥 숫자는 ConfigTable(웜뱃의 값), 치운 똥이 되는 재료는 농장 거름
        private double Config(string id)
        {
            return m_tables.Get<ConfigTable>(id).Value;
        }

        private string Manure => m_tables.Get<FarmConfigTable>(FarmConfigTable.k_Main).ManureItem;

        private static void Set(TableSet tables, string id, double value)
        {
            tables.Get<ConfigTable>(id).Value = value;
        }

        private BakeryArea Create(Action<TableSet> tweak = null)
        {
            m_tables = TestTables.Load();
            tweak?.Invoke(m_tables);
            m_bus = new EventBus();
            ZooState state = ZooState.CreateNew(m_tables, m_bus);
            return new BakeryArea(state, m_tables, new SequenceRandom(new double[400]), new Wombat(m_tables, state), m_bus);
        }

        private static void Run(BakeryArea shop, double seconds)
        {
            for (double t = 0d; t < seconds; t += k_Dt)
            {
                shop.Tick(k_Dt);
            }
        }

        private static void RunUntil(BakeryArea shop, Func<bool> done, double maxSeconds = 20d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return;
                }

                shop.Tick(k_Dt);
            }

            Assert.Fail("시간 안에 조건이 참이 되지 않았다.");
        }

        // 계산대 가운데 높이의 한쪽(side −1 왼쪽, +1 오른쪽) 통로를 똥 둘로 막는다
        private static void BlockPassage(BakeryArea shop, float side)
        {
            float y = shop.Counter.Position.Y + 0.15f;

            foreach (float x in k_PassageX)
            {
                Assert.That(shop.TryDropPoop(new Vector2(side * x, y)), Is.True);
            }
        }

        // 진열대에 식빵을 채우고 손님 하나를 들여, 빵을 집고 줄로 걷기 시작할 때까지
        private BakeryVisitor VisitorHeadingToQueue(BakeryArea shop)
        {
            shop.Shelves[0].Put(m_tables.Get<BreadTable>("b01"), 4);
            shop.Admit(m_tables.GetAll<VisitorTable>()[0]);
            BakeryVisitor visitor = shop.Visitors[0];
            RunUntil(shop, () => visitor.Phase == VisitorPhase.ToQueue);
            return visitor;
        }

        [Test]
        public void Walking_DropsPoopBehindTheWombatEveryPoopEvery()
        {
            BakeryArea shop = Create(t => Set(t, ConfigTable.k_PoopEvery, 1d));
            shop.Wombat.SetInput(new Vector2(-1f, 0f));
            List<PoopInteractable> dropped = new List<PoopInteractable>();
            m_bus.Subscribe<Events.PoopDropped>(e => dropped.Add(e.Poop));

            RunUntil(shop, () => shop.Poops.Count > 0, 2d);

            // 왼쪽으로 걸으면 등 뒤 = 오른쪽 0.25
            Vector2 behind = shop.Wombat.Mover.Position + new Vector2(0.25f, 0f);
            Assert.That(Vector2.Distance(shop.Poops[0].Position, behind), Is.LessThan(1e-3f));
            Assert.That(dropped, Is.EqualTo(new[] { shop.Poops[0] }));
            Assert.That(shop.Things, Does.Contain(shop.Poops[0]));
        }

        [Test]
        public void Walking_WithZeroChance_DropsNothing()
        {
            BakeryArea shop = Create(t =>
            {
                Set(t, ConfigTable.k_PoopEvery, 1d);
                Set(t, ConfigTable.k_PoopChance, 0d);
            });
            shop.Wombat.SetInput(new Vector2(-1f, 0f));

            Run(shop, 1d);

            Assert.That(shop.Poops, Is.Empty);
        }

        // 순간 이동은 걸은 거리가 아니고, 광장에 가 있으면 빵집에서 걷지 않는다
        [Test]
        public void TeleportOrBeingAway_DropsNothing()
        {
            BakeryArea shop = Create(t => Set(t, ConfigTable.k_PoopEvery, 1d));

            for (int i = 0; i < 6; i++)
            {
                shop.Wombat.Mover.Place(new Vector2(i % 2 == 0 ? -2f : 2f, -3f));
                shop.Tick(k_Dt);
            }

            shop.Leave();
            shop.Wombat.SetInput(new Vector2(-1f, 0f));
            Run(shop, 2d);

            Assert.That(shop.Poops, Is.Empty);
        }

        [Test]
        public void TryDropPoop_KeepsTheCapAndTheGap()
        {
            BakeryArea shop = Create();

            Assert.That(shop.TryDropPoop(new Vector2(2f, -3f)), Is.True);
            Assert.That(shop.TryDropPoop(new Vector2(2f + (float)Config(ConfigTable.k_PoopGap) - 0.05f, -3f)), Is.False);

            for (int i = 1; i < (int)Config(ConfigTable.k_PoopMax); i++)
            {
                Assert.That(shop.TryDropPoop(new Vector2(2f, -3f - i)), Is.True);
            }

            Assert.That(shop.Poops.Count, Is.EqualTo((int)Config(ConfigTable.k_PoopMax)));
            Assert.That(shop.TryDropPoop(new Vector2(-2f, -3f)), Is.False);
        }

        // 손님 땅의 길은 둘레 밖으로 돌고(도착점 제외), 둘레 안에서 시작하면 걸어 나온다. 웜뱃·점원 땅은 그대로
        [Test]
        public void CustomerPath_KeepsOutOfTheRim_WombatPathDoesNot()
        {
            BakeryArea shop = Create();
            Vector2 from = new Vector2(-2f, -3f);
            Vector2 to = new Vector2(2f, -3f);
            float straight = BurrowNav.Length(from, shop.Layout.Nav.FindPath(from, to));
            Vector2 poop = new Vector2(0f, -3f);
            float radius = (float)Config(ConfigTable.k_PoopAvoidRadius);

            shop.TryDropPoop(poop);
            List<Vector2> around = shop.Layout.Nav.FindPath(from, to);

            Assert.That(around.Count, Is.GreaterThan(0));
            Assert.That(BurrowNav.Length(from, around), Is.GreaterThan(straight));
            Vector2 p = from;

            foreach (Vector2 q in around)
            {
                for (float k = 0f; k <= 1f; k += 0.05f)
                {
                    Assert.That(Vector2.Distance(Vector2.Lerp(p, q, k), poop), Is.GreaterThanOrEqualTo(radius - 0.01f));
                }

                p = q;
            }

            Assert.That(BurrowNav.Length(from, shop.Layout.WombatNav.FindPath(from, to)), Is.EqualTo(straight).Within(1e-3f));
            Assert.That(shop.Layout.Nav.FindPath(poop + new Vector2(0.2f, 0f), to).Count, Is.GreaterThan(0));
        }

        // 한쪽 통로가 막히면 반대쪽으로 돌아 줄에 선다(포기하지 않는다)
        [Test]
        public void Visitor_GoesAroundWhenOnePassageIsBlocked()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = VisitorHeadingToQueue(shop);
            BlockPassage(shop, -1f);
            float closest = float.MaxValue;
            float right = float.MinValue;

            for (double t = 0d; t < 20d && visitor.Phase != VisitorPhase.Queued; t += k_Dt)
            {
                shop.Tick(k_Dt);

                foreach (PoopInteractable poop in shop.Poops)
                {
                    closest = Math.Min(closest, Vector2.Distance(visitor.Position, poop.Position));
                }

                right = Math.Max(right, visitor.Position.X);
            }

            Assert.That(visitor.Phase, Is.EqualTo(VisitorPhase.Queued));
            Assert.That(visitor.Disgusted, Is.False);
            Assert.That(closest, Is.GreaterThanOrEqualTo((float)Config(ConfigTable.k_PoopAvoidRadius) - 0.05f));
            Assert.That(right, Is.GreaterThan(shop.Counter.Position.X + 1f));
        }

        // 두 통로가 다 막히면 🤢 → 줄에서 빠지고 든 빵을 버리고 구멍으로 나간다
        [Test]
        public void Visitor_GivesUpWhenTheQueueIsCutOff()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = VisitorHeadingToQueue(shop);
            BlockPassage(shop, -1f);
            BlockPassage(shop, 1f);

            Run(shop, 0.1d);

            Assert.That(visitor.Disgusted, Is.True);
            Assert.That(visitor.Angry, Is.True);
            Assert.That(visitor.CarriesBread, Is.False);
            Assert.That(visitor.Bubble.Id, Is.EqualTo(BubbleTable.k_Yuck));
            Assert.That(shop.Counter.Queue, Has.No.Member(visitor));
            RunUntil(shop, () => shop.Visitors.Count == 0);
        }

        // 구멍에서 나오자마자 사방이 둘레면 🤢로 바로 되돌아간다
        [Test]
        public void Visitor_GoesBackIntoTheHoleWhenBoxedIn()
        {
            BakeryArea shop = Create();
            Vector2 hole = shop.Layout.HoleFloor;

            foreach (Vector2 d in new[] { new Vector2(0.5f, 0f), new Vector2(-0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, -0.5f) })
            {
                Assert.That(shop.TryDropPoop(hole + d), Is.True);
            }

            shop.Admit(m_tables.GetAll<VisitorTable>()[0]);
            BakeryVisitor visitor = shop.Visitors[0];
            RunUntil(shop, () => visitor.Disgusted, 3d);
            RunUntil(shop, () => shop.Visitors.Count == 0, 3d);
        }

        // 나가는 손님은 길이 막혀도 둘레를 무시하고 나간다(가게가 멈추지 않게)
        [Test]
        public void LeavingVisitor_PassesThroughWhenCutOff()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = VisitorHeadingToQueue(shop);
            RunUntil(shop, () => visitor.Paid);
            BlockPassage(shop, -1f);
            BlockPassage(shop, 1f);

            RunUntil(shop, () => shop.Visitors.Count == 0);

            Assert.That(visitor.Disgusted, Is.False);
        }

        // 진열대 서는 자리가 둘레 안이면 다른 자리를 고른다
        [Test]
        public void Visitor_SkipsAShelfSpotInsideTheRim()
        {
            BakeryArea shop = Create();
            Vector2 first = shop.Layout.ShelfSpots(shop.Shelves[0])[0];
            shop.TryDropPoop(first);
            shop.Admit(m_tables.GetAll<VisitorTable>()[0]);
            BakeryVisitor visitor = shop.Visitors[0];

            RunUntil(shop, () => visitor.HasSpot, 3d);

            Assert.That(Vector2.Distance(visitor.Spot, first), Is.GreaterThanOrEqualTo((float)Config(ConfigTable.k_PoopAvoidRadius)));
        }

        // 똥이 대상이면 버튼은 치우기, 누르면 range 안 똥만 전부 치운다. 치운 만큼 거름이 창고로 간다(설계 28)
        [Test]
        public void CleanButton_ClearsEveryPoopInRange()
        {
            BakeryArea shop = Create();
            List<PoopInteractable> cleaned = new List<PoopInteractable>();
            m_bus.Subscribe<Events.PoopCleaned>(e => cleaned.Add(e.Poop));
            Vector2 wombat = new Vector2(2f, -3f);
            shop.Wombat.Mover.Place(wombat);
            shop.TryDropPoop(wombat + new Vector2(0.3f, 0f));
            shop.TryDropPoop(wombat + new Vector2(-0.4f, 0f));
            shop.TryDropPoop(new Vector2(-2f, -7f));
            shop.Tick(k_Dt);
            int manure = shop.Wallet.Count(Manure);

            Assert.That(shop.Target, Is.InstanceOf<PoopInteractable>());
            Assert.That(shop.TargetAction.Id, Is.EqualTo(ActionTable.k_Clean));
            Assert.That(shop.TryInteract(), Is.True);
            Assert.That(cleaned.Count, Is.EqualTo(2));
            Assert.That(shop.Poops.Count, Is.EqualTo(1));
            Assert.That(shop.Wallet.Count(Manure), Is.EqualTo(manure + 2));
            Assert.That(shop.Things, Has.No.Member(cleaned[0]));
        }

        // 사물을 똥 위에 놓으면 그 똥은 치운 셈(거름은 안 된다)
        [Test]
        public void PlacingAThingOnPoop_ClearsIt()
        {
            BakeryArea shop = Create();
            Assert.That(shop.TryFindSpot(ShelfInteractable.k_Id, new Vector2(1.7f, -3.6f), out Vector2 spot), Is.True);
            shop.TryDropPoop(spot + new Vector2(0f, 0.2f));
            int manure = shop.Wallet.Count(Manure);

            Assert.That(shop.TryBuy(ShelfInteractable.k_Id, spot), Is.True);
            Assert.That(shop.Poops, Is.Empty);
            Assert.That(shop.Wallet.Count(Manure), Is.EqualTo(manure));
        }
    }
}
