using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13: 진열대도 오븐도 없는 자리. 행동은 시트 열기(진열대·오븐 놓기)
    public sealed class SlotInteractable : Interactable
    {
        public const string k_Id = "slot";

        private readonly Vector2 m_base;

        public Cell Cell { get; }

        public SlotInteractable(InteractableTable table, Cell cell, BakeryLayout layout) : base(table)
        {
            Cell = cell;
            m_base = layout.SlotBase(cell);
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_base);
        }
    }
}
