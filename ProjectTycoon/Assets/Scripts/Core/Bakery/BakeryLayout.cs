using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 3·4·6·7장: 가게 배치의 단일 출처. 칸 → 사물 자리, 구멍, 줄 자리, 서는 자리, 굴 마스크, 걷는 땅.
    // 배치 숫자는 아트 크기에서 나온 값이라 상수로 두고(굴 모양의 아치 크기와 같은 방식) 화면도 이 값으로 사물을 놓는다.
    // 좌표는 가게 원점(입구 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class BakeryLayout
    {
        // 사물 밑변(그림 발끝): 진열대·오븐은 칸 가운데에서, 계산대·웜뱃은 계산대 줄 윗변에서 아래로
        public const float k_ShelfDrop = 0.65f;
        public const float k_OvenDrop = 0.85f;
        public const float k_CounterDrop = 1.55f;
        public const float k_WombatDrop = 2.45f;
        public const float k_SlotDrop = 0.3f;

        // 사물 바닥 자리: 밑변에서 위로 depth, 가운데에서 좌우로 half
        private const float k_ShelfHalf = 0.6f;
        private const float k_OvenHalf = 0.65f;
        private const float k_CounterHalf = 1f;
        private const float k_WombatHalf = 0.4f;
        private const float k_ObjectDepth = 0.45f;
        private const float k_CounterDepth = 0.5f;
        // 웜뱃 바닥 자리는 계산대 밑변까지(둘 사이 틈으로 손님이 지나가지 않게)
        private const float k_WombatDepth = 0.9f;
        // 서는 자리: 진열대 옆(가운데에서), 앞(아래)
        private const float k_SideOffset = 1.1f;
        private const float k_FrontOffsetX = 0.55f;
        private const float k_FrontOffsetY = 0.7f;
        // 재고 표지판(2026-09-24): 진열대 벽 쪽 앞 모서리의 발끝. 옆으로 53px(PPU 80, 홀수)이라 진열대와 같은 칸 격자(2px)에 선다
        private const float k_SignOffsetX = 0.6625f;
        private const float k_SignDrop = 0.2f;
        // 벽 쪽 옆자리는 표지판 너머로(손님이 표지판에 가리지 않게). 기본 굴(벽 −3.375)에서 걷는 땅 끝 −3.0
        private const float k_SignClear = 0.2f;
        // 줄: 간격, 머리 자리(계산대 밑변 기준), 서는 자리와 떨어질 거리, 구멍 아래와 떨어질 거리
        // 머리 높이 0.9: 계산대 바로 위 걷는 줄(−5.0). 한 줄 위(−4.8)는 줄 손님이 진열대 아랫단을 가린다
        private const float k_QueueSpacing = 0.8f;
        private const float k_SpotGap = 0.6f;
        private const float k_HoleGap = 1f;
        private const float k_OverflowDistance = 2f;
        private static readonly Vector2 k_QueueHeadOffset = new Vector2(0.6f, 0.9f);

        private readonly double m_cellWidth;
        private readonly double m_cellHeight;
        private readonly double m_entranceHeight;
        private readonly List<Vector2> m_queueSlots = new List<Vector2>();
        private readonly Dictionary<Cell, List<Vector2>> m_shelfSpots = new Dictionary<Cell, List<Vector2>>();

        // 굴 칸 크기(유닛)
        public float CellWidth => (float)m_cellWidth;
        public float CellHeight => (float)m_cellHeight;
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 설계 09: 웜뱃이 걷는 땅. 손님 땅과 같되 계산대 뒤 웜뱃 자리를 막지 않는다
        public BurrowNav WombatNav { get; private set; }
        public IReadOnlyList<Vector2> QueueSlots => m_queueSlots;
        // 구멍 안(나타나는 곳)과 구멍 아래 바닥(내려앉는 곳)
        // 굴 환경 A2: 아치 구멍 밑변(= 띠 밑변) 바로 위에서 톡 나온다
        public Vector2 HoleInside => new Vector2(0f, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        public Vector2 CounterBase => new Vector2(0f, -RowTop(BurrowGrid.k_CounterRow) - k_CounterDrop);
        public Vector2 WombatHome => new Vector2(0f, -RowTop(BurrowGrid.k_CounterRow) - k_WombatDrop);

        public BakeryLayout(TableSet tables)
        {
            m_cellWidth = tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
            m_cellHeight = tables.Get<ConfigTable>(ConfigTable.k_CellHeight).Value;
            m_entranceHeight = tables.Get<ConfigTable>(ConfigTable.k_EntranceHeight).Value;
        }

        public float RowTop(int row)
        {
            return row <= 0 ? 0f : (float)(m_entranceHeight + (row - 1) * m_cellHeight);
        }

        public Vector2 CellCenter(Cell cell)
        {
            float height = (float)(cell.Row == 0 ? m_entranceHeight : m_cellHeight);
            return new Vector2((float)((cell.Col + 0.5) * m_cellWidth), -(RowTop(cell.Row) + height * 0.5f));
        }

        public Vector2 ShelfBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_ShelfDrop);
        }

        public Vector2 ShelfSignBase(Cell cell)
        {
            Vector2 shelf = ShelfBase(cell);
            return shelf + new Vector2(-ToCenter(shelf) * k_SignOffsetX, -k_SignDrop);
        }

        public Vector2 OvenBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_OvenDrop);
        }

        // 빈 자리 표지(칸 가운데보다 조금 아래)
        public Vector2 SlotBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_SlotDrop);
        }

        // 칸 사각형까지의 거리(안이면 0). 파기 대상 고르기
        public float DistanceToCell(Cell cell, Vector2 p)
        {
            float top = -RowTop(cell.Row);
            float bottom = top - (float)(cell.Row == 0 ? m_entranceHeight : m_cellHeight);
            float left = (float)(cell.Col * m_cellWidth);
            float right = left + (float)m_cellWidth;
            float dx = Math.Max(Math.Max(left - p.X, p.X - right), 0f);
            float dy = Math.Max(Math.Max(bottom - p.Y, p.Y - top), 0f);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public IReadOnlyList<Vector2> ShelfSpots(Cell cell)
        {
            return m_shelfSpots[cell];
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
            int unit = (int)BurrowShape.k_PixelsPerUnit;
            Shape = BurrowShape.Build(cells, (int)Math.Round(m_cellWidth * unit), (int)Math.Round(m_cellHeight * unit), (int)Math.Round(m_entranceHeight * unit));

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
            WombatNav = new BurrowNav(Shape, blocked);
            blocked.Add(Footprint(WombatHome, k_WombatHalf, k_WombatDepth));
            Nav = new BurrowNav(Shape, blocked);

            BuildQueue(queueCapacity);
            BuildShelfSpots(shelves);
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
                    new Vector2(shelf.X - toCenter * (k_SideOffset + k_SignClear), shelf.Y - 0.05f),
                    new Vector2(shelf.X + toCenter * k_FrontOffsetX, shelf.Y - k_FrontOffsetY),
                    new Vector2(shelf.X - toCenter * k_FrontOffsetX, shelf.Y - k_FrontOffsetY),
                };
                List<Vector2> spots = new List<Vector2>();

                foreach (Vector2 candidate in candidates)
                {
                    // x는 가까운 격자, y는 아래 격자(사물보다 앞에 그려지게)
                    Vector2 spot = new Vector2(Nav.Snap(candidate).X, (float)Math.Floor(candidate.Y / Nav.Step + 1e-4) * Nav.Step);

                    if (Nav.IsWalkable(spot) && !NearQueue(spot) && Vector2.Distance(spot, HoleFloor) >= k_SpotGap && !Near(all, spot, BurrowNav.k_Step))
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
