using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 47: 횟집 배치의 단일 출처. 놓인 사물 → 걷는 땅 · 수조 앞 서는 자리 · 탁자 앉는 자리, 구멍, 굴 마스크. 좌표는 가게 원점(입구 줄 윗변 가운데) 기준 유닛, y 위.
    // ponytail: 구멍 · 칸 · 도마 뒤 웜뱃 자리는 BakeryLayout과 같은 규칙을 따로 쓴다. 셋째 가게가 생기면 빵집과 함께 공용 가게 배치로 올린다
    public sealed class RestaurantLayout
    {
        // 웜뱃 바닥 자리(손님이 도마 뒤로 지나가지 않게): 도마 윗변부터 뒤 자리 위 k_WombatBack까지, 좌우 half
        private const float k_WombatHalf = 0.4f;
        private const float k_WombatBack = 0.3f;
        // 서는 자리가 구멍 아래와 떨어질 거리
        private const float k_SpotGap = 0.6f;

        private readonly CellMetrics m_cells;
        private readonly Dictionary<TankInteractable, List<Vector2>> m_tankSpots = new Dictionary<TankInteractable, List<Vector2>>();
        private readonly Dictionary<DiningTableInteractable, List<Seat>> m_seats = new Dictionary<DiningTableInteractable, List<Seat>>();
        private readonly List<Vector2> m_noSpots = new List<Vector2>();
        private readonly List<Seat> m_noSeats = new List<Seat>();

        public CellMetrics Cells => m_cells;
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 웜뱃이 걷는 땅. 손님 땅과 같되 도마 뒤 웜뱃 자리를 막지 않는다
        public BurrowNav WombatNav { get; private set; }
        public Vector2 HoleInside => new Vector2(0f, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);

        public RestaurantLayout(TableSet tables)
        {
            m_cells = new CellMetrics(tables);
        }

        // 앉는 자리 하나: 탁자 표 자리 번호 · 바닥 점 · 보는 방향(탁자 쪽)
        public readonly struct Seat
        {
            public readonly int Index;
            public readonly Vector2 Position;
            public readonly Facing Facing;

            public Seat(int index, Vector2 position, Facing facing)
            {
                Index = index;
                Position = position;
                Facing = facing;
            }
        }

        // 수조 앞 서는 자리(구멍에서 가까운 순)
        public IReadOnlyList<Vector2> TankSpots(TankInteractable tank)
        {
            return tank != null && m_tankSpots.TryGetValue(tank, out List<Vector2> spots) ? spots : m_noSpots;
        }

        // 걷는 땅에 놓인 앉는 자리
        public IReadOnlyList<Seat> Seats(DiningTableInteractable table)
        {
            return table != null && m_seats.TryGetValue(table, out List<Seat> seats) ? seats : m_noSeats;
        }

        public float DistanceToCell(Cell cell, Vector2 p)
        {
            return m_cells.DistanceToCell(cell, p);
        }

        public void Rebuild(IReadOnlyCollection<Cell> cells, IReadOnlyList<TankInteractable> tanks, IReadOnlyList<CuttingBoardInteractable> boards,
            IReadOnlyList<DiningTableInteractable> tables)
        {
            Shape = m_cells.Build(cells);
            List<NavRect> blocked = new List<NavRect>();

            foreach (TankInteractable tank in tanks)
            {
                blocked.Add(Placement.Rect(tank));
            }

            foreach (DiningTableInteractable table in tables)
            {
                blocked.Add(Placement.Rect(table));
            }

            // 도마는 바닥 사각형 전체를 막는다: 웜뱃이 도마 뒤에 바짝 붙어 몸이 묻히지 않게(계산대와 다르다, 사용자 2026-10-06).
            // 손님에게는 그 뒤 웜뱃 자리도
            foreach (CuttingBoardInteractable board in boards)
            {
                blocked.Add(Placement.Rect(board));
            }

            WombatNav = new BurrowNav(Shape, blocked);

            foreach (CuttingBoardInteractable board in boards)
            {
                Vector2 home = board.WorkerSpot;
                blocked.Add(new NavRect(home.X - k_WombatHalf, Placement.Rect(board).YMax, home.X + k_WombatHalf, home.Y + k_WombatBack));
            }

            Nav = new BurrowNav(Shape, blocked);
            BuildTankSpots(tanks);
            BuildSeats(tables);
        }

        private void BuildTankSpots(IReadOnlyList<TankInteractable> tanks)
        {
            m_tankSpots.Clear();

            foreach (TankInteractable tank in tanks)
            {
                List<Vector2> spots = new List<Vector2>();

                foreach (SpotOffset offset in tank.Kind.Spots)
                {
                    Vector2 spot = Placement.SnapSpot(Nav, Placement.SpotAt(tank.Position, offset));

                    if (offset.Role == SpotRole.Customer && Nav.IsWalkable(spot) && Vector2.Distance(spot, HoleFloor) >= k_SpotGap)
                    {
                        spots.Add(spot);
                    }
                }

                spots.Sort((a, b) => Vector2.Distance(a, HoleFloor).CompareTo(Vector2.Distance(b, HoleFloor)));
                m_tankSpots[tank] = spots;
            }
        }

        private void BuildSeats(IReadOnlyList<DiningTableInteractable> tables)
        {
            m_seats.Clear();

            foreach (DiningTableInteractable table in tables)
            {
                List<Seat> seats = new List<Seat>();
                IReadOnlyList<SpotOffset> offsets = table.Kind.Spots;

                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector2 spot = Placement.SnapSpot(Nav, Placement.SpotAt(table.Position, offsets[i]));

                    if (offsets[i].Role == SpotRole.Customer && Nav.IsWalkable(spot))
                    {
                        seats.Add(new Seat(i, spot, offsets[i].Face));
                    }
                }

                m_seats[table] = seats;
            }
        }
    }
}
