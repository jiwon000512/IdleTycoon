using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5: 파낸 칸의 집합·파기 규칙·비용(길 찾기는 손님 동선 설계 v0.2의 BurrowNav). 시작은 가운데 두 열(−1·0) × 네 줄(입구·자리·계산대·자리).
    // 팔 수 있는 칸 = 안 판 칸 중 파낸 칸과 상하좌우로 붙고 입구 줄(row 0)이 아닌 곳. 비용 = digBaseCost × digCostGrowth^(판 칸 수)
    public sealed class BurrowGrid
    {
        public const int k_EntranceRow = 0;
        public const int k_CounterRow = 2;
        private const int k_StartCells = 8;

        private static readonly Cell[] k_Around = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };

        private readonly ZooState m_state;
        private readonly BakeryConfigTable m_config;
        private readonly HashSet<Cell> m_cells = new HashSet<Cell>();

        public IReadOnlyCollection<Cell> Cells => m_cells;
        public double DigCost => m_config.DigBaseCost * Math.Pow(m_config.DigCostGrowth, m_cells.Count - k_StartCells);
        public Cell Counter => new Cell(0, k_CounterRow);

        public event Action<Cell> Dug;

        public BurrowGrid(ZooState state, BakeryConfigTable config)
        {
            m_state = state;
            m_config = config;

            for (int row = 0; row < 4; row++)
            {
                m_cells.Add(new Cell(-1, row));
                m_cells.Add(new Cell(0, row));
            }
        }

        public bool Contains(Cell cell)
        {
            return m_cells.Contains(cell);
        }

        // 자리(진열대·오븐)를 놓을 수 있는 칸: 입구 줄·계산대 줄이 아닌 파낸 칸
        public bool HasSlot(Cell cell)
        {
            return m_cells.Contains(cell) && cell.Row != k_EntranceRow && cell.Row != k_CounterRow;
        }

        public bool CanDig(Cell cell)
        {
            if (cell.Row <= k_EntranceRow || m_cells.Contains(cell))
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

        public bool TryDig(Cell cell)
        {
            if (!CanDig(cell) || !m_state.TrySpendCoins(DigCost))
            {
                return false;
            }

            m_cells.Add(cell);
            OnDug(cell);
            return true;
        }

        private void OnDug(Cell cell)
        {
            Dug?.Invoke(cell);
        }
    }
}
