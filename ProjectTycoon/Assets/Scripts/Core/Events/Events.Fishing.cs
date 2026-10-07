namespace ZooTycoon.Core
{
    // 설계 52: 우물 낚시 사건. 찌 · 물고기 자리 · 긴장 · 거리는 화면이 매 프레임 곳(FishingArea)에서 읽는다
    public static partial class Events
    {
        // 던졌다(찌가 날아가 뜬다)
        public readonly struct CastThrown
        {
            public readonly FishingArea Fishing;

            public CastThrown(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 가짜 입질(찌가 까딱)
        public readonly struct Nibbled
        {
            public readonly FishingArea Fishing;

            public Nibbled(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 물었다(찌가 쑥 잠김 · 소리 · 진동). 틈 안에 낚아채야 한다
        public readonly struct Bitten
        {
            public readonly FishingArea Fishing;

            public Bitten(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 걸렸다(줄다리기 시작)
        public readonly struct Hooked
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;

            public Hooked(FishingArea fishing, Fish fish)
            {
                Fishing = fishing;
                Fish = fish;
            }
        }

        // 물고기가 달리기 시작했다(true) · 쉰다(false)
        public readonly struct RunChanged
        {
            public readonly FishingArea Fishing;
            public readonly bool Running;

            public RunChanged(FishingArea fishing, bool running)
            {
                Fishing = fishing;
                Running = running;
            }
        }

        // 낚았다(재료는 이미 창고에): 물고기가 튀어 오르고 이름 · 무게 알약
        public readonly struct FishLanded
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;

            public FishLanded(FishingArea fishing, Fish fish)
            {
                Fishing = fishing;
                Fish = fish;
            }
        }

        // 줄이 빈 줄로 돌아갔다(거둠 · 놓침 · 끊김)
        public readonly struct FishingEnded
        {
            public readonly FishingArea Fishing;
            public readonly FishingEnd Reason;

            public FishingEnded(FishingArea fishing, FishingEnd reason)
            {
                Fishing = fishing;
                Reason = reason;
            }
        }
    }
}
