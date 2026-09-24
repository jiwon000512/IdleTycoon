using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
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
        private BakeryArea m_shop;
        private PlazaArea m_plaza;
        private Mall m_mall;

        private void Create(Action<BakeryConfigTable> tweak = null)
        {
            m_tables = TestTables.Load();
            tweak?.Invoke(m_tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery));
            ZooState state = ZooState.CreateNew(m_tables);
            Wombat wombat = new Wombat(m_tables);
            m_shop = new BakeryArea(state, m_tables, new SequenceRandom(new double[2000]), wombat);
            m_plaza = new PlazaArea(m_tables, m_shop, new SequenceRandom(Enumerable.Repeat(0.5, 4000).ToArray()), wombat);
            m_mall = new Mall(m_shop, m_plaza);
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
                    Vector2 delta = point - m_mall.Active.WombatPosition;
                    float distance = delta.Length();
                    m_mall.SetWombatInput(distance < 1e-3f ? Vector2.Zero : delta / distance * Math.Min(1f, distance / stepLength));
                    return distance < 0.05f;
                }, 20d);
            }

            m_mall.SetWombatInput(Vector2.Zero);
        }

        [Test]
        public void ExitAndEnter_MoveWombatBetweenShopAndPlaza()
        {
            Create(c => c.MaxCustomers = 0);
            Vector2 home = m_shop.Layout.WombatHome;
            Vector2 hole = m_shop.Layout.HoleFloor;
            int changes = 0;
            m_mall.AreaChanged += () => changes++;

            Assert.That(m_mall.Current, Is.EqualTo(Area.Bakery));
            Assert.That(m_plaza.WombatPresent, Is.False);

            // 계산대를 오른쪽으로 돌아 구멍 아래로
            Steer(new Vector2(2.4f, home.Y), new Vector2(2.4f, hole.Y), hole);
            Assert.That(m_shop.Target, Is.InstanceOf<ExitInteractable>());
            Assert.That(m_shop.TargetAction.Id, Is.EqualTo(ActionTable.k_Exit));
            Assert.That(m_mall.Active.TryInteract(), Is.True);

            Assert.That(m_mall.Current, Is.EqualTo(Area.Plaza));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(m_shop.WombatPresent, Is.False);
            Assert.That(m_shop.WombatAtCounter, Is.False);
            Assert.That(m_plaza.WombatPresent, Is.True);
            Assert.That(m_plaza.WombatPosition, Is.EqualTo(m_plaza.Layout.DoorFloor));
            Assert.That(m_plaza.Target, Is.InstanceOf<DoorInteractable>());
            Assert.That(m_plaza.TargetAction.Id, Is.EqualTo(ActionTable.k_Enter));

            // 광장에서 걸어 문에서 멀어지면 대상이 없다
            Steer(m_plaza.Layout.DoorFloor + new Vector2(0f, -2f));
            Assert.That(m_plaza.Target, Is.Null);
            Steer(m_plaza.Layout.DoorFloor);
            Assert.That(m_mall.Active.TryInteract(), Is.True);

            Assert.That(m_mall.Current, Is.EqualTo(Area.Bakery));
            Assert.That(m_plaza.WombatPresent, Is.False);
            Assert.That(m_shop.WombatPresent, Is.True);
            Assert.That(m_shop.WombatPosition, Is.EqualTo(hole));
        }

        [Test]
        public void Visitor_VisitsSpotsThenEntersShop_WithSameLook()
        {
            Create();
            Visitor first = null;
            m_plaza.VisitorArrived += v => first ??= v;

            double time = RunUntil(() => m_shop.Customers.Count > 0);

            Assert.That(first, Is.Not.Null);
            Assert.That(m_shop.Customers[0].Look, Is.SameAs(first.Look));
            // 두 곳에서 3.25초씩 머문 뒤에야 들어간다
            Assert.That(time, Is.GreaterThan(2 * 3.25));
            Assert.That(m_plaza.Visitors, Has.No.Member(first));
        }

        [Test]
        public void ShopFull_VisitorsWanderAndLeave()
        {
            Create(c => c.MaxCustomers = 0);
            int left = 0;
            m_plaza.VisitorRemoved += _ => left++;

            Run(40d);

            Assert.That(m_shop.Customers.Count, Is.EqualTo(0));
            Assert.That(left, Is.GreaterThan(0));
        }

        [Test]
        public void CustomerLeavingShop_ComesOutOfDoorWithSameLook()
        {
            Create();
            RunUntil(() => m_shop.Customers.Count > 0);
            Customer customer = m_shop.Customers[0];
            Visitor back = null;
            m_plaza.VisitorArrived += v =>
            {
                if (Vector2.Distance(v.Position, m_plaza.Layout.DoorInside) < 1e-3f)
                {
                    back ??= v;
                }
            };

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
            m_mall.Wombat.Hands.Add(bread, 3);
            Vector2 home = m_shop.Layout.WombatHome;
            Vector2 hole = m_shop.Layout.HoleFloor;

            Steer(new Vector2(2.4f, home.Y), new Vector2(2.4f, hole.Y), hole);
            Assert.That(m_mall.Active.TryInteract(), Is.True);
            Assert.That(m_mall.Current, Is.EqualTo(Area.Plaza));
            Assert.That(m_mall.Active.TryInteract(), Is.True);

            Assert.That(m_mall.Current, Is.EqualTo(Area.Bakery));
            Assert.That(m_mall.Wombat.Hands.Count, Is.EqualTo(3));
            Assert.That(m_mall.Wombat.Hands.Bread, Is.SameAs(bread));
        }
    }
}
