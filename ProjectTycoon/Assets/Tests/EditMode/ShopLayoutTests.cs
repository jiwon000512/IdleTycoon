using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 손님 동선 설계 v0.2 검증 1: 기본 굴(진열대 (−1,1), 오븐 (−1,3))의 줄 자리·서는 자리·길
    public sealed class ShopLayoutTests
    {
        private ShopSim Create()
        {
            GameConfig config = TestTables.LoadConfig();
            return new ShopSim(ZooState.CreateNew(config), TestTables.Build(config: config), new SequenceRandom(new double[10]));
        }

        [Test]
        public void QueueSlots_FillCapacity_OnWalkableGroundAt08Apart()
        {
            ShopSim shop = Create();
            IReadOnlyList<Vector2> slots = shop.Layout.QueueSlots;

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

            // 머리는 계산대 위, 줄은 입구 구멍 아래와 가운데 통로(입구 줄)에서 떨어져 있다
            Assert.That(slots[0].Y, Is.GreaterThan(shop.Layout.CounterBase.Y));

            foreach (Vector2 slot in slots)
            {
                Assert.That(Vector2.Distance(slot, shop.Layout.HoleFloor), Is.GreaterThan(1f));
            }
        }

        [Test]
        public void ShelfSpots_AreWalkable_AndKeepAwayFromQueue()
        {
            ShopSim shop = Create();
            IReadOnlyList<Vector2> spots = shop.Layout.ShelfSpots(new Cell(-1, 1));

            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(2));

            foreach (Vector2 spot in spots)
            {
                Assert.That(shop.Layout.Nav.IsWalkable(spot), Is.True);

                foreach (Vector2 slot in shop.Layout.QueueSlots)
                {
                    Assert.That(Vector2.Distance(spot, slot), Is.GreaterThanOrEqualTo(0.6f));
                }
            }

            // 첫 자리는 가운데 쪽 옆: 진열대를 왼쪽에 두고 선다
            Assert.That(spots[0].X, Is.GreaterThan(shop.Layout.ShelfBase(new Cell(-1, 1)).X));
            Assert.That(shop.Layout.ShelfFacing(new Cell(-1, 1), spots[0]), Is.EqualTo(Facing.Left));
        }

        // 입구 → 진열대, 진열대 → 줄 머리, 줄 머리 → 입구, 웜뱃 → 오븐 모두 이어지고 가로·세로로만 걷는다
        [Test]
        public void Paths_BetweenKeyPoints_ExistAndAreAxisAligned()
        {
            ShopSim shop = Create();
            ShopLayout layout = shop.Layout;
            Vector2 spot = layout.ShelfSpots(new Cell(-1, 1))[0];
            (Vector2 from, Vector2 to)[] trips =
            {
                (layout.HoleFloor, spot),
                (spot, layout.QueueSlots[0]),
                (layout.QueueSlots[0], layout.HoleFloor),
                (layout.WombatHome, layout.OvenSpot(new Cell(-1, 3))),
                (layout.OvenSpot(new Cell(-1, 3)), layout.WombatHome),
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

        // 웜뱃은 계산대를 뚫지 않는다: 길 위 점이 계산대 그림 사각형 안에 들어가지 않는다
        [Test]
        public void WombatPath_ToOvenAboveCounter_GoesAroundCounter()
        {
            ShopSim shop = Create();
            ShopLayout layout = shop.Layout;
            Vector2 counter = layout.CounterBase;
            List<Vector2> path = layout.Nav.FindPath(layout.WombatHome, layout.CellCenter(new Cell(0, 1)));
            Vector2 p = layout.WombatHome;

            Assert.That(path, Is.Not.Empty);

            foreach (Vector2 q in path)
            {
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    Vector2 s = Vector2.Lerp(p, q, t);
                    bool inCounter = Math.Abs(s.X - counter.X) < 1.05f && s.Y > counter.Y && s.Y < counter.Y + 0.725f;
                    Assert.That(inCounter, Is.False, $"계산대 안 {s}");
                }

                p = q;
            }
        }
    }
}
