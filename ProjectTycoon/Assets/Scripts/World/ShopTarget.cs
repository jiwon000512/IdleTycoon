namespace ZooTycoon.World
{
    public enum ShopTargetKind
    {
        Shelf,
        LockedShelf,
        Oven,
        LockedOven,
        Counter,
    }

    // 사물 터치 기획(2026-09-23): 가게 안에서 탭한 사물. Index는 진열대 칸 번호 / 오븐 번호(계산대는 0)
    public readonly struct ShopTarget
    {
        public ShopTargetKind Kind { get; }
        public int Index { get; }

        public ShopTarget(ShopTargetKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public bool Equals(ShopTarget other)
        {
            return Kind == other.Kind && Index == other.Index;
        }
    }
}
