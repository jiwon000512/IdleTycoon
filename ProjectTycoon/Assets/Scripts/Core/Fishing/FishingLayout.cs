using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 58 층계 웅덩이: 낚시터 배치의 단일 출처. 층(FishingStretchTable index, 작을수록 위)은 세로로 쌓인 웅덩이 하나 — 둑 줄 하나 + 물 줄 waterRows.
    // 열린 층의 물은 열 단위 격자(BurrowGrid, 칸 (col, 1) = 물 한 열, 열 너비는 굴 칸의 반 — 사용자 2026-10-12 「물 칸을 반 너비로」)이고 비버가 둑을 갉아 넓어진다.
    // 방(굴 칸)은 열린 층 전부가 같은 폭(물 열이 든 굴 칸 ± 1칸)이라 옆 둑으로 층 사이를 오르내린다.
    // 아래 층이 열리면 위 층 물의 아랫부분 k_Cliff는 벼랑(폭포)이라 던지는 물 네모에서 뺀다. 좌표는 낚시터 원점 기준 유닛, y 위. 집 층 가운데가 x 0, 광장 구멍은 집 층 둑 줄 윗벽 가운데
    public sealed class FishingLayout
    {
        public const float k_Cliff = 1.0f;
        // 구멍 바닥 점은 둑 줄 바닥 윗변에서 이만큼 아래(입구 줄의 아치 구멍 밑변 ↔ 바닥 점 간격과 같다)
        private const float k_HoleDrop = 0.775f;
        private const float k_HoleIn = 0.025f;

        private readonly FishingConfigTable m_config;
        private readonly int m_minIndex;
        private readonly Dictionary<int, int> m_startCols = new Dictionary<int, int>();
        private readonly Dictionary<int, NavRect> m_waters = new Dictionary<int, NavRect>();

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 열린 층이 차지하는 네모(카메라 경계): 맨 위 층 둑 윗벽 띠부터 맨 아래 층 물 바닥까지
        public NavRect Bounds { get; private set; }
        public Vector2 HoleFloor { get; }
        public Vector2 HoleInside { get; }
        // 열린 층의 던지는 물 네모(index → 네모)
        public IReadOnlyDictionary<int, NavRect> Waters => m_waters;
        public int WaterRows => m_config.WaterRows;
        // 물 한 열 너비(굴 칸의 반)
        public float ColumnWidth => Cells.CellWidth * 0.5f;
        // 집 층 물 윗선(둑 선) · 집 층 가운데 x
        public float BankY => WaterTopOf(0);
        public float CenterX => CenterOf(0);

        // 층 가운데 x = 시작 물 열의 가운데(BurrowGrid.Columns처럼 -cols/2열부터라 홀수 열이면 칸 가운데)
        public float CenterOf(int index)
        {
            int cols = m_startCols[index];
            return (-cols / 2 + cols * 0.5f) * ColumnWidth;
        }

        public FishingLayout(TableSet tables)
        {
            m_config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Cells = new CellMetrics(tables);
            m_minIndex = tables.GetAll<FishingStretchTable>().Min(stretch => stretch.Index);

            foreach (FishingStretchTable stretch in tables.GetAll<FishingStretchTable>())
            {
                m_startCols[stretch.Index] = stretch.StartCols;
            }

            float floorTop = BankTopOf(0);
            HoleFloor = new Vector2(CenterOf(0), floorTop - k_HoleDrop);
            HoleInside = new Vector2(CenterOf(0), floorTop + k_HoleIn);
        }

        // 층의 둑 줄(전역 줄 번호). 0번 줄은 입구 줄이라 높이가 달라 비워 둔다
        public int BankRowOf(int index)
        {
            return 1 + (index - m_minIndex) * (1 + WaterRows);
        }

        public float BankTopOf(int index)
        {
            return -Cells.RowTop(BankRowOf(index));
        }

        public float WaterTopOf(int index)
        {
            return -Cells.RowTop(BankRowOf(index) + 1);
        }

        public float WaterBottomOf(int index)
        {
            return -Cells.RowTop(BankRowOf(index) + 1 + WaterRows);
        }

        // 물 한 열(격자 칸 (col, 1))의 네모
        public NavRect ColumnRect(int index, Cell cell)
        {
            float left = cell.Col * ColumnWidth;
            return new NavRect(left, WaterBottomOf(index), left + ColumnWidth, WaterTopOf(index));
        }

        public NavRect WaterOf(int index)
        {
            return m_waters[index];
        }

        // 열린 층의 물 격자로 굴 · 물 네모 · 길을 다시 만든다
        public void Rebuild(IReadOnlyDictionary<int, BurrowGrid> waters)
        {
            // 물 열(반 칸) → 그 열이 든 굴 칸, 양옆으로 한 칸씩 둑
            int colMin = FloorDiv(waters.Values.SelectMany(grid => grid.Cells).Min(cell => cell.Col), 2) - 1;
            int colMax = FloorDiv(waters.Values.SelectMany(grid => grid.Cells).Max(cell => cell.Col), 2) + 1;
            HashSet<Cell> cells = new HashSet<Cell>();
            List<NavRect> blocked = new List<NavRect>();
            m_waters.Clear();

            foreach (KeyValuePair<int, BurrowGrid> pair in waters)
            {
                int bank = BankRowOf(pair.Key);

                for (int row = bank; row <= bank + WaterRows; row++)
                {
                    for (int col = colMin; col <= colMax; col++)
                    {
                        cells.Add(new Cell(col, row));
                    }
                }

                foreach (Cell cell in pair.Value.Cells)
                {
                    blocked.Add(ColumnRect(pair.Key, cell));
                }

                int left = pair.Value.Cells.Min(cell => cell.Col);
                int right = pair.Value.Cells.Max(cell => cell.Col);
                float bottom = WaterBottomOf(pair.Key) + (waters.ContainsKey(pair.Key + 1) ? k_Cliff : 0f);
                m_waters[pair.Key] = new NavRect(left * ColumnWidth, bottom, (right + 1) * ColumnWidth, WaterTopOf(pair.Key));
            }

            Shape = Cells.Build(cells);
            Nav = new BurrowNav(Shape, blocked);
            int top = waters.Keys.Min();
            int bottomIndex = waters.Keys.Max();
            Bounds = new NavRect(colMin * Cells.CellWidth, WaterBottomOf(bottomIndex), (colMax + 1) * Cells.CellWidth, BankTopOf(top) + BurrowShape.k_WallHeight / BurrowShape.k_PixelsPerUnit);
        }

        private static int FloorDiv(int a, int b)
        {
            return (int)Math.Floor(a / (double)b);
        }

        // p에서 가장 가까운 층의 물 네모
        public NavRect NearestWater(Vector2 p)
        {
            return m_waters.Values.OrderBy(rect => rect.DistanceTo(p)).First();
        }

        // p에서 가장 가까운 물가(물 네모 둘레의 점). 물 안이면 가장 가까운 변으로
        public Vector2 Shore(Vector2 p)
        {
            NavRect water = NearestWater(p);
            float x = Math.Clamp(p.X, water.XMin, water.XMax);
            float y = Math.Clamp(p.Y, water.YMin, water.YMax);

            if (x != p.X || y != p.Y)
            {
                return new Vector2(x, y);
            }

            float left = p.X - water.XMin;
            float right = water.XMax - p.X;
            float bottom = p.Y - water.YMin;
            float top = water.YMax - p.Y;
            float nearest = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
            return nearest == top ? new Vector2(p.X, water.YMax) : nearest == bottom ? new Vector2(p.X, water.YMin) : nearest == left ? new Vector2(water.XMin, p.Y) : new Vector2(water.XMax, p.Y);
        }

        // 물가 점에서 물 안쪽으로 향하는 단위 벡터(그 변의 안쪽 법선)
        public Vector2 Inward(Vector2 shore)
        {
            NavRect water = NearestWater(shore);

            if (shore.Y >= water.YMax)
            {
                return new Vector2(0f, -1f);
            }

            if (shore.Y <= water.YMin)
            {
                return new Vector2(0f, 1f);
            }

            return shore.X <= water.XMin ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
        }

        // 그 물가에서 건너편까지 거리
        public float Depth(Vector2 shore)
        {
            NavRect water = NearestWater(shore);
            return Inward(shore).X == 0f ? water.YMax - water.YMin : water.XMax - water.XMin;
        }

        // p에서 본 물가에서 물 안쪽으로 k(0 = 물가, 1 = 건너편)만큼 들어간 점
        public Vector2 Deep(Vector2 p, float k)
        {
            Vector2 shore = Shore(p);
            return shore + Inward(shore) * (Depth(shore) * k);
        }
    }
}
