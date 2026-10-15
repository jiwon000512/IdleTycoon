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

        // 설계 61: 다리를 놓아 그 섬에 닿았다(걷는 땅 · 좌대 · 물고기, 다음 섬이 보인다)
        public readonly struct IslandReached
        {
            public readonly FishingArea Fishing;
            public readonly int Island;

            public IslandReached(FishingArea fishing, int island)
            {
                Fishing = fishing;
                Island = island;
            }
        }

        // 설계 61: 명물 그림자가 그 섬 아래 물에 나타났다(사라짐은 화면이 매 프레임 ShadowVisible로 읽는다)
        public readonly struct ShadowShown
        {
            public readonly FishingArea Fishing;
            public readonly FishingIslandTable Island;

            public ShadowShown(FishingArea fishing, FishingIslandTable island)
            {
                Fishing = fishing;
                Island = island;
            }
        }

        // 설계 61: 상자가 떠내려왔다 · 끌어냈다(코인은 이미 지갑에)
        public readonly struct CrateDrifted
        {
            public readonly FishingArea Fishing;
            public readonly CrateInteractable Crate;

            public CrateDrifted(FishingArea fishing, CrateInteractable crate)
            {
                Fishing = fishing;
                Crate = crate;
            }
        }

        public readonly struct CratePulled
        {
            public readonly FishingArea Fishing;
            public readonly CrateInteractable Crate;
            public readonly double Coins;

            public CratePulled(FishingArea fishing, CrateInteractable crate, double coins)
            {
                Fishing = fishing;
                Crate = crate;
                Coins = coins;
            }
        }

        // 설계 53: 좌대를 놓았다
        public readonly struct SeatPlaced
        {
            public readonly FishingArea Fishing;
            public readonly SeatInteractable Seat;

            public SeatPlaced(FishingArea fishing, SeatInteractable seat)
            {
                Fishing = fishing;
                Seat = seat;
            }
        }

        // 설계 53: 좌대 손님이 구멍에서 나왔다 · 한 마리 낚았다(값은 이미 지갑에) · 구멍으로 나갔다
        public readonly struct FishingVisitorArrived
        {
            public readonly FishingVisitor Visitor;

            public FishingVisitorArrived(FishingVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        public readonly struct FishingVisitorCaught
        {
            public readonly FishingVisitor Visitor;
            public readonly FishTable Kind;
            public readonly double Coins;

            public FishingVisitorCaught(FishingVisitor visitor, FishTable kind, double coins)
            {
                Visitor = visitor;
                Kind = kind;
                Coins = coins;
            }
        }

        public readonly struct FishingVisitorLeft
        {
            public readonly FishingVisitor Visitor;

            public FishingVisitorLeft(FishingVisitor visitor)
            {
                Visitor = visitor;
            }
        }
    }
}
