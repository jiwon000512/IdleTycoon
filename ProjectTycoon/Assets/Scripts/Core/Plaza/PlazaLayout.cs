using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 2장: 광장 배치의 단일 출처. 빵집과 같은 칸 격자(가로 cols칸 가운데 정렬 × 세로 rows칸)로 굴 그림·걷는 땅을 만들고,
    // 뒷벽에 빵집 문(계단 왼쪽 칸 가운데)과 지상 계단(가운데), 바닥에 장식과 들를 곳을 둔다. 좌표는 광장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class PlazaLayout
    {
        // 구멍 안(나타나는 곳)은 띠 밑변 바로 위, 아래 바닥(내려앉는 곳)은 빵집 구멍 아래와 같은 높이
        private const float k_HoleFloorY = -2.2f;

        private readonly List<PlazaSpot> m_spots = new List<PlazaSpot>();
        private readonly List<DecorationData> m_decor = new List<DecorationData>();

        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; }
        public IReadOnlyList<DecorationData> Decor => m_decor;
        public IReadOnlyList<PlazaSpot> Spots => m_spots;
        public Vector2 DoorInside { get; }
        public Vector2 DoorFloor { get; }
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
            int unit = (int)BurrowShape.k_PixelsPerUnit;
            int firstCol = -plaza.Cols / 2;
            HashSet<Cell> cells = new HashSet<Cell>();

            for (int row = 0; row < plaza.Rows; row++)
            {
                for (int col = firstCol; col < firstCol + plaza.Cols; col++)
                {
                    cells.Add(new Cell(col, row));
                }
            }

            Shape = BurrowShape.Build(cells, (int)Math.Round(cellWidth * unit), (int)Math.Round(cellHeight * unit), (int)Math.Round(entranceHeight * unit));
            Width = (float)(plaza.Cols * cellWidth);
            Height = (float)(entranceHeight + (plaza.Rows - 1) * cellHeight);

            float inside = -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit;
            float doorX = (float)(-0.5 * cellWidth);
            DoorInside = new Vector2(doorX, inside);
            DoorFloor = new Vector2(doorX, k_HoleFloorY);
            StairsInside = new Vector2(0f, inside);
            StairsFloor = new Vector2(0f, k_HoleFloorY);

            List<NavRect> blocked = new List<NavRect>();

            foreach (PlazaDecorTable placed in tables.GetAll<PlazaDecorTable>())
            {
                DecorationTable decoration = tables.Get<DecorationTable>(placed.Decoration);
                Vector2 position = new Vector2((float)placed.X, (float)placed.Y);
                m_decor.Add(new DecorationData(decoration, position));
                blocked.Add(new NavRect(position.X - (float)decoration.HalfWidth, position.Y, position.X + (float)decoration.HalfWidth,
                    position.Y + (float)decoration.Depth));
            }

            Nav = new BurrowNav(Shape, blocked);

            // 들를 곳은 격자에 붙이고, 걷는 땅이 아니면 뺀다(장식이 벽에 붙어 있을 때)
            foreach (DecorationData decor in m_decor)
            {
                foreach (DecorationSpot spot in decor.Table.Spots)
                {
                    Vector2 p = Nav.Snap(decor.Position + new Vector2((float)spot.Dx, (float)spot.Dy));

                    if (Nav.IsWalkable(p))
                    {
                        m_spots.Add(new PlazaSpot(p, spot.Face));
                    }
                }
            }
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
