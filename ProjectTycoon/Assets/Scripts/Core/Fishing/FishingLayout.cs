using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터 배치의 단일 출처. 광장처럼 고정 방(BurrowShape.Room)이고, 물길(축 정렬 꺾은선 + 폭)은 걸을 수 없는 막힌 사각형이다.
    // 물고기는 물길 길이 s(첫 점 0 → 끝 Length)를 따라 헤엄친다. 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위.
    // 설계 45: 표의 꺾은선은 끝까지 팠을 때의 길(Plan)이고, 물은 판 길이(Length)까지만이다. 판 끝은 막혀 있다. SetDug가 물길 · 막힌 사각형 · 길찾기를 다시 만든다
    public sealed class FishingLayout
    {
        private readonly Vector2[] m_plan;
        private readonly float[] m_planStarts;
        private Vector2[] m_points;
        private float[] m_starts;

        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; private set; }
        // 광장으로 가는 구멍(첫 줄 가운데, 빵집 · 농장 구멍과 같은 자리)
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        // 판 데까지의 꺾은선(끝 점 = 막다른 끝)
        public IReadOnlyList<Vector2> Stream => m_points;
        public float StreamWidth { get; }
        public float Length { get; private set; }
        public float PlanLength { get; }
        // 물길 선분마다 폭을 더한 사각형(막힌 땅 · 물 그림)
        public IReadOnlyList<NavRect> StreamRects { get; private set; }
        public Vector2 HutSpot { get; }

        public FishingLayout(TableSet tables)
        {
            FishingConfigTable config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Shape = BurrowShape.Room(tables, config.Cols, config.Rows);
            StreamWidth = (float)config.StreamWidth;
            m_plan = config.Stream.Select(p => new Vector2((float)p.X, (float)p.Y)).ToArray();
            m_planStarts = Starts(m_plan);
            PlanLength = m_planStarts[m_planStarts.Length - 1];
            HutSpot = new Vector2((float)config.HutX, (float)config.HutY);
            SetDug((float)config.DugStart);
        }

        // 판 길이를 정한다(정해진 길 끝까지)
        public void SetDug(float length)
        {
            Length = Math.Clamp(length, 0f, PlanLength);
            List<Vector2> points = new List<Vector2> { m_plan[0] };

            for (int i = 1; i < m_plan.Length && m_planStarts[i - 1] < Length; i++)
            {
                points.Add(m_planStarts[i] <= Length ? m_plan[i] : PlanPointAt(Length));
            }

            m_points = points.ToArray();
            m_starts = Starts(m_points);
            List<NavRect> rects = new List<NavRect>();
            float half = StreamWidth / 2f;

            for (int i = 1; i < m_points.Length; i++)
            {
                Vector2 a = m_points[i - 1];
                Vector2 b = m_points[i];
                rects.Add(new NavRect(Math.Min(a.X, b.X) - half, Math.Min(a.Y, b.Y) - half, Math.Max(a.X, b.X) + half, Math.Max(a.Y, b.Y) + half));
            }

            StreamRects = rects;
            Nav = new BurrowNav(Shape, rects);
        }

        // 물길 길이 s의 자리(0 = 첫 점, Length = 막다른 끝)
        public Vector2 PointAt(float s)
        {
            return At(m_points, m_starts, Math.Clamp(s, 0f, Length));
        }

        // 정해진 길(끝까지 판 물길) 위 s의 자리
        public Vector2 PlanPointAt(float s)
        {
            return At(m_plan, m_planStarts, Math.Clamp(s, 0f, PlanLength));
        }

        private static Vector2 At(Vector2[] points, float[] starts, float s)
        {
            for (int i = 1; i < points.Length; i++)
            {
                if (s <= starts[i])
                {
                    float segment = starts[i] - starts[i - 1];
                    return Vector2.Lerp(points[i - 1], points[i], segment <= 0f ? 0f : (s - starts[i - 1]) / segment);
                }
            }

            return points[points.Length - 1];
        }

        private static float[] Starts(Vector2[] points)
        {
            float[] starts = new float[points.Length];

            for (int i = 1; i < points.Length; i++)
            {
                starts[i] = starts[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            return starts;
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
