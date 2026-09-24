using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11 2장: 광장 배치의 단일 출처. 빵집과 같은 칸 격자(가로 cols칸 가운데 정렬 × 세로 rows칸)로 굴 그림·걷는 땅을 만들고,
    // 뒷벽에 빵집 문(계단 왼쪽 칸 가운데)과 지상 계단(가운데), 바닥에 장식과 들를 곳을 둔다. 좌표는 광장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class PlazaLayout
    {
        // 구멍 안(나타나는 곳)은 띠 밑변 바로 위, 아래 바닥(내려앉는 곳)은 빵집 구멍 아래와 같은 높이
        private const float k_HoleFloorY = -2.2f;

        private readonly List<PlazaSpot> m_spots = new List<PlazaSpot>();
        private readonly List<PlazaDecor> m_decor = new List<PlazaDecor>();

        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; }
        public IReadOnlyList<PlazaDecor> Decor => m_decor;
        public IReadOnlyList<PlazaSpot> Spots => m_spots;
        public Vector2 DoorInside { get; }
        public Vector2 DoorFloor { get; }
        public Vector2 StairsInside { get; }
        public Vector2 StairsFloor { get; }
        // 광장 크기(유닛): 가로는 가운데 정렬, 세로는 원점부터 아래로
        public float Width { get; }
        public float Height { get; }

        public PlazaLayout(GameConfig config, IReadOnlyList<DecorationRecord> decorations)
        {
            GameConfig.ShopConfig shop = config.Shop;
            GameConfig.PlazaConfig plaza = config.Plaza;
            int unit = (int)ShopLayout.k_PixelsPerUnit;
            int firstCol = -plaza.Cols / 2;
            HashSet<Cell> cells = new HashSet<Cell>();

            for (int row = 0; row < plaza.Rows; row++)
            {
                for (int col = firstCol; col < firstCol + plaza.Cols; col++)
                {
                    cells.Add(new Cell(col, row));
                }
            }

            Shape = BurrowShape.Build(cells, (int)Math.Round(shop.CellWidth * unit), (int)Math.Round(shop.CellHeight * unit),
                (int)Math.Round(shop.EntranceHeight * unit), ShopLayout.k_RoundRadius);
            Width = (float)(plaza.Cols * shop.CellWidth);
            Height = (float)(shop.EntranceHeight + (plaza.Rows - 1) * shop.CellHeight);

            float inside = -(BurrowShape.k_EntranceFloorTop - 1) / ShopLayout.k_PixelsPerUnit;
            float doorX = (float)(-0.5 * shop.CellWidth);
            DoorInside = new Vector2(doorX, inside);
            DoorFloor = new Vector2(doorX, k_HoleFloorY);
            StairsInside = new Vector2(0f, inside);
            StairsFloor = new Vector2(0f, k_HoleFloorY);

            List<NavRect> blocked = new List<NavRect>();

            foreach (GameConfig.PlacedDecor placed in plaza.Decor)
            {
                DecorationRecord record = Find(decorations, placed.Id);
                Vector2 position = new Vector2((float)placed.X, (float)placed.Y);
                m_decor.Add(new PlazaDecor(record, position));
                blocked.Add(new NavRect(position.X - (float)record.HalfWidth, position.Y, position.X + (float)record.HalfWidth, position.Y + (float)record.Depth));
            }

            Nav = new BurrowNav(Shape, ShopLayout.k_PixelsPerUnit, blocked, ShopLayout.k_Clearance, ShopLayout.k_Step, ShopLayout.k_TurnPenalty);

            // 들를 곳은 격자에 붙이고, 걷는 땅이 아니면 뺀다(장식이 벽에 붙어 있을 때)
            foreach (PlazaDecor decor in m_decor)
            {
                foreach (DecorationSpot spot in decor.Record.Spots)
                {
                    Vector2 p = Nav.Snap(decor.Position + new Vector2((float)spot.Dx, (float)spot.Dy));

                    if (Nav.IsWalkable(p))
                    {
                        m_spots.Add(new PlazaSpot(p, ParseFacing(spot.Face)));
                    }
                }
            }
        }

        public static Facing ParseFacing(string face)
        {
            switch (face)
            {
                case "up": return Facing.Up;
                case "left": return Facing.Left;
                case "right": return Facing.Right;
                default: return Facing.Down;
            }
        }

        private static DecorationRecord Find(IReadOnlyList<DecorationRecord> decorations, string id)
        {
            foreach (DecorationRecord record in decorations)
            {
                if (record.Id == id)
                {
                    return record;
                }
            }

            throw new KeyNotFoundException($"장식 ID '{id}'가 decorations.json에 없다.");
        }
    }

    // 놓인 장식 하나(밑변 가운데)
    public readonly struct PlazaDecor
    {
        public DecorationRecord Record { get; }
        public Vector2 Position { get; }

        public PlazaDecor(DecorationRecord record, Vector2 position)
        {
            Record = record;
            Position = position;
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
