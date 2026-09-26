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
    // 설계 11 검증 1~3: 빵집 ↔ 광장 오가기, 광장 손님 흐름, 걷는 땅. 실제 JSON 값, 난수는 모두 0.5
    // (들를 곳 2곳 · 빵집에 들어감 · 머묾 3.25초 · ♥ 없음)
    public sealed class PlazaAreaTests
    {
        private const double k_Dt = 0.02;

        private TableSet m_tables;
        private EventBus m_bus;
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private Mall m_mall;

        private void Create(Action<BakeryConfigTable> tweak = null)
        {
            m_tables = TestTables.Load();
            tweak?.Invoke(m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery));
            m_bus = new EventBus();
            ZooState state = ZooState.CreateNew(m_tables, m_bus);
            Wombat wombat = new Wombat(m_tables, state);
            m_shop = new BakeryArea(state, m_tables, new SequenceRandom(new double[2000]), wombat, m_bus);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), wombat, m_bus);
            m_mall = new Mall(m_shop, m_plaza, m_bus);
        }

        private void Run(double seconds)
        {
            for (double t = 0d; t < seconds - 1e-9; t += k_Dt)
            {
                m_mall.Tick(k_Dt);
            }
        }

        private double RunUntil(Func<bool> done, double maxSeconds = 60d)
        {
            for (double t = 0d; t < maxSeconds; t += k_Dt)
            {
                if (done())
                {
                    return t;
                }

                m_mall.Tick(k_Dt);
            }

            Assert.Fail("시간 안에 조건이 참이 되지 않았다.");
            return maxSeconds;
        }

        // 조이스틱으로 지금 있는 곳의 웜뱃을 점마다 끈다
        private void Steer(params Vector2[] points)
        {
            float stepLength = (float)(m_tables.Get<ConfigTable>(ConfigTable.k_WombatSpeed).Value * k_Dt);

            foreach (Vector2 point in points)
            {
                RunUntil(() =>
                {
                    Vector2 delta = point - m_mall.Active.Wombat.Mover.Position;
                    float distance = delta.Length();
                    m_mall.Wombat.SetInput(distance < 1e-3f ? Vector2.Zero : delta / distance * Math.Min(1f, distance / stepLength));
                    return distance < 0.05f;
                }, 20d);
            }

            m_mall.Wombat.SetInput(Vector2.Zero);
        }

        // 조이스틱을 한 방향으로 밀어 통로를 지나 곳이 바뀔 때까지(자동 이동)
        private void PushUntil(Vector2 direction, WombatArea to)
        {
            RunUntil(() =>
            {
                m_mall.Wombat.SetInput(direction);
                return m_mall.Active == to;
            }, 5d);
            m_mall.Wombat.SetInput(Vector2.Zero);
        }

        // 2026-09-26 자동 이동: 구멍·문 앞 띠(바닥선에서 0.25 위)에 들어오면 버튼 없이 넘어간다. 들어온 직후에는 조이스틱을 놓을 때까지 서 있고(굴을 등짐), 놓았다 다시 위로 밀면 물러서지 않아도 넘어간다
        [Test]
        public void ExitAndEnter_MoveWombatBetweenShopAndPlaza()
        {
            Create(c => c.MaxCustomers = 0);
            Vector2 home = m_shop.Layout.WombatHome;
            Vector2 hole = m_shop.Layout.HoleFloor;
            int changes = 0;
            m_bus.Subscribe<Events.AreaChanged>(_ => changes++);

            Assert.That(m_mall.Active, Is.SameAs(m_shop));
            Assert.That(m_plaza.WombatPresent, Is.False);

            // 계산대를 오른쪽으로 돌아 구멍 아래 바닥선까지는 안 넘어가고, 위로 밀어 띠에 들면 넘어간다
            Steer(new Vector2(2.4f, home.Y), new Vector2(2.4f, hole.Y), hole);
            Assert.That(m_mall.Active, Is.SameAs(m_shop));
            Assert.That(m_shop.Target, Is.Not.InstanceOf<PassageInteractable>());
            PushUntil(new Vector2(0f, 1f), m_plaza);

            // 도착 뒤 계속 위로 밀어도 문 위에서 아래를 본 채 서 있다
            m_mall.Wombat.SetInput(new Vector2(0f, 1f));
            Run(1d);
            Assert.That(m_mall.Active, Is.SameAs(m_plaza));
            Assert.That(m_plaza.Wombat.Mover.Facing, Is.EqualTo(Facing.Down));
            Assert.That(m_plaza.Wombat.Moving, Is.False);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Run(0.1d);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(m_shop.WombatPresent, Is.False);
            Assert.That(m_shop.IsInRange(m_shop.Counter), Is.False);
            Assert.That(m_plaza.WombatPresent, Is.True);
            Assert.That(m_plaza.Wombat.Mover.Position, Is.EqualTo(m_plaza.Layout.DoorFloor));
            Assert.That(m_plaza.Target, Is.Null);

            // 놓았다가 그 자리에서 다시 위로 조금만(조이스틱 0.3) 밀어도 물러서지 않고 빵집
            PushUntil(new Vector2(0f, 0.3f), m_shop);
            Assert.That(changes, Is.EqualTo(2));
            m_mall.Wombat.SetInput(Vector2.Zero);
            Run(0.1d);

            // 빵집 구멍 아래에서 멀어졌다가 위로 밀며 다가가면 바닥선에서 0.25 올라간 순간 다시 광장
            Steer(hole + new Vector2(0f, -2f));
            Assert.That(m_shop.Target, Is.Not.InstanceOf<PassageInteractable>());
            PushUntil(new Vector2(0f, 1f), m_plaza);
            Assert.That(changes, Is.EqualTo(3));
            m_mall.Wombat.SetInput(Vector2.Zero);
            Run(0.1d);
            PushUntil(new Vector2(0f, 1f), m_shop);
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(m_plaza.WombatPresent, Is.False);
            Assert.That(m_shop.WombatPresent, Is.True);
            Assert.That(m_shop.Wombat.Mover.Position, Is.EqualTo(hole));
        }

        [Test]
        public void Visitor_VisitsSpotsThenEntersShop_WithSameLook()
        {
            Create();
            PlazaVisitor first = null;
            m_bus.Subscribe<Events.PlazaVisitorArrived>(e => first ??= e.Visitor);

            double time = RunUntil(() => m_shop.Visitors.Count > 0);

            Assert.That(first, Is.Not.Null);
            Assert.That(m_shop.Visitors[0].Look, Is.SameAs(first.Look));
            // 두 곳에서 3.25초씩 머문 뒤에야 들어간다
            Assert.That(time, Is.GreaterThan(2 * 3.25));
            Assert.That(m_plaza.Visitors, Has.No.Member(first));
        }

        [Test]
        public void ShopFull_VisitorsWanderAndLeave()
        {
            Create(c => c.MaxCustomers = 0);
            int left = 0;
            m_bus.Subscribe<Events.PlazaVisitorLeft>(_ => left++);

            Run(40d);

            Assert.That(m_shop.Visitors.Count, Is.EqualTo(0));
            Assert.That(left, Is.GreaterThan(0));
        }

        [Test]
        public void CustomerLeavingShop_ComesOutOfDoorWithSameLook()
        {
            Create();
            RunUntil(() => m_shop.Visitors.Count > 0);
            BakeryVisitor customer = m_shop.Visitors[0];
            PlazaVisitor back = null;
            m_bus.Subscribe<Events.PlazaVisitorArrived>(e =>
            {
                if (Vector2.Distance(e.Visitor.Position, m_plaza.Layout.DoorInside) < 1e-3f)
                {
                    back ??= e.Visitor;
                }
            });

            // 진열대가 비어 두리번하다 화나서 나간다(인내 6초)
            RunUntil(() => back != null, 40d);

            Assert.That(back.Look, Is.SameAs(customer.Look));
            Assert.That(customer.Angry, Is.True);
        }

        [Test]
        public void Layout_DoorStairsAndSpotsWalkable()
        {
            Create();
            PlazaLayout layout = m_plaza.Layout;

            Assert.That(layout.Nav.IsWalkable(layout.DoorFloor), Is.True);
            Assert.That(layout.Nav.IsWalkable(layout.StairsFloor), Is.True);
            Assert.That(layout.Decor.Count, Is.EqualTo(m_tables.GetAll<PlazaDecorTable>().Count));
            // 벽에 붙은 화분의 벽 쪽 자리만 빠진다
            Assert.That(layout.Spots.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(layout.Spots.All(s => layout.Nav.IsWalkable(s.Position)), Is.True);
        }

        // 설계 13: 웜뱃이 하나라 든 빵이 광장에 갔다 와도 그대로다
        [Test]
        public void CarriedBread_StaysInHands_AcrossPlaza()
        {
            Create(c => c.MaxCustomers = 0);
            BreadTable bread = m_tables.Get<BreadTable>("b01");
            m_mall.Wombat.Worker.Hands.Add(bread, 3, null);
            Vector2 home = m_shop.Layout.WombatHome;
            Vector2 hole = m_shop.Layout.HoleFloor;

            Steer(new Vector2(2.4f, home.Y), new Vector2(2.4f, hole.Y), hole);
            PushUntil(new Vector2(0f, 1f), m_plaza);
            m_mall.Wombat.SetInput(Vector2.Zero);
            Run(0.1d);
            PushUntil(new Vector2(0f, 1f), m_shop);

            Assert.That(m_mall.Active, Is.SameAs(m_shop));
            Assert.That(m_mall.Wombat.Worker.Hands.Count, Is.EqualTo(3));
            Assert.That(m_mall.Wombat.Worker.Hands.Bread, Is.SameAs(bread));
        }

        // 설계 16: 다른 곳(가짜 빵집)의 통로 사건은 Mall이 무시하고, 곳의 사건은 발신자가 그 곳이다
        [Test]
        public void Passed_FromForeignArea_IsIgnored()
        {
            Create(c => c.MaxCustomers = 0);
            ZooState otherState = ZooState.CreateNew(m_tables, m_bus);
            BakeryArea other = new BakeryArea(otherState, m_tables, new SequenceRandom(new double[10]), new Wombat(m_tables, otherState), m_bus);
            int changes = 0;
            m_bus.Subscribe<Events.AreaChanged>(_ => changes++);

            m_bus.Publish(new Events.Passed(other));

            Assert.That(changes, Is.EqualTo(0));
            Assert.That(m_mall.Active, Is.SameAs(m_shop));

            m_bus.Publish(new Events.Passed(m_shop));

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(m_mall.Active, Is.SameAs(m_plaza));
        }

        // 설계 13 v0.5: 표가 사물에 붙인 행동은 그 사물을 받을 수 있다(맞지 않는 짝이 조용히 안 보이는 일을 막는다)
        [Test]
        public void EveryTableAction_AcceptsItsThing()
        {
            Create();

            foreach (WombatArea area in new WombatArea[] { m_shop, m_plaza })
            {
                foreach (Interactable thing in area.Things)
                {
                    foreach (string id in thing.Table.Actions)
                    {
                        Assert.That(ActionFactory.Create(m_tables.Get<ActionTable>(id)).Accepts(thing), Is.True, $"{thing.Table.Id} · {id}");
                    }
                }
            }
        }
    }
}
