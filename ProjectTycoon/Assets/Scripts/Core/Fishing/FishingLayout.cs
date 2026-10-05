using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터 배치의 단일 출처. 광장처럼 고정 방(BurrowShape.Room)이고, 물길(축 정렬 꺾은선 + 폭)은 걸을 수 없는 막힌 사각형이다.
    // 물고기는 물길 길이 s(첫 점 0 → 끝 점 Length)를 따라 헤엄친다. 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class FishingLayout
    {
        private readonly Vector2[] m_points;
        private readonly float[] m_starts;

        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; }
        // 광장으로 가는 구멍(첫 줄 가운데, 빵집 · 농장 구멍과 같은 자리)
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        public IReadOnlyList<Vector2> Stream => m_points;
        public float StreamWidth { get; }
        public float Length { get; }
        // 물길 선분마다 폭을 더한 사각형(막힌 땅 · 물 그림)
        public IReadOnlyList<NavRect> StreamRects { get; }
        public Vector2 HutSpot { get; }

        public FishingLayout(TableSet tables)
        {
            FishingConfigTable config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Shape = BurrowShape.Room(tables, config.Cols, config.Rows);
            StreamWidth = (float)config.StreamWidth;
            m_points = new Vector2[config.Stream.Length];
            m_starts = new float[config.Stream.Length];
            List<NavRect> rects = new List<NavRect>();
            float half = StreamWidth / 2f;

            for (int i = 0; i < m_points.Length; i++)
            {
                m_points[i] = new Vector2((float)config.Stream[i].X, (float)config.Stream[i].Y);

                if (i == 0)
                {
                    continue;
                }

                Vector2 a = m_points[i - 1];
                Vector2 b = m_points[i];
                m_starts[i] = m_starts[i - 1] + Vector2.Distance(a, b);
                rects.Add(new NavRect(Math.Min(a.X, b.X) - half, Math.Min(a.Y, b.Y) - half, Math.Max(a.X, b.X) + half, Math.Max(a.Y, b.Y) + half));
            }

            Length = m_starts[m_starts.Length - 1];
            StreamRects = rects;
            Nav = new BurrowNav(Shape, rects);
            HutSpot = new Vector2((float)config.HutX, (float)config.HutY);
        }

        // 물길 길이 s의 자리(0 = 첫 점, Length = 끝 점)
        public Vector2 PointAt(float s)
        {
            s = Math.Clamp(s, 0f, Length);

            for (int i = 1; i < m_points.Length; i++)
            {
                if (s <= m_starts[i])
                {
                    float segment = m_starts[i] - m_starts[i - 1];
                    return Vector2.Lerp(m_points[i - 1], m_points[i], segment <= 0f ? 0f : (s - m_starts[i - 1]) / segment);
                }
            }

            return m_points[m_points.Length - 1];
        }

        // 물길 가운데 선까지 거리
        public float DistanceToStream(Vector2 p)
        {
            float best = float.MaxValue;

            for (int i = 1; i < m_points.Length; i++)
            {
                best = Math.Min(best, DistanceToSegment(p, m_points[i - 1], m_points[i]));
            }

            return best;
        }

        // 가운데 선에서 p에 가장 가까운 점
        public Vector2 NearestOnStream(Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 found = m_points[0];

            for (int i = 1; i < m_points.Length; i++)
            {
                Vector2 q = Closest(p, m_points[i - 1], m_points[i]);
                float d = Vector2.Distance(p, q);

                if (d < best)
                {
                    best = d;
                    found = q;
                }
            }

            return found;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            return Vector2.Distance(p, Closest(p, a, b));
        }

        private static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            float t = lengthSquared <= 0f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f);
            return a + ab * t;
        }
    }
}
