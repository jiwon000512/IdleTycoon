using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13: 팔 수 있는 흙 칸. 거리는 칸 사각형까지, 행동은 파기 시트
    public sealed class DigInteractable : Interactable
    {
        public const string k_Id = "dig";

        private readonly BakeryLayout m_layout;

        public Cell Cell { get; }

        public DigInteractable(InteractableTable table, Cell cell, BakeryLayout layout) : base(table)
        {
            Cell = cell;
            m_layout = layout;
        }

        public override float DistanceTo(Vector2 p)
        {
            return m_layout.DistanceToCell(Cell, p);
        }
    }
}
