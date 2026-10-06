namespace ZooTycoon.Core
{
    // 설계 44: 물길에 떠 있는 물고기 하나. 무게만큼 감기면 낚이고, 물길 끝에 닿으면 놓친다.
    // 월척은 처음 감는 대가 붙잡아 멈추고(HookedBy), 웜뱃 털썩 · 점원이 거들어야 낚인다.
    // 설계 49 대물(Boss): 물때 10번째 떼 끝에 하나. 붙잡히지 않고 지나가는 대마다 이어서 감는다
    public sealed class Fish
    {
        public FishTable Kind { get; }
        // 크기 번호(0 작은 · 1 보통 · 2 큰)
        public int Size { get; }
        public bool Trophy { get; }
        public bool Boss { get; }
        public double Weight { get; }
        // 헤엄 속도(유닛/초)
        public double Speed { get; }
        public double Reeled { get; internal set; }
        // 물길 길이 위 자리
        public float S { get; internal set; }
        public double StunLeft { get; internal set; }
        public StakeInteractable HookedBy { get; internal set; }
        // 붙잡힌 지 몇 초(점원이 거드는 때를 잰다)
        public double HookedFor { get; internal set; }
        public bool Big => Size == 2;

        public Fish(FishTable kind, int size, bool trophy, double weight, bool boss = false, double speedScale = 1d)
        {
            Kind = kind;
            Size = size;
            Trophy = trophy;
            Weight = weight;
            Boss = boss;
            Speed = kind.Speed * speedScale;
        }
    }
}
