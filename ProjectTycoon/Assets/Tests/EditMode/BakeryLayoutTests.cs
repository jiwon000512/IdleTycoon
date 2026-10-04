using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 손님 동선 설계 v0.2 검증 1: 기본 굴(BakeryConfigTable 시작 배치)의 줄 자리·서는 자리·길
    public sealed class BakeryLayoutTests
    {
        private BakeryArea Create()
        {
            TableSet tables = TestTables.Load();
            GameKit.Events.EventBus bus = new GameKit.Events.EventBus();
            ZooState state = ZooState.CreateNew(tables, bus);
            return new BakeryArea(state, tables, new SequenceRandom(new double[10]), new Wombat(tables, state), bus);
        }

        [Test]
        public void QueueSlots_FillCapacity_OnWalkableGroundAt08Apart()
        {
            BakeryArea shop = Create();
            IReadOnlyList<Vector2> slots = shop.Layout.QueueSlots(shop.Counter);

            Assert.That(slots.Count, Is.EqualTo(8));

            for (int i = 0; i < slots.Count; i++)
            {
                Assert.That(shop.Layout.Nav.IsWalkable(slots[i]), Is.True, $"줄 자리 {i} {slots[i]}");

                if (i > 0)
                {
                    Assert.That(Vector2.Distance(slots[i], slots[i - 1]), Is.EqualTo(0.8f).Within(1e-3f));
                    Assert.That(shop.Layout.Nav.IsLineWalkable(slots[i - 1], slots[i]), Is.True);
                }
            }

            // 설계 18: 머리는 계산대 앞(아래), 웜뱃은 뒤(위). 줄은 입구 구멍 아래에서 떨어져 있다
            Assert.That(slots[0].Y, Is.LessThan(shop.Counter.Position.Y));

            foreach (Vector2 slot in slots)
            {
                Assert.That(Vector2.Distance(slot, shop.Layout.HoleFloor), Is.GreaterThan(1f));
                Assert.That(slot.Y, Is.LessThan(shop.Shelves[0].Position.Y - 1.1f));
            }
        }

        [Test]
        public void ShelfSpots_AreWalkable_AndKeepAwayFromQueue()
        {
            BakeryArea shop = Create();
            IReadOnlyList<Vector2> spots = shop.Layout.ShelfSpots(shop.Shelves[0]);

            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(2));

            // 재고 표지판(반 폭 0.275)에 손님(반 폭 약 0.28)이 가리지 않는다
            Vector2 sign = shop.Layout.ShelfSignBase(shop.Shelves[0]);

            foreach (Vector2 spot in spots)
            {
                Assert.That(shop.Layout.Nav.IsWalkable(spot), Is.True);
                Assert.That(Math.Abs(spot.X - sign.X), Is.GreaterThan(0.55f));

                foreach (Vector2 slot in shop.Layout.QueueSlots(shop.Counter))
                {
                    Assert.That(Vector2.Distance(spot, slot), Is.GreaterThanOrEqualTo(0.6f));
                }
            }

            // 구멍에서 가까운 자리부터: 시작 진열대(오른쪽 위)는 가운데 쪽 왼쪽 옆이 먼저고, 진열대를 오른쪽에 두고 선다
            for (int i = 1; i < spots.Count; i++)
            {
                Assert.That(Vector2.Distance(spots[i - 1], shop.Layout.HoleFloor), Is.LessThanOrEqualTo(Vector2.Distance(spots[i], shop.Layout.HoleFloor)));
            }

            Assert.That(spots[0].X, Is.LessThan(shop.Shelves[0].Position.X));
            Assert.That(shop.Layout.ShelfFacing(shop.Shelves[0], spots[0]), Is.EqualTo(Facing.Right));
        }

        // 입구 → 진열대, 진열대 → 줄 머리, 줄 머리 → 입구 모두 이어지고 가로·세로로만 걷는다
        [Test]
        public void Paths_BetweenKeyPoints_ExistAndAreAxisAligned()
        {
            BakeryArea shop = Create();
            BakeryLayout layout = shop.Layout;
            Vector2 spot = layout.ShelfSpots(shop.Shelves[0])[0];
            Vector2 head = layout.QueueSlots(shop.Counter)[0];
            (Vector2 from, Vector2 to)[] trips =
            {
                (layout.HoleFloor, spot),
                (spot, head),
                (head, layout.HoleFloor),
            };

            foreach ((Vector2 from, Vector2 to) in trips)
            {
                List<Vector2> path = layout.Nav.FindPath(from, to);
                Assert.That(path, Is.Not.Empty, $"{from} → {to}");
                Assert.That(Vector2.Distance(path[path.Count - 1], to), Is.LessThan(1e-3f));
                Vector2 p = from;

                foreach (Vector2 q in path)
                {
                    Assert.That(Math.Abs(q.X - p.X) < 0.06f || Math.Abs(q.Y - p.Y) < 0.06f, Is.True, $"대각선 {p} → {q}");
                    p = q;
                }
            }
        }

        // 설계 09 · 설계 18: 웜뱃 자리(계산대 뒤 = 위)는 웜뱃만 드나들고 계산대는 둘 다 막힌다
        [Test]
        public void WombatNav_OpensWombatHomeOnly()
        {
            BakeryArea shop = Create();
            BakeryLayout layout = shop.Layout;
            Vector2 counter = shop.Counter.Position + new Vector2(0f, 0.2f);

            Assert.That(layout.WombatNav.IsWalkable(layout.WombatHome), Is.True);
            Assert.That(layout.Nav.IsWalkable(layout.WombatHome), Is.False);
            Assert.That(layout.WombatNav.IsWalkable(counter), Is.False);
            Assert.That(layout.Nav.IsWalkable(counter), Is.False);
        }
    }
}
