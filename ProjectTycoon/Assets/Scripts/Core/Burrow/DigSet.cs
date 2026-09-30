using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 27: 굴의 파기 사물 묶음(빵집·농장 공용). 프론티어(팔 수 있는 칸)에 맞추되 있던 칸은 같은 객체를 둬 대상이 흔들리지 않는다
    public sealed class DigSet
    {
        private readonly InteractableTable m_row;
        private readonly BurrowGrid m_grid;
        private readonly CellMetrics m_cells;
        private readonly WombatArea m_area;
        private readonly Dictionary<Cell, DigInteractable> m_digs = new Dictionary<Cell, DigInteractable>();
        private readonly List<DigInteractable> m_things = new List<DigInteractable>();

        // 프론티어 순서. 곳이 사물 목록 끝에 붙인다
        public IReadOnlyList<DigInteractable> Things => m_things;

        public DigSet(InteractableTable row, BurrowGrid grid, CellMetrics cells, WombatArea area)
        {
            m_row = row;
            m_grid = grid;
            m_cells = cells;
            m_area = area;
        }

        public void Sync()
        {
            List<Cell> frontier = new List<Cell>(m_grid.Frontier());

            foreach (Cell cell in new List<Cell>(m_digs.Keys))
            {
                if (!frontier.Contains(cell))
                {
                    m_digs.Remove(cell);
                }
            }

            m_things.Clear();

            foreach (Cell cell in frontier)
            {
                if (!m_digs.TryGetValue(cell, out DigInteractable dig))
                {
                    dig = new DigInteractable(m_row, cell, m_grid, m_cells, m_area);
                    m_digs[cell] = dig;
                }

                m_things.Add(dig);
            }
        }

        // 그 점이 든 팔 수 있는 칸. 없으면 null(편집 모드의 흙 탭)
        public DigInteractable At(Vector2 p)
        {
            foreach (DigInteractable dig in m_things)
            {
                if (dig.DistanceTo(p) == 0f)
                {
                    return dig;
                }
            }

            return null;
        }
    }
}
