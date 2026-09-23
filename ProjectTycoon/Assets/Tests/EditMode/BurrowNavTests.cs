using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 손님 동선 설계 v0.2 검증 1: 4방향 A*. 마스크는 한 칸 = 1/40유닛, 가게 좌표 y 위
    public sealed class BurrowNavTests
    {
        private const float k_Ppu = 40f;
        private const float k_Clearance = 0.3f;
        private const float k_Step = 0.2f;
        private const float k_TurnPenalty = 0.6f;

        // 가게 좌표 사각형들(x0, y0, x1, y1, y 위)을 바닥으로 칠한 마스크. 원점 (0,0)에서 오른쪽 아래로 w×h 유닛
        private static BurrowShape.Result Mask(float w, float h, params (float x0, float y0, float x1, float y1)[] floors)
        {
            int pw = (int)(w * k_Ppu);
            int ph = (int)(h * k_Ppu);
            bool[,] mask = new bool[pw, ph];

            for (int y = 0; y < ph; y++)
            {
                for (int x = 0; x < pw; x++)
                {
                    float ux = (x + 0.5f) / k_Ppu;
                    float uy = -(y + 0.5f) / k_Ppu;

                    foreach ((float x0, float y0, float x1, float y1) in floors)
                    {
                        if (ux >= x0 && ux <= x1 && uy >= y0 && uy <= y1)
                        {
                            mask[x, y] = true;
                        }
                    }
                }
            }

            return new BurrowShape.Result(mask, 0, 0);
        }

        private static BurrowNav Nav(BurrowShape.Result shape, params NavRect[] blocked)
        {
            return new BurrowNav(shape, k_Ppu, blocked, k_Clearance, k_Step, k_TurnPenalty);
        }

        private static void AssertAxisAligned(Vector2 from, IReadOnlyList<Vector2> path)
        {
            Vector2 p = from;

            foreach (Vector2 q in path)
            {
                Assert.That(Math.Abs(q.X - p.X) < 1e-4f || Math.Abs(q.Y - p.Y) < 1e-4f, Is.True, $"대각선 {p} → {q}");
                p = q;
            }
        }

        private static int Turns(Vector2 from, IReadOnlyList<Vector2> path)
        {
            int turns = 0;
            bool? horizontal = null;
            Vector2 p = from;

            foreach (Vector2 q in path)
            {
                bool h = Math.Abs(q.Y - p.Y) < 1e-4f;

                if (horizontal.HasValue && horizontal.Value != h)
                {
                    turns++;
                }

                horizontal = h;
                p = q;
            }

            return turns;
        }

        [Test]
        public void FindPath_OpenRoom_IsOneStraightSegment()
        {
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -10f, 10f, 0f)));
            Vector2 from = new Vector2(1f, -1f);
            Vector2 to = new Vector2(6f, -1f);

            List<Vector2> path = nav.FindPath(from, to);

            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(Vector2.Distance(path[0], to), Is.LessThan(1e-3f));
        }

        [Test]
        public void FindPath_AroundBlockedRect_StaysAxisAlignedAndOutside()
        {
            NavRect pillar = new NavRect(4f, -8f, 6f, -2f);
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -10f, 10f, 0f)), pillar);
            Vector2 from = new Vector2(1f, -5f);
            Vector2 to = new Vector2(9f, -5f);

            List<Vector2> path = nav.FindPath(from, to);

            Assert.That(path, Is.Not.Empty);
            Assert.That(Vector2.Distance(path[path.Count - 1], to), Is.LessThan(1e-3f));
            AssertAxisAligned(from, path);
            Vector2 p = from;

            foreach (Vector2 q in path)
            {
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    Assert.That(pillar.Contains(Vector2.Lerp(p, q, t), 0f), Is.False);
                }

                p = q;
            }

            Assert.That(Turns(from, path), Is.EqualTo(2));
        }

        // ㄱ자 방: 한쪽 팔 끝에서 다른 팔 끝까지 꺾임 1번
        [Test]
        public void FindPath_LRoom_TurnsOnce()
        {
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -2f, 10f, 0f), (8f, -10f, 10f, 0f)));
            Vector2 from = new Vector2(1f, -1f);
            Vector2 to = new Vector2(9f, -9f);

            List<Vector2> path = nav.FindPath(from, to);

            AssertAxisAligned(from, path);
            Assert.That(Turns(from, path), Is.EqualTo(1));
            Assert.That(BurrowNav.Length(from, path), Is.EqualTo(16f).Within(1e-3f));
        }

        [Test]
        public void IsWalkable_KeepsClearanceFromWall()
        {
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -10f, 10f, 0f)));

            Assert.That(nav.IsWalkable(new Vector2(0.2f, -5f)), Is.False);
            Assert.That(nav.IsWalkable(new Vector2(0.4f, -5f)), Is.True);
        }

        [Test]
        public void FindPath_Unreachable_ReturnsEmpty()
        {
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -10f, 4f, 0f), (6f, -10f, 10f, 0f)));

            Assert.That(nav.FindPath(new Vector2(1f, -5f), new Vector2(9f, -5f)), Is.Empty);
        }

        // 막힌 곳(계산대 뒤 웜뱃 자리)에서 출발하면 가장 가까운 걷는 점으로 곧장 나온다
        [Test]
        public void FindPath_FromBlockedStart_StepsOutStraight()
        {
            NavRect counter = new NavRect(3f, -5.4f, 7f, -4.6f);
            BurrowNav nav = Nav(Mask(10f, 10f, (0f, -10f, 10f, 0f)), counter);
            Vector2 from = new Vector2(5f, -5f);
            Vector2 to = new Vector2(5f, -9f);

            List<Vector2> path = nav.FindPath(from, to);

            Assert.That(path, Is.Not.Empty);
            AssertAxisAligned(from, path);
            Assert.That(Vector2.Distance(path[path.Count - 1], to), Is.LessThan(1e-3f));
        }
    }
}
