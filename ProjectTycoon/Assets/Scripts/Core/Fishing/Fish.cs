namespace ZooTycoon.Core
{
    // 설계 52: 줄에 걸린 물고기 하나(어종 · 무게 kg). 낚으면 어종의 재료 하나가 창고로
    public sealed class Fish
    {
        public FishTable Kind { get; }
        public double Weight { get; }

        public Fish(FishTable kind, double weight)
        {
            Kind = kind;
            Weight = weight;
        }
    }

    // 설계 44: 어종 하나의 기록(도감 화면은 다음). Sizes = 본 크기 비트(옛 저장 칸, 지금은 쓰지 않는다)
    public sealed class FishRecord
    {
        public int Caught;
        public int Sizes;
        public double Best;
    }
}
