using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13: 진열대도 오븐도 없는 자리. 시트에서 다음 빵 진열대·오븐을 놓는다
    public sealed class SlotInteractable : Interactable
    {
        public const string k_Id = "slot";

        private readonly Vector2 m_base;

        public Cell Cell { get; }
        public BakeryArea Bakery { get; }

        public SlotInteractable(InteractableTable table, Cell cell, BakeryArea bakery) : base(table, bakery)
        {
            Cell = cell;
            Bakery = bakery;
            m_base = bakery.Layout.SlotBase(cell);
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_base);
        }
    }
}
