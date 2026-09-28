using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 웜뱃 비켜 가기(2026-09-28): 걷는 손님·점원은 웜뱃 둘레 0.8 안으로 걸어 들어가지 않는다. 돌아갈 길이 없으면 1.5초 기다렸다 지나간다
    public sealed class WombatYieldTests
    {
        private const double k_Dt = 0.02;
        private const float k_Radius = 0.8f;

        private TableSet m_tables;

        private BakeryArea Create()
        {
            m_tables = TestTables.Load();
            EventBus bus = new EventBus();
            ZooState state = ZooState.CreateNew(m_tables, bus);
            BakeryArea shop = new BakeryArea(state, m_tables, new SequenceRandom(new double[200]), new Wombat(m_tables, state), bus);

            if (!shop.WombatPresent)
            {
                shop.Enter();
            }

            // 길에서 먼 구석
            shop.Wombat.Mover.Place(shop.Layout.WombatHome);
            return shop;
        }

        private static Vector2 Step(Facing facing)
        {
            switch (facing)
            {
                case Facing.Up: return new Vector2(0f, 1f);
                case Facing.Down: return new Vector2(0f, -1f);
                case Facing.Left: return new Vector2(-1f, 0f);
                default: return new Vector2(1f, 0f);
            }
        }

        // 구멍에서 나와 자리로 걷기 시작한 손님
        private static BakeryVisitor WalkingVisitor(BakeryArea shop)
        {
            shop.Admit(shop.Tables.GetAll<VisitorTable>()[0]);
            BakeryVisitor visitor = shop.Visitors[0];

            for (double t = 0d; t < 5d && !(visitor.HasSpot && visitor.Moving && !visitor.Hopping); t += k_Dt)
            {
                shop.Tick(k_Dt);
            }

            Assert.That(visitor.HasSpot && visitor.Moving, Is.True);
            return visitor;
        }

        [Test]
        public void FindPath_KeepsOutOfTheAvoidedCircle()
        {
            BakeryArea shop = Create();
            BurrowNav nav = shop.Layout.Nav;
            Vector2 from = shop.Layout.HoleFloor;
            Vector2 to = WalkingVisitor(shop).Spot;
            List<Vector2> straight = nav.FindPath(from, to);
            // 곧은 길의 가장 긴 선분 한가운데를 막는다
            Vector2 avoid = from;
            float longest = 0f;
            Vector2 a = from;

            foreach (Vector2 b in straight)
            {
                if (Vector2.Distance(a, b) > longest)
                {
                    longest = Vector2.Distance(a, b);
                    avoid = (a + b) * 0.5f;
                }

                a = b;
            }

            Assert.That(longest, Is.GreaterThan(k_Radius * 2f));
            List<Vector2> around = nav.FindPath(from, to, avoid, k_Radius);

            Assert.That(around.Count, Is.GreaterThan(0));
            Assert.That(BurrowNav.Length(from, around), Is.GreaterThan(BurrowNav.Length(from, straight)));
            Vector2 p = from;

            foreach (Vector2 q in around)
            {
                for (float k = 0f; k <= 1f; k += 0.05f)
                {
                    Assert.That(Vector2.Distance(Vector2.Lerp(p, q, k), avoid), Is.GreaterThanOrEqualTo(k_Radius - 0.01f));
                }

                p = q;
            }
        }

        [Test]
        public void Visitor_WalksAroundTheWombatOnItsPath()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = WalkingVisitor(shop);
            Vector2 block = shop.Layout.Nav.Snap(visitor.Position + Step(visitor.Facing) * 1.2f);
            Assert.That(shop.Layout.Nav.IsWalkable(block), Is.True);
            Assert.That(Vector2.Distance(block, visitor.Spot), Is.GreaterThan(k_Radius));
            shop.Wombat.Mover.Place(block);
            float closest = float.MaxValue;
            bool waited = false;

            for (double t = 0d; t < 15d && visitor.Moving; t += k_Dt)
            {
                shop.Tick(k_Dt);
                closest = Math.Min(closest, Vector2.Distance(visitor.Position, block));
                waited |= visitor.Yielding;
            }

            Assert.That(visitor.Moving, Is.False);
            Assert.That(Vector2.Distance(visitor.Position, visitor.Spot), Is.LessThan(0.01f));
            Assert.That(closest, Is.GreaterThanOrEqualTo(k_Radius - 0.05f));
            Assert.That(waited, Is.False);
        }

        [Test]
        public void Visitor_WaitsWhileTheWombatStandsOnItsSpot()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = WalkingVisitor(shop);
            shop.Wombat.Mover.Place(visitor.Spot);

            for (double t = 0d; t < 10d; t += k_Dt)
            {
                shop.Tick(k_Dt);
                Assert.That(Vector2.Distance(visitor.Position, visitor.Spot), Is.GreaterThanOrEqualTo(k_Radius - 0.05f));
            }

            Assert.That(visitor.Yielding, Is.True);
            shop.Wombat.Mover.Place(shop.Layout.WombatHome);

            for (double t = 0d; t < 10d && visitor.Moving; t += k_Dt)
            {
                shop.Tick(k_Dt);
            }

            Assert.That(Vector2.Distance(visitor.Position, visitor.Spot), Is.LessThan(0.01f));
        }

        // 웜뱃이 서 있는 곳 둘레의 진열대 자리는 찬 자리다
        [Test]
        public void FreeSpot_SkipsTheSpotUnderTheWombat()
        {
            BakeryArea shop = Create();
            BakeryVisitor first = WalkingVisitor(shop);
            Vector2 spot = first.Spot;
            shop.Wombat.Mover.Place(spot);
            shop.Admit(shop.Tables.GetAll<VisitorTable>()[0]);
            BakeryVisitor second = shop.Visitors[1];

            for (double t = 0d; t < 5d && !second.HasSpot; t += k_Dt)
            {
                shop.Tick(k_Dt);
            }

            Assert.That(second.HasSpot, Is.True);
            Assert.That(Vector2.Distance(second.Spot, spot), Is.GreaterThanOrEqualTo(k_Radius));
        }

        [Test]
        public void Visitor_IgnoresTheWombatWhenItIsElsewhere()
        {
            BakeryArea shop = Create();
            BakeryVisitor visitor = WalkingVisitor(shop);
            shop.Wombat.Mover.Place(visitor.Spot);
            shop.Leave();
            bool waited = false;

            for (double t = 0d; t < 15d && visitor.Moving; t += k_Dt)
            {
                shop.Tick(k_Dt);
                waited |= visitor.Yielding;
            }

            Assert.That(visitor.Moving, Is.False);
            Assert.That(waited, Is.False);
        }
    }
}
