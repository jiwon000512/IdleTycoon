using ZooTycoon.Core;

namespace ZooTycoon.World
{
    public enum ShopTargetKind
    {
        Shelf,
        Oven,
        Counter,
        EmptySlot,
        Dig,
    }

    // 사물 터치 기획(2026-09-23): 가게 안에서 탭한 사물. 굴 격자 설계 v0.5: Cell = 그 사물의 칸(진열대·빈 자리·파기), Index = 오븐 번호
    public readonly struct ShopTarget
    {
        public ShopTargetKind Kind { get; }
        public Cell Cell { get; }
        public int Index { get; }

        public ShopTarget(ShopTargetKind kind, Cell cell, int index = 0)
        {
            Kind = kind;
            Cell = cell;
            Index = index;
        }
    }
}
