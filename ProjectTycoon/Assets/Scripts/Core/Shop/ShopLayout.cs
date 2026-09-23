using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 3·4·6·7장: 가게 배치의 단일 출처. 칸 → 사물 자리, 구멍, 줄 자리, 서는 자리, 굴 마스크, 걷는 땅.
    // 배치 숫자는 아트 크기에서 나온 값이라 상수로 두고(굴 모양의 아치 크기와 같은 방식) 화면도 이 값으로 사물을 놓는다.
    // 좌표는 가게 원점(입구 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class ShopLayout
    {
        public const float k_PixelsPerUnit = 40f;
        public const int k_RoundRadius = 12;
        // 사물 밑변(그림 발끝): 진열대·오븐은 칸 가운데에서, 계산대·웜뱃은 계산대 줄 윗변에서 아래로
        public const float k_ShelfDrop = 0.65f;
        public const float k_OvenDrop = 0.85f;
        public const float k_CounterDrop = 1.55f;
        public const float k_WombatDrop = 2.45f;

        // 사물 바닥 자리: 밑변에서 위로 depth, 가운데에서 좌우로 half
        private const float k_ShelfHalf = 0.6f;
        private const float k_OvenHalf = 0.65f;
        private const float k_CounterHalf = 1f;
        private const float k_WombatHalf = 0.4f;
        private const float k_ObjectDepth = 0.45f;
        private const float k_CounterDepth = 0.5f;
        // 웜뱃 바닥 자리는 계산대 밑변까지(둘 사이 틈으로 손님이 지나가지 않게)
        private const float k_WombatDepth = 0.9f;
        // 걷는 땅: 몸 반 폭, 격자 간격, 꺾임 벌점
        private const float k_Clearance = 0.3f;
        private const float k_Step = 0.2f;
        private const float k_TurnPenalty = 0.6f;
        // 서는 자리: 진열대 옆(가운데에서), 앞(아래), 웜뱃은 오븐 옆
        private const float k_SideOffset = 1.1f;
        private const float k_FrontOffsetX = 0.55f;
        private const float k_FrontOffsetY = 0.7f;
        private const float k_OvenSpotOffset = 1.25f;
        // 줄: 간격, 머리 자리(계산대 밑변 기준), 서는 자리와 떨어질 거리, 구멍 아래와 떨어질 거리
        private const float k_QueueSpacing = 0.8f;
        private const float k_SpotGap = 0.6f;
        private const float k_HoleGap = 1f;
        private const float k_OverflowDistance = 2f;
        private static readonly Vector2 k_QueueHeadOffset = new Vector2(0.6f, 1.1f);

        private readonly GameConfig.ShopConfig m_config;
        private readonly List<Vector2> m_queueSlots = new List<Vector2>();
        private readonly Dictionary<Cell, List<Vector2>> m_shelfSpots = new Dictionary<Cell, List<Vector2>>();
        private readonly Dictionary<Cell, Vector2> m_ovenSpots = new Dictionary<Cell, Vector2>();

        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        public IReadOnlyList<Vector2> QueueSlots => m_queueSlots;
        // 구멍 안(나타나는 곳)과 구멍 아래 바닥(내려앉는 곳)
        public Vector2 HoleInside => new Vector2(0f, -1.6f);
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        public Vector2 CounterBase => new Vector2(0f, -RowTop(BurrowGrid.k_CounterRow) - k_CounterDrop);
        public Vector2 WombatHome => new Vector2(0f, -RowTop(BurrowGrid.k_CounterRow) - k_WombatDrop);

        public ShopLayout(GameConfig.ShopConfig config)
        {
            m_config = config;
        }

        public float RowTop(int row)
        {
            return row <= 0 ? 0f : (float)(m_config.EntranceHeight + (row - 1) * m_config.CellHeight);
        }

        public Vector2 CellCenter(Cell cell)
        {
            float height = (float)(cell.Row == 0 ? m_config.EntranceHeight : m_config.CellHeight);
            return new Vector2((float)((cell.Col + 0.5) * m_config.CellWidth), -(RowTop(cell.Row) + height * 0.5f));
        }

        public Vector2 ShelfBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_ShelfDrop);
        }

        public Vector2 OvenBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_OvenDrop);
        }

        public IReadOnlyList<Vector2> ShelfSpots(Cell cell)
        {
            return m_shelfSpots[cell];
        }

        public Vector2 OvenSpot(Cell cell)
        {
            return m_ovenSpots[cell];
        }

        // 진열대 옆자리는 진열대 쪽을, 앞자리는 위를 본다
        public Facing ShelfFacing(Cell cell, Vector2 spot)
        {
            Vector2 shelf = ShelfBase(cell);

            if (Math.Abs(spot.X - shelf.X) < 0.3f)
            {
                return Facing.Up;
            }

            return shelf.X < spot.X ? Facing.Left : Facing.Right;
        }

        // 줄 머리는 계산대(아래)를, 나머지는 앞사람 쪽을 본다
        public Facing QueueFacing(int index)
        {
            return index == 0 ? Facing.Down : Mover.FacingOf(m_queueSlots[index - 1] - m_queueSlots[index]);
        }

        // 서는 자리가 다 찼을 때: 진열대에서 가까운 빈 걷는 점
        public Vector2 OverflowSpot(Cell cell, Func<Vector2, bool> taken)
        {
            Vector2 around = m_shelfSpots[cell].Count > 0 ? m_shelfSpots[cell][0] : ShelfBase(cell) - new Vector2(0f, k_FrontOffsetY);
            return Nav.TryNearestFree(around, p => taken(p) || NearQueue(p), k_OverflowDistance, out Vector2 spot) ? spot : around;
        }

        public void Rebuild(IReadOnlyCollection<Cell> cells, IEnumerable<Cell> shelfCells, IEnumerable<Cell> ovenCells, int queueCapacity)
        {
            int unit = (int)k_PixelsPerUnit;
            Shape = BurrowShape.Build(cells, (int)Math.Round(m_config.CellWidth * unit), (int)Math.Round(m_config.CellHeight * unit),
                (int)Math.Round(m_config.EntranceHeight * unit), k_RoundRadius);

            List<NavRect> blocked = new List<NavRect>();
            List<Cell> shelves = new List<Cell>(shelfCells);
            List<Cell> ovens = new List<Cell>(ovenCells);

            foreach (Cell cell in shelves)
            {
                blocked.Add(Footprint(ShelfBase(cell), k_ShelfHalf, k_ObjectDepth));
            }

            foreach (Cell cell in ovens)
            {
                blocked.Add(Footprint(OvenBase(cell), k_OvenHalf, k_ObjectDepth));
            }

            blocked.Add(Footprint(CounterBase, k_CounterHalf, k_CounterDepth));
            blocked.Add(Footprint(WombatHome, k_WombatHalf, k_WombatDepth));
            Nav = new BurrowNav(Shape, k_PixelsPerUnit, blocked, k_Clearance, k_Step, k_TurnPenalty);

            BuildQueue(queueCapacity);
            BuildShelfSpots(shelves);
            m_ovenSpots.Clear();

            foreach (Cell cell in ovens)
            {
                Vector2 oven = OvenBase(cell);
                Vector2 spot = Nav.Snap(oven + new Vector2(ToCenter(oven) * k_OvenSpotOffset, 0.05f));
                m_ovenSpots[cell] = Nav.IsWalkable(spot) || !Nav.TryNearestFree(spot, _ => false, k_OverflowDistance, out Vector2 near) ? spot : near;
            }
        }

        // 6장: 머리(등록기 앞)에서 왼쪽으로 간격마다. 막히면 아래(계산대 왼쪽) → 위 → 오른쪽으로 꺾고, 모두 막히면 끝자리에 겹쳐 선다
        private void BuildQueue(int capacity)
        {
            m_queueSlots.Clear();
            Vector2 head = Nav.Snap(CounterBase + k_QueueHeadOffset);
            m_queueSlots.Add(Nav.IsWalkable(head) || !Nav.TryNearestFree(head, _ => false, k_OverflowDistance, out Vector2 near) ? head : near);
            Vector2[] turns = { new Vector2(-1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(1f, 0f) };
            Vector2 dir = turns[0];

            while (m_queueSlots.Count < capacity)
            {
                Vector2 prev = m_queueSlots[m_queueSlots.Count - 1];
                bool placed = false;

                foreach (Vector2 d in Prepend(dir, turns))
                {
                    Vector2 next = Nav.Snap(prev + d * k_QueueSpacing);

                    if (d == -dir || !Nav.IsLineWalkable(prev, next) || Vector2.Distance(next, HoleFloor) < k_HoleGap || CrowdsQueue(next))
                    {
                        continue;
                    }

                    m_queueSlots.Add(next);
                    dir = d;
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    break;
                }
            }

            while (m_queueSlots.Count < capacity)
            {
                m_queueSlots.Add(m_queueSlots[m_queueSlots.Count - 1]);
            }
        }

        // 7장: 가운데 쪽 옆 → 벽 쪽 옆 → 앞 두 자리. 걷는 땅이 아니거나 줄·구멍 아래·다른 진열대 자리와 가까우면 뺀다
        private void BuildShelfSpots(List<Cell> shelves)
        {
            m_shelfSpots.Clear();
            List<Vector2> all = new List<Vector2>();

            foreach (Cell cell in shelves)
            {
                Vector2 shelf = ShelfBase(cell);
                float toCenter = ToCenter(shelf);
                Vector2[] candidates =
                {
                    new Vector2(shelf.X + toCenter * k_SideOffset, shelf.Y - 0.05f),
                    new Vector2(shelf.X - toCenter * k_SideOffset, shelf.Y - 0.05f),
                    new Vector2(shelf.X + toCenter * k_FrontOffsetX, shelf.Y - k_FrontOffsetY),
                    new Vector2(shelf.X - toCenter * k_FrontOffsetX, shelf.Y - k_FrontOffsetY),
                };
                List<Vector2> spots = new List<Vector2>();

                foreach (Vector2 candidate in candidates)
                {
                    // x는 가까운 격자, y는 아래 격자(사물보다 앞에 그려지게)
                    Vector2 spot = new Vector2(Nav.Snap(candidate).X, (float)Math.Floor(candidate.Y / Nav.Step + 1e-4) * Nav.Step);

                    if (Nav.IsWalkable(spot) && !NearQueue(spot) && Vector2.Distance(spot, HoleFloor) >= k_SpotGap && !Near(all, spot, k_Step))
                    {
                        spots.Add(spot);
                        all.Add(spot);
                    }
                }

                m_shelfSpots[cell] = spots;
            }
        }

        private bool NearQueue(Vector2 p)
        {
            return Near(m_queueSlots, p, k_SpotGap);
        }

        private bool CrowdsQueue(Vector2 p)
        {
            return Near(m_queueSlots, p, k_QueueSpacing - 0.01f);
        }

        private static bool Near(List<Vector2> points, Vector2 p, float distance)
        {
            foreach (Vector2 q in points)
            {
                if (Vector2.Distance(p, q) < distance)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Vector2> Prepend(Vector2 first, Vector2[] rest)
        {
            yield return first;

            foreach (Vector2 d in rest)
            {
                if (d != first)
                {
                    yield return d;
                }
            }
        }

        private static float ToCenter(Vector2 p)
        {
            return p.X < 0f ? 1f : -1f;
        }

        private static NavRect Footprint(Vector2 bottom, float half, float depth)
        {
            return new NavRect(bottom.X - half, bottom.Y, bottom.X + half, bottom.Y + depth);
        }
    }
}
