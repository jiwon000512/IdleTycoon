using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 3·4·6·7장 · 설계 18: 가게 배치의 단일 출처. 놓인 사물 → 걷는 땅·줄 자리(계산대마다)·서는 자리(진열대마다), 구멍, 굴 마스크.
    // 사물 바닥 사각형·자리 오프셋은 표(InteractableTable)에서, 시작 배치는 BakeryConfigTable에서 오고, 칸 좌표(CellCenter 등)는 파기와 테스트에만 쓴다.
    // 좌표는 가게 원점(입구 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class BakeryLayout
    {
        // 테스트용 칸 자리의 사물 밑변(그림 발끝): 칸 가운데에서 아래로
        public const float k_ShelfDrop = 0.65f;
        public const float k_OvenDrop = 0.85f;

        // 웜뱃 바닥 자리(손님이 계산대 뒤로 지나가지 않게): 계산대 윗변부터 뒤 자리 위 k_WombatBack까지, 좌우 half
        private const float k_WombatHalf = 0.4f;
        private const float k_WombatBack = 0.3f;
        // 재고 표지판(2026-09-24): 진열대 왼쪽 앞 모서리의 발끝. 옆으로 53px(PPU 80, 홀수)이라 진열대와 같은 칸 격자(2px)에 선다
        private const float k_SignOffsetX = 0.6625f;
        private const float k_SignDrop = 0.2f;
        // 앞자리 폭(넘칠 때 진열대 앞 기준점)
        private const float k_FrontOffsetY = 0.7f;
        // 줄: 간격, 서는 자리와 떨어질 거리, 구멍 아래와 떨어질 거리
        private const float k_QueueSpacing = 0.8f;
        private const float k_SpotGap = 0.6f;
        private const float k_HoleGap = 1f;
        private const float k_OverflowDistance = 2f;

        private readonly CellMetrics m_cells;
        private readonly Dictionary<CounterInteractable, List<Vector2>> m_queues = new Dictionary<CounterInteractable, List<Vector2>>();
        private readonly Dictionary<ShelfInteractable, List<Vector2>> m_shelfSpots = new Dictionary<ShelfInteractable, List<Vector2>>();
        private readonly List<Vector2> m_noSpots = new List<Vector2>();

        // 굴 칸 크기(유닛)와 칸 ↔ 좌표(설계 27: 빵집·농장 공용 CellMetrics)
        public CellMetrics Cells => m_cells;
        public float CellWidth => m_cells.CellWidth;
        public float CellHeight => m_cells.CellHeight;
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 설계 09: 웜뱃이 걷는 땅. 손님 땅과 같되 계산대 뒤 웜뱃 자리를 막지 않는다
        public BurrowNav WombatNav { get; private set; }
        // 첫 계산대 뒤 웜뱃 자리(시작 위치)
        public Vector2 WombatHome { get; private set; }
        // 구멍 안(나타나는 곳)과 구멍 아래 바닥(내려앉는 곳)
        // 굴 환경 A2: 아치 구멍 밑변(= 띠 밑변) 바로 위에서 톡 나온다
        public Vector2 HoleInside => new Vector2(0f, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);

        public BakeryLayout(TableSet tables)
        {
            m_cells = new CellMetrics(tables);
        }

        public float RowTop(int row)
        {
            return m_cells.RowTop(row);
        }

        public Vector2 CellCenter(Cell cell)
        {
            return m_cells.CellCenter(cell);
        }

        // 테스트용 칸 자리
        public Vector2 ShelfBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_ShelfDrop);
        }

        public Vector2 OvenBase(Cell cell)
        {
            return CellCenter(cell) - new Vector2(0f, k_OvenDrop);
        }

        // 표지판은 늘 진열대 왼쪽 앞
        public Vector2 ShelfSignBase(ShelfInteractable shelf)
        {
            return shelf.Position + new Vector2(-k_SignOffsetX, -k_SignDrop);
        }

        // 칸 사각형까지의 거리(안이면 0). 파기 대상 고르기
        public float DistanceToCell(Cell cell, Vector2 p)
        {
            return m_cells.DistanceToCell(cell, p);
        }

        // 진열대의 서는 자리(구멍에서 가까운 순). 놓이지 않은(보관된) 진열대는 없음
        public IReadOnlyList<Vector2> ShelfSpots(ShelfInteractable shelf)
        {
            return shelf != null && m_shelfSpots.TryGetValue(shelf, out List<Vector2> spots) ? spots : m_noSpots;
        }

        // 진열대 옆자리는 진열대 쪽을, 앞자리는 위를 본다
        public Facing ShelfFacing(ShelfInteractable shelf, Vector2 spot)
        {
            if (Math.Abs(spot.X - shelf.Position.X) < 0.3f)
            {
                return Facing.Up;
            }

            return shelf.Position.X < spot.X ? Facing.Left : Facing.Right;
        }

        public IReadOnlyList<Vector2> QueueSlots(CounterInteractable counter)
        {
            return m_queues[counter];
        }

        // 줄 머리는 계산대(위)를, 나머지는 앞사람 쪽을 본다
        public Facing QueueFacing(CounterInteractable counter, int index)
        {
            List<Vector2> slots = m_queues[counter];
            return index == 0 ? Facing.Up : Mover.FacingOf(slots[index - 1] - slots[index]);
        }

        // 서는 자리가 다 찼을 때: 진열대에서 가까운 빈 걷는 점
        public Vector2 OverflowSpot(ShelfInteractable shelf, Func<Vector2, bool> taken)
        {
            IReadOnlyList<Vector2> spots = ShelfSpots(shelf);
            Vector2 around = spots.Count > 0 ? spots[0] : shelf.Position - new Vector2(0f, k_FrontOffsetY);
            return Nav.TryNearestFree(around, p => taken(p) || NearQueue(p), k_OverflowDistance, out Vector2 spot) ? spot : around;
        }

        public void Rebuild(IReadOnlyCollection<Cell> cells, IReadOnlyList<ShelfInteractable> shelves, IReadOnlyList<OvenInteractable> ovens,
            IReadOnlyList<CounterInteractable> counters, int queueCapacity)
        {
            Shape = m_cells.Build(cells);

            List<NavRect> blocked = new List<NavRect>();

            foreach (ShelfInteractable shelf in shelves)
            {
                blocked.Add(Placement.Rect(shelf));
            }

            foreach (OvenInteractable oven in ovens)
            {
                blocked.Add(Placement.Rect(oven));
            }

            // 웜뱃에게 계산대는 밑변 선만 막는다(뒤에 바짝 붙어 발이 계산대 그림에 묻히게). 손님에게는 바닥 사각형 + 웜뱃 자리
            foreach (CounterInteractable counter in counters)
            {
                NavRect rect = Placement.Rect(counter);
                blocked.Add(new NavRect(rect.XMin, rect.YMin, rect.XMax, rect.YMin));
            }

            WombatNav = new BurrowNav(Shape, blocked);
            blocked.RemoveRange(blocked.Count - counters.Count, counters.Count);

            foreach (CounterInteractable counter in counters)
            {
                Vector2 home = counter.WorkerSpot;
                blocked.Add(Placement.Rect(counter));
                blocked.Add(new NavRect(home.X - k_WombatHalf, Placement.Rect(counter).YMax, home.X + k_WombatHalf, home.Y + k_WombatBack));
            }

            Nav = new BurrowNav(Shape, blocked);
            WombatHome = counters.Count > 0 ? counters[0].WorkerSpot : Vector2.Zero;

            m_queues.Clear();

            foreach (CounterInteractable counter in counters)
            {
                m_queues[counter] = BuildQueue(Placement.SpotOf(counter, SpotRole.Queue), queueCapacity);
            }

            BuildShelfSpots(shelves);
        }

        // 6장: 머리(등록기 앞)에서 왼쪽으로 간격마다. 막히면 아래(계산대 왼쪽) → 위 → 오른쪽으로 꺾고, 모두 막히면 끝자리에 겹쳐 선다
        private List<Vector2> BuildQueue(Vector2 headAt, int capacity)
        {
            List<Vector2> slots = new List<Vector2>();
            Vector2 head = Nav.Snap(headAt);
            slots.Add(Nav.IsWalkable(head) || !Nav.TryNearestFree(head, _ => false, k_OverflowDistance, out Vector2 near) ? head : near);
            Vector2[] turns = { new Vector2(-1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(1f, 0f) };
            Vector2 dir = turns[0];

            while (slots.Count < capacity)
            {
                Vector2 prev = slots[slots.Count - 1];
                bool placed = false;

                foreach (Vector2 d in Prepend(dir, turns))
                {
                    Vector2 next = Nav.Snap(prev + d * k_QueueSpacing);

                    if (d == -dir || !Nav.IsLineWalkable(prev, next) || Vector2.Distance(next, HoleFloor) < k_HoleGap || CrowdsQueue(next) || Near(slots, next, k_QueueSpacing - 0.01f))
                    {
                        continue;
                    }

                    slots.Add(next);
                    dir = d;
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    break;
                }
            }

            while (slots.Count < capacity)
            {
                slots.Add(slots[slots.Count - 1]);
            }

            return slots;
        }

        // 7장: 표의 손님 자리 중 걷는 땅이 아니거나 줄·구멍 아래·다른 진열대 자리와 가까운 것은 빼고, 구멍에서 가까운 자리부터(넘칠 때 첫 자리 근처에 선다).
        // 벽 쪽 좁은 옆자리가 먼저면 오가는 길에 손님이 몰렸다(2026-10-02 시작 배치 C)
        private void BuildShelfSpots(IReadOnlyList<ShelfInteractable> shelves)
        {
            m_shelfSpots.Clear();
            List<Vector2> all = new List<Vector2>();

            foreach (ShelfInteractable shelf in shelves)
            {
                List<Vector2> spots = new List<Vector2>();

                foreach (SpotOffset offset in shelf.Kind.Spots)
                {
                    if (offset.Role != SpotRole.Customer)
                    {
                        continue;
                    }

                    Vector2 spot = Placement.SnapSpot(Nav, Placement.SpotAt(shelf.Position, offset));

                    if (Nav.IsWalkable(spot) && !NearQueue(spot) && Vector2.Distance(spot, HoleFloor) >= k_SpotGap && !Near(all, spot, BurrowNav.k_Step))
                    {
                        spots.Add(spot);
                        all.Add(spot);
                    }
                }

                spots.Sort((a, b) => Vector2.Distance(a, HoleFloor).CompareTo(Vector2.Distance(b, HoleFloor)));
                m_shelfSpots[shelf] = spots;
            }
        }

        private bool NearQueue(Vector2 p)
        {
            foreach (List<Vector2> slots in m_queues.Values)
            {
                if (Near(slots, p, k_SpotGap))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CrowdsQueue(Vector2 p)
        {
            foreach (List<Vector2> slots in m_queues.Values)
            {
                if (Near(slots, p, k_QueueSpacing - 0.01f))
                {
                    return true;
                }
            }

            return false;
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
    }
}
