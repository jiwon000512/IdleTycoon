using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13 → 설계 27: 팔 수 있는 흙 칸(빵집·농장 공용). 거리는 칸 사각형까지, 시트에서 판다(ActionFactory.Dig가 Grid에 값을 치른다)
    public sealed class DigInteractable : Interactable
    {
        public const string k_Id = "dig";

        private readonly CellMetrics m_cells;

        public Cell Cell { get; }
        public BurrowGrid Grid { get; }

        public DigInteractable(InteractableTable table, Cell cell, BurrowGrid grid, CellMetrics cells, WombatArea area) : base(table, area)
        {
            Cell = cell;
            Grid = grid;
            m_cells = cells;
        }

        public override float DistanceTo(Vector2 p)
        {
            return m_cells.DistanceToCell(Cell, p);
        }

        // 칸 사각형에서 p에 가장 가까운 점(웜뱃이 파는 쪽을 본다)
        public Vector2 ClosestPoint(Vector2 p)
        {
            NavRect r = m_cells.CellRect(Cell);
            return new Vector2(System.Math.Clamp(p.X, r.XMin, r.XMax), System.Math.Clamp(p.Y, r.YMin, r.YMax));
        }
    }
}
