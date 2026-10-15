using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 · 설계 61: 낚시터 배치의 단일 출처. 낚시터는 지하 호수 한 굴이다: 굴 칸은 보이는 섬 뭍(FishingIslandTable land) 둘레에 lakeMargin을 더한 칸의 합이고,
    // 걷는 땅은 닿은 섬 뭍 + 그 섬으로 놓은 다리 네모, 그 밖의 굴 바닥은 모두 물이다(보이기만 하는 섬 뭍은 땅 그림이지만 걷지 못한다).
    // 웜뱃은 선 땅 네모의 물에 닿는 변(물가)에서 물 쪽을 보고 던진다. 좌표는 낚시터 원점(구멍 위 가운데) 기준 유닛, y 위
    public sealed class FishingLayout
    {
        // 물가 바깥을 물인지 살피는 거리 · 깊이를 재는 걸음 · 물가에 바짝 붙은 뭍끼리 사이의 최소 깊이
        private const float k_Probe = 0.15f;
        private const float k_DepthStep = 0.1f;
        private const float k_MinDepth = 0.3f;

        private readonly FishingConfigTable m_config;
        private readonly Dictionary<int, FishingIslandTable> m_islands = new Dictionary<int, FishingIslandTable>();
        private readonly List<NavRect> m_lands = new List<NavRect>();
        private readonly List<NavRect> m_bridges = new List<NavRect>();
        private readonly List<NavRect> m_walkable = new List<NavRect>();
        private IReadOnlyCollection<int> m_visible = new int[0];
        private IReadOnlyCollection<int> m_reached = new int[0];

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 광장으로 가는 구멍(0번 섬 위 가운데, 빵집 · 농장 구멍과 같은 자리)
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        public Vector2 HoleInside => new Vector2(0f, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        // 땅 그림이 되는 뭍(보이는 섬 모두) · 놓인 다리 · 걷는 땅(닿은 뭍 + 놓인 다리)
        public IReadOnlyList<NavRect> Lands => m_lands;
        public IReadOnlyList<NavRect> Bridges => m_bridges;
        public IReadOnlyList<NavRect> Walkable => m_walkable;
        public IEnumerable<FishingIslandTable> Islands => m_islands.Values.OrderBy(island => island.Index);

        public FishingLayout(TableSet tables)
        {
            m_config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Cells = new CellMetrics(tables);

            foreach (FishingIslandTable island in tables.GetAll<FishingIslandTable>())
            {
                m_islands[island.Index] = island;
            }

            Rebuild(new[] { 0 }, new[] { 0 });
        }

        public FishingIslandTable Island(int index)
        {
            return m_islands[index];
        }

        public bool HasIsland(int index)
        {
            return m_islands.ContainsKey(index);
        }

        public NavRect LandOf(int index)
        {
            return m_islands[index].Land.ToRect();
        }

        public NavRect BridgeOf(int index)
        {
            return m_islands[index].Bridge.ToRect();
        }

        // 보이는 섬 · 닿은 섬으로 굴 · 땅 · 길을 다시 만든다
        public void Rebuild(IReadOnlyCollection<int> visible, IReadOnlyCollection<int> reached)
        {
            m_visible = visible;
            m_reached = reached;
            HashSet<Cell> cells = new HashSet<Cell>();
            m_lands.Clear();
            m_bridges.Clear();
            m_walkable.Clear();

            foreach (int index in visible.OrderBy(i => i))
            {
                NavRect land = LandOf(index);
                m_lands.Add(land);
                AddCells(cells, land, (float)m_config.LakeMargin);

                if (index > 0 && reached.Contains(index))
                {
                    NavRect bridge = BridgeOf(index);
                    m_bridges.Add(bridge);
                    AddCells(cells, bridge, 0f);
                }

                if (reached.Contains(index))
                {
                    m_walkable.Add(land);
                }
            }

            m_walkable.AddRange(m_bridges);
            Shape = Cells.Build(cells);
            Nav = new BurrowNav(Shape, new NavRect[0], m_walkable);
        }

        // 네모(둘레 margin을 더해)에 걸치는 칸을 모두 판다. 줄은 0(입구 줄)부터
        private void AddCells(HashSet<Cell> cells, NavRect rect, float margin)
        {
            float left = rect.XMin - margin;
            float right = rect.XMax + margin;
            float top = rect.YMax + margin;
            float bottom = rect.YMin - margin;
            int firstCol = (int)Math.Floor(left / Cells.CellWidth);
            int lastCol = (int)Math.Ceiling(right / Cells.CellWidth) - 1;

            for (int row = 0; -Cells.RowTop(row) > bottom; row++)
            {
                if (-Cells.RowTop(row + 1) >= top)
                {
                    continue;
                }

                for (int col = firstCol; col <= lastCol; col++)
                {
                    cells.Add(new Cell(col, row));
                }
            }
        }

        // 그 점이 물인가: 굴 바닥이면서 어느 뭍 · 다리에도 들지 않는다
        public bool IsWater(Vector2 p)
        {
            return BurrowNav.IsFloor(Shape, p) && !m_lands.Any(land => Inside(land, p)) && !m_bridges.Any(bridge => Inside(bridge, p));
        }

        // 그 점이 선 섬(뭍 또는 그 섬으로 놓은 다리 위). 어디에도 안 들면 가장 가까운 뭍의 섬
        public FishingIslandTable IslandAt(Vector2 p, out bool onBridge)
        {
            onBridge = false;

            foreach (int index in m_reached)
            {
                if (Inside(LandOf(index), p))
                {
                    return m_islands[index];
                }
            }

            foreach (int index in m_reached.Where(i => i > 0))
            {
                if (Inside(BridgeOf(index), p))
                {
                    onBridge = true;
                    return m_islands[index];
                }
            }

            return m_islands[m_reached.OrderBy(index => LandOf(index).DistanceTo(p)).First()];
        }

        // p에서 가장 가까운 물가(선 땅 네모의 네 변 중 바깥이 물인 변의 점)와 그 변에서 물 안쪽으로 향하는 단위 벡터
        public Vector2 Shore(Vector2 p, out Vector2 inward)
        {
            float best = float.MaxValue;
            Vector2 shore = p;
            inward = new Vector2(0f, -1f);
            NavRect fallback = m_walkable.OrderBy(rect => rect.DistanceTo(p)).First();

            foreach (NavRect rect in m_walkable.Where(rect => Inside(rect, p)).DefaultIfEmpty(fallback))
            {
                float x = Math.Clamp(p.X, rect.XMin, rect.XMax);
                float y = Math.Clamp(p.Y, rect.YMin, rect.YMax);
                (Vector2 point, Vector2 normal)[] edges =
                {
                    (new Vector2(x, rect.YMin), new Vector2(0f, -1f)),
                    (new Vector2(x, rect.YMax), new Vector2(0f, 1f)),
                    (new Vector2(rect.XMin, y), new Vector2(-1f, 0f)),
                    (new Vector2(rect.XMax, y), new Vector2(1f, 0f)),
                };

                foreach ((Vector2 point, Vector2 normal) in edges)
                {
                    float distance = Vector2.Distance(p, point);

                    if (distance < best && IsWater(point + normal * k_Probe))
                    {
                        best = distance;
                        shore = point;
                        inward = normal;
                    }
                }
            }

            if (best == float.MaxValue)
            {
                shore = new Vector2(Math.Clamp(p.X, fallback.XMin, fallback.XMax), fallback.YMin);
            }

            return shore;
        }

        // 그 물가에서 물 안쪽으로 물이 이어지는 거리(castReach까지)
        public float Depth(Vector2 shore, Vector2 inward)
        {
            float reach = (float)m_config.CastReach;
            float depth = 0f;

            while (depth + k_DepthStep <= reach && IsWater(shore + inward * (depth + k_DepthStep)))
            {
                depth += k_DepthStep;
            }

            return Math.Max(k_MinDepth, depth);
        }

        // p에서 본 물가에서 물 안쪽으로 k(0 = 물가, 1 = 깊이 끝)만큼 들어간 점
        public Vector2 Deep(Vector2 p, float k)
        {
            return Deep(p, k, out _);
        }

        public Vector2 Deep(Vector2 p, float k, out Vector2 inward)
        {
            Vector2 shore = Shore(p, out inward);
            return shore + inward * (Depth(shore, inward) * k);
        }

        public static bool Inside(NavRect rect, Vector2 p)
        {
            return p.X >= rect.XMin && p.X <= rect.XMax && p.Y >= rect.YMin && p.Y <= rect.YMax;
        }
    }
}
