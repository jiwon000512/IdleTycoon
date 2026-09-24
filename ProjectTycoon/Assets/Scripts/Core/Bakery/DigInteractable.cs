using System.Numerics;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 · 설계 13: 팔 수 있는 흙 칸. 거리는 칸 사각형까지, 시트에서 판다
    public sealed class DigInteractable : Interactable
    {
        public const string k_Id = "dig";

        public Cell Cell { get; }
        public BakeryArea Bakery { get; }

        public DigInteractable(InteractableTable table, Cell cell, BakeryArea bakery) : base(table, bakery)
        {
            Cell = cell;
            Bakery = bakery;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Bakery.Layout.DistanceToCell(Cell, p);
        }
    }
}
