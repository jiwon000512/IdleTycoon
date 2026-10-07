using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 · 설계 53: 낚시터 배치의 단일 출처. 굴은 구간(칸 stretchCols열 × rows줄)을 옆으로 이어 붙인 판 칸 굴이고(농장 · 횟집과 같은 칸 굴),
    // 방 아래쪽을 가로지르는 강(Water 사각형, 물이 찬 구간까지)과 열렸지만 댐 뒤라 마른 강바닥(DryBeds)은 걸을 수 없다.
    // 웜뱃은 둑(물가) 위에서 물 쪽을 보고 던진다. 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위. 구간 0의 가운데가 x 0
    public sealed class FishingLayout
    {
        private readonly FishingConfigTable m_config;
        private readonly List<NavRect> m_dryBeds = new List<NavRect>();

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        // 광장으로 가는 구멍(구간 0 첫 줄 가운데, 빵집 · 농장 구멍과 같은 자리)
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        public Vector2 HoleInside => new Vector2(0f, -(BurrowShape.k_EntranceFloorTop - 1) / BurrowShape.k_PixelsPerUnit);
        // 물 사각형(물이 찬 구간들, 굴 밖으로 나간 부분은 굴 그림에 잘린다)
        public NavRect Water { get; private set; }
        public IReadOnlyList<NavRect> DryBeds => m_dryBeds;
        // 둑 선(물 위 선)
        public float BankY => (float)m_config.Water.Y1;
        public float StretchWidth => m_config.StretchCols * Cells.CellWidth;

        public FishingLayout(TableSet tables)
        {
            m_config = tables.Get<FishingConfigTable>(FishingConfigTable.k_Main);
            Cells = new CellMetrics(tables);
            Rebuild(new[] { 0 }, new[] { 0 });
        }

        public int StretchOf(float x)
        {
            return (int)Math.Floor(x / StretchWidth + 0.5f);
        }

        public float CenterOf(int stretch)
        {
            return stretch * StretchWidth;
        }

        public float LeftOf(int stretch)
        {
            return (stretch - 0.5f) * StretchWidth;
        }

        public float RightOf(int stretch)
        {
            return (stretch + 0.5f) * StretchWidth;
        }

        // 구간 0 쪽 경계(바위 · 댐이 서는 x)
        public float InnerEdge(int stretch)
        {
            return stretch > 0 ? LeftOf(stretch) : RightOf(stretch);
        }

        // 열린 구간의 칸으로 굴을 다시 만들고, 물이 찬 구간까지 물 · 나머지는 마른 강바닥(물이 찬 구간은 늘 이어져 있다)
        public void Rebuild(IReadOnlyCollection<int> opened, IReadOnlyCollection<int> flooded)
        {
            HashSet<Cell> cells = new HashSet<Cell>();

            foreach (int stretch in opened)
            {
                int first = stretch * m_config.StretchCols - m_config.StretchCols / 2;

                for (int col = first; col < first + m_config.StretchCols; col++)
                {
                    for (int row = 0; row < m_config.Rows; row++)
                    {
                        cells.Add(new Cell(col, row));
                    }
                }
            }

            Shape = Cells.Build(cells);
            float bottom = (float)m_config.Water.Y0;
            Water = new NavRect(LeftOf(flooded.Min()), bottom, RightOf(flooded.Max()), BankY);
            m_dryBeds.Clear();

            foreach (int stretch in opened.Where(s => !flooded.Contains(s)))
            {
                m_dryBeds.Add(new NavRect(LeftOf(stretch), bottom, RightOf(stretch), BankY));
            }

            Nav = new BurrowNav(Shape, new[] { Water }.Concat(m_dryBeds).ToList());
        }

        // p에서 가장 가까운 물가(물 사각형 둘레의 점). 물 안이면 가장 가까운 변으로
        public Vector2 Shore(Vector2 p)
        {
            float x = Math.Clamp(p.X, Water.XMin, Water.XMax);
            float y = Math.Clamp(p.Y, Water.YMin, Water.YMax);

            if (x != p.X || y != p.Y)
            {
                return new Vector2(x, y);
            }

            float left = p.X - Water.XMin;
            float right = Water.XMax - p.X;
            float bottom = p.Y - Water.YMin;
            float top = Water.YMax - p.Y;
            float nearest = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
            return nearest == top ? new Vector2(p.X, Water.YMax) : nearest == bottom ? new Vector2(p.X, Water.YMin) : nearest == left ? new Vector2(Water.XMin, p.Y) : new Vector2(Water.XMax, p.Y);
        }

        // 물가 점에서 물 안쪽으로 향하는 단위 벡터(그 변의 안쪽 법선)
        public Vector2 Inward(Vector2 shore)
        {
            if (shore.Y >= Water.YMax)
            {
                return new Vector2(0f, -1f);
            }

            if (shore.Y <= Water.YMin)
            {
                return new Vector2(0f, 1f);
            }

            return shore.X <= Water.XMin ? new Vector2(1f, 0f) : new Vector2(-1f, 0f);
        }

        // 그 물가에서 건너편까지 거리
        public float Depth(Vector2 shore)
        {
            return Inward(shore).X == 0f ? Water.YMax - Water.YMin : Water.XMax - Water.XMin;
        }

        // p에서 본 물가에서 물 안쪽으로 k(0 = 물가, 1 = 건너편)만큼 들어간 점
        public Vector2 Deep(Vector2 p, float k)
        {
            Vector2 shore = Shore(p);
            return shore + Inward(shore) * (Depth(shore) * k);
        }
    }
}
