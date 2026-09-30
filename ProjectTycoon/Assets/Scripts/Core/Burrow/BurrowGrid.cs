using System;
using System.Collections.Generic;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 → 설계 27: 파낸 칸의 집합·파기 규칙·비용(길 찾기는 손님 동선 설계 v0.2의 BurrowNav). 빵집·농장 공용 — 시작 칸·비용 곡선·층 범위는 곳이 준다.
    // 팔 수 있는 칸 = 안 판 칸 중 파낸 칸과 상하좌우로 붙고 입구 줄(row 0)이 아니며 층 범위 안인 곳. 비용 = digBaseCost × digCostGrowth^(판 칸 수 − 시작 칸 수). 값은 파기 행동이 치른다.
    // 사물 자리는 설계 18부터 칸이 아니라 자유 좌표(농장 밭은 설계 27부터 칸)
    public sealed class BurrowGrid
    {
        public const int k_EntranceRow = 0;

        private static readonly Cell[] k_Around = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };

        private readonly double m_digBaseCost;
        private readonly double m_digCostGrowth;
        private readonly int m_startCount;
        private readonly EventBus m_bus;
        private readonly HashSet<Cell> m_cells = new HashSet<Cell>();

        public IReadOnlyCollection<Cell> Cells => m_cells;
        public CellBounds Bounds { get; }
        public double DigCost => m_digBaseCost * Math.Pow(m_digCostGrowth, m_cells.Count - m_startCount);

        public BurrowGrid(IEnumerable<Cell> start, double digBaseCost, double digCostGrowth, CellBounds bounds, EventBus bus)
        {
            m_digBaseCost = digBaseCost;
            m_digCostGrowth = digCostGrowth;
            Bounds = bounds;
            m_bus = bus;

            foreach (Cell cell in start)
            {
                m_cells.Add(cell);
            }

            m_startCount = m_cells.Count;
        }

        // 가운데 정렬 cols열 × 입구 줄부터 rows줄(빵집 시작 2×4 · 농장 시작 2×startRows)
        public static IEnumerable<Cell> Columns(int cols, int rows)
        {
            int firstCol = -cols / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int col = firstCol; col < firstCol + cols; col++)
                {
                    yield return new Cell(col, row);
                }
            }
        }

        public bool Contains(Cell cell)
        {
            return m_cells.Contains(cell);
        }

        public bool CanDig(Cell cell)
        {
            if (cell.Row <= k_EntranceRow || m_cells.Contains(cell) || !Bounds.Contains(cell))
            {
                return false;
            }

            foreach (Cell d in k_Around)
            {
                if (m_cells.Contains(cell.Offset(d.Col, d.Row)))
                {
                    return true;
                }
            }

            return false;
        }

        // 팔 수 있는 칸 전부. dCol·dRow를 주면 그 방향으로 붙은 칸만(왼쪽 = (−1,0): 오른쪽 이웃이 파낸 칸)
        public IEnumerable<Cell> Frontier(int dCol = 0, int dRow = 0)
        {
            HashSet<Cell> seen = new HashSet<Cell>();

            foreach (Cell cell in m_cells)
            {
                foreach (Cell d in k_Around)
                {
                    Cell next = cell.Offset(d.Col, d.Row);

                    if ((dCol != 0 || dRow != 0) && (d.Col != dCol || d.Row != dRow))
                    {
                        continue;
                    }

                    if (CanDig(next) && seen.Add(next))
                    {
                        yield return next;
                    }
                }
            }
        }

        // 칸을 판다(팔 수 있는지는 부르는 쪽이 CanDig으로)
        public void Dig(Cell cell)
        {
            m_cells.Add(cell);
            m_bus.Publish(new Events.Dug(this, cell));
        }
    }
}
