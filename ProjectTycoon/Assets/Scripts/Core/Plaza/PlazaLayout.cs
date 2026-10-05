using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 2장 · 설계 18: 광장 배치의 단일 출처. 빵집과 같은 칸 격자(가로 cols칸 가운데 정렬 × 세로 rows칸)로 굴 그림·걷는 땅을 만들고,
    // 뒷벽에 곳으로 가는 문(설계 44: PlazaConfigTable doors, 칸 가운데)과 지상 계단(가운데)을 둔다. 장식은 곳이 들고 있고 Rebuild로 걷는 땅·들를 곳을 다시 만든다.
    // 좌표는 광장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class PlazaLayout
    {
        // 구멍 안(나타나는 곳)은 띠 밑변 바로 위, 아래 바닥(내려앉는 곳)은 빵집 구멍 아래와 같은 높이
        private const float k_HoleFloorY = -2.2f;

        private readonly List<PlazaSpot> m_spots = new List<PlazaSpot>();

        public BurrowShape.Result Shape { get; }
        // 손님 땅(설계 37: 똥 둘레가 걸린다)과 웜뱃 땅(둘레 없이 밟고 지나간다). 막힌 곳은 같다
        public BurrowNav Nav { get; private set; }
        public BurrowNav WombatNav { get; private set; }
        public IReadOnlyList<PlazaSpot> Spots => m_spots;
        // 설계 44: 뒷벽 문들(빵집 · 농장 · …). 그 곳 점원도 외출할 때 그 문으로 드나든다(설계 38)
        public IReadOnlyList<PlazaDoor> Doors { get; }
        public Vector2 StairsInside { get; }
        public Vector2 StairsFloor { get; }
        // 광장 크기(유닛): 가로는 가운데 정렬, 세로는 원점부터 아래로
        public float Width { get; }
        public float Height { get; }

        public PlazaLayout(TableSet tables)
        {
            PlazaConfigTable plaza = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            double cellWidth = tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
            double cellHeight = tables.Get<ConfigTable>(ConfigTable.k_CellHeight).Value;
            double entranceHeight = tables.Get<ConfigTable>(ConfigTable.k_EntranceHeight).Value;
            Shape = BurrowShape.Room(tables, plaza.Cols, plaza.Rows);
            Width = (float)(plaza.Cols * cellWidth);
            Height = (float)(entranceHeight + (plaza.Rows - 1) * cellHeight);

            float inside = -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit;
            List<PlazaDoor> doors = new List<PlazaDoor>();

            foreach (PlazaDoorData door in plaza.Doors)
            {
                float x = (float)((door.Col - plaza.Cols / 2d + 0.5) * cellWidth);
                doors.Add(new PlazaDoor(door.To, new Vector2(x, inside), new Vector2(x, k_HoleFloorY)));
            }

            Doors = doors;
            StairsInside = new Vector2(0f, inside);
            StairsFloor = new Vector2(0f, k_HoleFloorY);
            Rebuild(new List<DecorationData>());
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

        // 장식 · 석상 바닥을 막고, 들를 곳은 격자에 붙이되 걷는 땅이 아니면 뺀다(장식이 벽에 붙어 있을 때)
        public void Rebuild(IEnumerable<IPlaced> decor)
        {
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

    // 설계 44: 뒷벽 문 하나. 구멍 안(나타나는 곳) · 아래 바닥(내려앉는 곳)
    public sealed class PlazaDoor
    {
        public string To { get; }
        public Vector2 Inside { get; }
        public Vector2 Floor { get; }

        public PlazaDoor(string to, Vector2 inside, Vector2 floor)
        {
            To = to;
            Inside = inside;
            Floor = floor;
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
