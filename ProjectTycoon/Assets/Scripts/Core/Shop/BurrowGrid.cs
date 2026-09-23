using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5: 파낸 칸의 집합·파기 규칙·비용·칸 사이 경로. 시작은 가운데 두 열(−1·0) × 네 줄(입구·자리·계산대·자리).
    // 팔 수 있는 칸 = 안 판 칸 중 파낸 칸과 상하좌우로 붙고 입구 줄(row 0)이 아닌 곳. 비용 = digBaseCost × digCostGrowth^(판 칸 수)
    public sealed class BurrowGrid
    {
        public const int k_EntranceRow = 0;
        public const int k_CounterRow = 2;
        private const int k_StartCells = 8;

        private static readonly Cell[] k_Around = { new Cell(1, 0), new Cell(-1, 0), new Cell(0, 1), new Cell(0, -1) };

        private readonly ZooState m_state;
        private readonly GameConfig.ShopConfig m_config;
        private readonly HashSet<Cell> m_cells = new HashSet<Cell>();
        private readonly Dictionary<Cell, Cell> m_parents = new Dictionary<Cell, Cell>();
        private readonly Queue<Cell> m_queue = new Queue<Cell>();

        public IReadOnlyCollection<Cell> Cells => m_cells;
        public double DigCost => m_config.DigBaseCost * Math.Pow(m_config.DigCostGrowth, m_cells.Count - k_StartCells);
        public Cell Counter => new Cell(0, k_CounterRow);

        // 입구·계산대는 가운데 두 칸(−1·0)이라 목적지 쪽 칸에서 출발한다
        public Cell EntranceNear(Cell target)
        {
            return new Cell(target.Col < 0 ? -1 : 0, k_EntranceRow);
        }

        public Cell CounterNear(Cell target)
        {
            return new Cell(target.Col < 0 ? -1 : 0, k_CounterRow);
        }

        public event Action<Cell> Dug;

        public BurrowGrid(ZooState state, GameConfig.ShopConfig config)
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

        // 너비 우선 최단 경로. from·to 포함. 없으면 빈 목록(파낸 칸은 항상 이어져 있어 정상이면 안 생긴다)
        public IReadOnlyList<Cell> Path(Cell from, Cell to)
        {
            List<Cell> path = new List<Cell>();
            m_parents.Clear();
            m_queue.Clear();
            m_parents[from] = from;
            m_queue.Enqueue(from);

            while (m_queue.Count > 0)
            {
                Cell cell = m_queue.Dequeue();

                if (cell.Equals(to))
                {
                    for (Cell c = to; !c.Equals(from); c = m_parents[c])
                    {
                        path.Add(c);
                    }

                    path.Add(from);
                    path.Reverse();
                    return path;
                }

                foreach (Cell d in k_Around)
                {
                    Cell next = cell.Offset(d.Col, d.Row);

                    if (m_cells.Contains(next) && !m_parents.ContainsKey(next))
                    {
                        m_parents[next] = cell;
                        m_queue.Enqueue(next);
                    }
                }
            }

            return path;
        }

        // 경로 길이(유닛). 가로 걸음 = cellWidth, 세로 걸음 = cellHeight
        public double Distance(Cell from, Cell to)
        {
            IReadOnlyList<Cell> path = Path(from, to);
            double length = 0d;

            for (int i = 1; i < path.Count; i++)
            {
                length += path[i].Row != path[i - 1].Row ? m_config.CellHeight : m_config.CellWidth;
            }

            return length;
        }

        private void OnDug(Cell cell)
        {
            Dug?.Invoke(cell);
        }
    }
}
