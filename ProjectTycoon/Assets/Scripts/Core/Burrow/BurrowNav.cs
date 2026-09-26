using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 막힌 사각형(가게 원점 기준 유닛, y 위). 사물 바닥 자리
    public readonly struct NavRect
    {
        public float XMin { get; }
        public float YMin { get; }
        public float XMax { get; }
        public float YMax { get; }

        public NavRect(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public bool Intersects(NavRect other)
        {
            return XMin < other.XMax && other.XMin < XMax && YMin < other.YMax && other.YMin < YMax;
        }

        public bool Contains(Vector2 p, float margin)
        {
            return p.X > XMin - margin && p.X < XMax + margin && p.Y > YMin - margin && p.Y < YMax + margin;
        }
    }

    // 손님 동선 설계 v0.2 4장: 굴 마스크 위 격자(간격 step)의 걷는 땅과 4방향 A*.
    // 걷는 점 = 굴 바닥이고 벽·막힌 사각형에서 clearance 이상 떨어진 격자 점. 비용 = 걸은 거리 + 꺾을 때마다 turnPenalty
    public sealed class BurrowNav
    {
        // 걷는 땅: 몸 반 폭(벽·사물에서 이만큼 떨어진 점만), 격자 간격, 꺾임 벌점
        public const float k_Clearance = 0.3f;
        public const float k_Step = 0.2f;
        public const float k_TurnPenalty = 0.6f;
        private const int k_None = 4;
        private const int k_EscapeSteps = 16;
        private static readonly int[] k_Dx = { 1, -1, 0, 0 };
        private static readonly int[] k_Dy = { 0, 0, 1, -1 };

        private readonly float m_step;
        private readonly float m_turnPenalty;
        private readonly int m_minX;
        private readonly int m_minY;
        private readonly int m_width;
        private readonly int m_height;
        private readonly bool[] m_walkable;
        // A* 작업 배열. 탐색마다 도장 값으로 지운 것처럼 쓴다
        private readonly float[] m_g;
        private readonly int[] m_parent;
        private readonly int[] m_seen;
        private readonly int[] m_closed;
        private readonly MinHeap m_open = new MinHeap();
        private int m_search;

        public float Step => m_step;

        public BurrowNav(BurrowShape.Result shape, IReadOnlyList<NavRect> blocked)
        {
            float pixelsPerUnit = BurrowShape.k_PixelsPerUnit;
            float clearance = k_Clearance;
            float step = k_Step;
            m_step = step;
            m_turnPenalty = k_TurnPenalty;
            float xMin = shape.OriginX / pixelsPerUnit;
            float xMax = (shape.OriginX + shape.Width) / pixelsPerUnit;
            float yMax = -shape.OriginY / pixelsPerUnit;
            float yMin = -(shape.OriginY + shape.Height) / pixelsPerUnit;
            m_minX = (int)Math.Ceiling(xMin / step);
            m_minY = (int)Math.Ceiling(yMin / step);
            m_width = (int)Math.Floor(xMax / step) - m_minX + 1;
            m_height = (int)Math.Floor(yMax / step) - m_minY + 1;
            m_walkable = new bool[m_width * m_height];

            for (int j = 0; j < m_height; j++)
            {
                for (int i = 0; i < m_width; i++)
                {
                    Vector2 p = NodePosition(i, j);
                    m_walkable[j * m_width + i] = OnFloor(shape, pixelsPerUnit, p, clearance) && !IsBlocked(blocked, p, clearance);
                }
            }

            int states = m_width * m_height * 5;
            m_g = new float[states];
            m_parent = new int[states];
            m_seen = new int[states];
            m_closed = new int[states];
        }

        public Vector2 Snap(Vector2 p)
        {
            return new Vector2((float)Math.Round(p.X / m_step) * m_step, (float)Math.Round(p.Y / m_step) * m_step);
        }

        public bool IsWalkable(Vector2 p)
        {
            return TryNode(p, out int i, out int j) && m_walkable[j * m_width + i];
        }

        // 두 격자 점을 잇는 가로·세로 선분의 모든 격자 점이 걷는 땅인가
        public bool IsLineWalkable(Vector2 a, Vector2 b)
        {
            if (!TryNode(a, out int ai, out int aj) || !TryNode(b, out int bi, out int bj) || (ai != bi && aj != bj))
            {
                return false;
            }

            int steps = Math.Max(Math.Abs(bi - ai), Math.Abs(bj - aj));

            for (int s = 0; s <= steps; s++)
            {
                int i = ai + Math.Sign(bi - ai) * s;
                int j = aj + Math.Sign(bj - aj) * s;

                if (!m_walkable[j * m_width + i])
                {
                    return false;
                }
            }

            return true;
        }

        // around에서 가까운 걷는 점부터 넓혀 가며 taken이 아닌 첫 점. maxDistance(맨해튼) 안에 없으면 false
        public bool TryNearestFree(Vector2 around, Func<Vector2, bool> taken, float maxDistance, out Vector2 found)
        {
            int ci = (int)Math.Round(around.X / m_step) - m_minX;
            int cj = (int)Math.Round(around.Y / m_step) - m_minY;
            int radius = (int)Math.Ceiling(maxDistance / m_step);

            for (int r = 0; r <= radius; r++)
            {
                for (int di = -r; di <= r; di++)
                {
                    int dj = r - Math.Abs(di);

                    foreach (int sj in dj == 0 ? new[] { 0 } : new[] { dj, -dj })
                    {
                        int i = ci + di;
                        int j = cj + sj;

                        if (i < 0 || j < 0 || i >= m_width || j >= m_height || !m_walkable[j * m_width + i])
                        {
                            continue;
                        }

                        Vector2 p = NodePosition(i, j);

                        if (!taken(p))
                        {
                            found = p;
                            return true;
                        }
                    }
                }
            }

            found = around;
            return false;
        }

        // from에서 to까지 가로·세로로만 걷는 길. 돌려주는 점은 from 다음 꺾임점부터 to까지. 닿을 수 없으면 빈 목록
        public List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            List<Vector2> path = new List<Vector2>();

            if (!TryNode(from, out int si, out int sj) || !TryNode(to, out int ti, out int tj))
            {
                return path;
            }

            // 막힌 곳(계산대 뒤 웜뱃 자리 등)에서 나가고 들어오는 한 걸음
            Vector2 startNode = NodePosition(si, sj);
            Vector2 targetNode = NodePosition(ti, tj);
            bool startOut = !m_walkable[sj * m_width + si];
            bool targetIn = !m_walkable[tj * m_width + ti];

            if (startOut && !TryEscape(ref si, ref sj))
            {
                return path;
            }

            if (targetIn && !TryEscape(ref ti, ref tj))
            {
                return path;
            }

            List<int> nodes = Search(si + sj * m_width, ti + tj * m_width);

            if (nodes == null)
            {
                return path;
            }

            AddIfApart(path, from, startNode);

            if (startOut)
            {
                path.Add(NodePosition(nodes[0] % m_width, nodes[0] / m_width));
            }

            // 방향이 바뀌는 점만 남긴다
            for (int k = 1; k < nodes.Count; k++)
            {
                bool last = k == nodes.Count - 1;
                bool turn = !last && Direction(nodes[k - 1], nodes[k]) != Direction(nodes[k], nodes[k + 1]);

                if (last || turn)
                {
                    path.Add(NodePosition(nodes[k] % m_width, nodes[k] / m_width));
                }
            }

            if (targetIn)
            {
                path.Add(targetNode);
            }

            if (Vector2.DistanceSquared(path.Count > 0 ? path[path.Count - 1] : from, to) > 1e-6f)
            {
                path.Add(to);
            }

            return path;
        }

        public static float Length(Vector2 from, IReadOnlyList<Vector2> path)
        {
            float length = 0f;
            Vector2 p = from;

            foreach (Vector2 q in path)
            {
                length += Vector2.Distance(p, q);
                p = q;
            }

            return length;
        }

        // 격자 밖 시작점(웜뱃 제자리 등)에서 가장 가까운 격자 점으로 맞추는 짧은 한 걸음
        private static void AddIfApart(List<Vector2> path, Vector2 from, Vector2 node)
        {
            if (Vector2.DistanceSquared(from, node) > 1e-6f)
            {
                path.Add(node);
            }
        }

        private List<int> Search(int start, int target)
        {
            m_search++;
            m_open.Clear();
            int startState = start * 5 + k_None;
            m_g[startState] = 0f;
            m_parent[startState] = -1;
            m_seen[startState] = m_search;
            m_open.Push(Heuristic(start, target), startState);

            while (m_open.Count > 0)
            {
                int state = m_open.Pop();

                if (m_closed[state] == m_search)
                {
                    continue;
                }

                m_closed[state] = m_search;
                int node = state / 5;
                int dir = state % 5;

                if (node == target)
                {
                    return Rebuild(state);
                }

                int i = node % m_width;
                int j = node / m_width;

                for (int d = 0; d < 4; d++)
                {
                    int ni = i + k_Dx[d];
                    int nj = j + k_Dy[d];

                    if (ni < 0 || nj < 0 || ni >= m_width || nj >= m_height || !m_walkable[nj * m_width + ni])
                    {
                        continue;
                    }

                    int next = (nj * m_width + ni) * 5 + d;
                    float g = m_g[state] + m_step + (dir != k_None && dir != d ? m_turnPenalty : 0f);

                    if (m_closed[next] == m_search || (m_seen[next] == m_search && m_g[next] <= g))
                    {
                        continue;
                    }

                    m_seen[next] = m_search;
                    m_g[next] = g;
                    m_parent[next] = state;
                    m_open.Push(g + Heuristic(next / 5, target), next);
                }
            }

            return null;
        }

        private List<int> Rebuild(int state)
        {
            List<int> nodes = new List<int>();

            for (int s = state; s >= 0; s = m_parent[s])
            {
                nodes.Add(s / 5);
            }

            nodes.Reverse();
            return nodes;
        }

        private float Heuristic(int node, int target)
        {
            return (Math.Abs(node % m_width - target % m_width) + Math.Abs(node / m_width - target / m_width)) * m_step;
        }

        private int Direction(int a, int b)
        {
            int d = b - a;
            return d == 1 ? 0 : d == -1 ? 1 : d > 0 ? 2 : 3;
        }

        // 네 방향으로 곧장 가서 처음 만나는 걷는 점. 가장 가까운 쪽
        private bool TryEscape(ref int i, ref int j)
        {
            for (int s = 1; s <= k_EscapeSteps; s++)
            {
                for (int d = 0; d < 4; d++)
                {
                    int ni = i + k_Dx[d] * s;
                    int nj = j + k_Dy[d] * s;

                    if (ni >= 0 && nj >= 0 && ni < m_width && nj < m_height && m_walkable[nj * m_width + ni])
                    {
                        i = ni;
                        j = nj;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryNode(Vector2 p, out int i, out int j)
        {
            i = (int)Math.Round(p.X / m_step) - m_minX;
            j = (int)Math.Round(p.Y / m_step) - m_minY;
            return i >= 0 && j >= 0 && i < m_width && j < m_height;
        }

        private Vector2 NodePosition(int i, int j)
        {
            return new Vector2((m_minX + i) * m_step, (m_minY + j) * m_step);
        }

        // 가운데와 둘레 8점이 모두 굴 바닥인가(벽에서 clearance 이상)
        // 그 점이 굴 바닥 마스크 안인가(여유 없이). 설계 18 배치 검증
        public static bool IsFloor(BurrowShape.Result shape, Vector2 p)
        {
            return MaskAt(shape, BurrowShape.k_PixelsPerUnit, p);
        }

        private static bool OnFloor(BurrowShape.Result shape, float ppu, Vector2 p, float clearance)
        {
            if (!MaskAt(shape, ppu, p))
            {
                return false;
            }

            for (int k = 0; k < 8; k++)
            {
                double angle = k * Math.PI / 4d;
                Vector2 q = p + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * clearance;

                if (!MaskAt(shape, ppu, q))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MaskAt(BurrowShape.Result shape, float ppu, Vector2 p)
        {
            int x = (int)Math.Floor(p.X * ppu) - shape.OriginX;
            int y = (int)Math.Floor(-p.Y * ppu) - shape.OriginY;
            return x >= 0 && y >= 0 && x < shape.Width && y < shape.Height && shape.IsFloor(x, y);
        }

        private static bool IsBlocked(IReadOnlyList<NavRect> blocked, Vector2 p, float clearance)
        {
            foreach (NavRect rect in blocked)
            {
                if (rect.Contains(p, clearance))
                {
                    return true;
                }
            }

            return false;
        }

        // 우선순위 큐(최소 힙). .NET Standard 2.1에는 PriorityQueue가 없다
        private sealed class MinHeap
        {
            private readonly List<float> m_keys = new List<float>();
            private readonly List<int> m_values = new List<int>();

            public int Count => m_values.Count;

            public void Clear()
            {
                m_keys.Clear();
                m_values.Clear();
            }

            public void Push(float key, int value)
            {
                m_keys.Add(key);
                m_values.Add(value);
                int c = m_values.Count - 1;

                while (c > 0)
                {
                    int p = (c - 1) / 2;

                    if (m_keys[p] <= m_keys[c])
                    {
                        break;
                    }

                    Swap(c, p);
                    c = p;
                }
            }

            public int Pop()
            {
                int top = m_values[0];
                int last = m_values.Count - 1;
                Swap(0, last);
                m_keys.RemoveAt(last);
                m_values.RemoveAt(last);
                int c = 0;

                while (true)
                {
                    int l = c * 2 + 1;
                    int r = l + 1;
                    int m = c;

                    if (l < m_values.Count && m_keys[l] < m_keys[m])
                    {
                        m = l;
                    }

                    if (r < m_values.Count && m_keys[r] < m_keys[m])
                    {
                        m = r;
                    }

                    if (m == c)
                    {
                        break;
                    }

                    Swap(c, m);
                    c = m;
                }

                return top;
            }

            private void Swap(int a, int b)
            {
                (m_keys[a], m_keys[b]) = (m_keys[b], m_keys[a]);
                (m_values[a], m_values[b]) = (m_values[b], m_values[a]);
            }
        }
    }
}
