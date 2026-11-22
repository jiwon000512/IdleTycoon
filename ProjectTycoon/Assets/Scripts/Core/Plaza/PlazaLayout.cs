using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 2장 · 설계 18 · 설계 64: 광장 배치의 단일 출처. 빵집과 같은 칸 격자에서 판 칸의 합으로 굴 그림·걷는 땅을 만들고(가지 굴: 굴길 + 주머니),
    // 문은 주머니 칸의 뒷벽(설계 44 문 그림 그대로), 지상 계단은 입구 줄 가운데. 장식은 곳이 들고 있고 Rebuild로 걷는 땅·들를 곳을 다시 만든다.
    // 좌표는 광장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class PlazaLayout
    {
        // 문 앞 바닥(내려앉는 곳)은 띠 밑변에서 이만큼 아래, 구멍 안(나타나는 곳)은 띠 밑변 바로 위
        public const float k_DoorFloorDrop = 0.775f;
        private const float k_DoorInsideRise = 1f / BurrowShape.k_PixelsPerUnit;
        // 석상 · 행상 자리는 주머니 칸 밑변에서 이만큼 안쪽(밑변 선은 걷는 땅 밖)
        private const float k_PocketInset = 0.4f;

        private readonly List<PlazaSpot> m_spots = new List<PlazaSpot>();

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        // 손님 땅(설계 37: 똥 둘레가 걸린다)과 웜뱃 땅(둘레 없이 밟고 지나간다). 막힌 곳은 같다
        public BurrowNav Nav { get; private set; }
        public BurrowNav WombatNav { get; private set; }
        public IReadOnlyList<PlazaSpot> Spots => m_spots;
        // 설계 44 · 64: 주머니 뒷벽 문들(빵집 · 농장 · …). 그 곳 점원도 외출할 때 그 문으로 드나든다(설계 38). 주머니를 파기 전엔 닿지 못한다
        public IReadOnlyList<PlazaDoor> Doors { get; }
        public Vector2 StairsInside { get; }
        public Vector2 StairsFloor { get; }
        // 설계 29 · 34 · 64: 석상 자리 · 행상 좌판 자리(그 주머니 칸 밑변 가운데에서 k_PocketInset 위)
        public Vector2 StatueBase { get; }
        public Vector2 MerchantStall { get; }
        // 굴 그림이 차지하는 네모(유닛). 카메라 범위
        public NavRect Bounds
        {
            get
            {
                float xMin = Shape.OriginX / BurrowShape.k_PixelsPerUnit;
                float yMax = -Shape.OriginY / BurrowShape.k_PixelsPerUnit;
                return new NavRect(xMin, yMax - Shape.Height / BurrowShape.k_PixelsPerUnit, xMin + Shape.Width / BurrowShape.k_PixelsPerUnit, yMax);
            }
        }

        public PlazaLayout(TableSet tables, PlazaConfigTable config, IReadOnlyCollection<Cell> cells)
        {
            Cells = new CellMetrics(tables);
            float entranceWall = -BurrowShape.k_EntranceFloorTop / BurrowShape.k_PixelsPerUnit;
            List<PlazaDoor> doors = new List<PlazaDoor>();

            foreach (PlazaPocketData pocket in config.Pockets)
            {
                Vector2 at = Cells.CellBase(pocket.Cell) + new Vector2(0f, k_PocketInset);

                switch (pocket.Use)
                {
                    case PlazaPocketData.k_Door:
                        float wallBottom = -Cells.RowTop(pocket.Row);
                        float x = Cells.CellCenter(pocket.Cell).X;
                        doors.Add(new PlazaDoor(pocket.To, pocket.Cell, new Vector2(x, wallBottom + k_DoorInsideRise), new Vector2(x, wallBottom - k_DoorFloorDrop), wallBottom));
                        break;
                    case PlazaPocketData.k_Statue:
                        StatueBase = at;
                        break;
                    case PlazaPocketData.k_Merchant:
                        MerchantStall = at;
                        break;
                }
            }

            Doors = doors;
            StairsInside = new Vector2(0f, entranceWall + k_DoorInsideRise);
            StairsFloor = new Vector2(0f, entranceWall - k_DoorFloorDrop);
            Rebuild(cells, new List<DecorationData>());
        }

        // 그 곳으로 가는 문(PlazaGate). 없으면 null
        public PlazaDoor DoorTo(string areaId)
        {
            foreach (PlazaDoor door in Doors)
            {
                if (door.To == areaId)
                {
                    return door;
                }
            }

            return null;
        }

        // 판 칸으로 굴을 만들고, 장식 · 석상 바닥을 막고, 들를 곳은 격자에 붙이되 걷는 땅이 아니면 뺀다(장식이 벽에 붙어 있을 때)
        public void Rebuild(IReadOnlyCollection<Cell> cells, IEnumerable<IPlaced> decor)
        {
            Shape = Cells.Build(cells);
            List<NavRect> blocked = new List<NavRect>();

            foreach (IPlaced placed in decor)
            {
                blocked.Add(Placement.Rect(placed));
            }

            Nav = new BurrowNav(Shape, blocked);
            WombatNav = new BurrowNav(Shape, blocked);
            m_spots.Clear();

            foreach (IPlaced placed in decor)
            {
                foreach (SpotOffset spot in placed.Kind.Spots)
                {
                    // 설계 31: 행상이 서는 자리(worker)는 손님이 들르지 않는다
                    if (spot.Role != SpotRole.Customer)
                    {
                        continue;
                    }

                    Vector2 p = Nav.Snap(Placement.SpotAt(placed.Position, spot));

                    if (Nav.IsWalkable(p))
                    {
                        m_spots.Add(new PlazaSpot(p, spot.Face));
                    }
                }
            }
        }
    }

    // 설계 44: 뒷벽 문 하나. 구멍 안(나타나는 곳) · 아래 바닥(내려앉는 곳) · 띠 밑변(문 그림이 서는 높이)
    public sealed class PlazaDoor
    {
        public string To { get; }
        // 설계 64: 문이 선 주머니 칸(파야 문이 보이고 닿는다)
        public Cell Cell { get; }
        public Vector2 Inside { get; }
        public Vector2 Floor { get; }
        public float WallBottom { get; }

        public PlazaDoor(string to, Cell cell, Vector2 inside, Vector2 floor, float wallBottom)
        {
            To = to;
            Cell = cell;
            Inside = inside;
            Floor = floor;
            WallBottom = wallBottom;
        }
    }

    // 손님이 들르는 자리와 거기서 보는 방향
    public readonly struct PlazaSpot
    {
        public Vector2 Position { get; }
        public Facing Facing { get; }

        public PlazaSpot(Vector2 position, Facing facing)
        {
            Position = position;
            Facing = facing;
        }
    }
}
