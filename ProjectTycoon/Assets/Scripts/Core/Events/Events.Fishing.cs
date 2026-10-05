using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 낚시터 사건. 물고기 · 대 · 물때의 자리는 화면이 매 프레임 곳에서 읽는다(말뚝이 바뀌면 ThingChanged)
    public static partial class Events
    {
        // 낚았다: 창고에 그 물고기 재료 count개(팝업 · 소리). Stake = 감은 말뚝(털썩 · 점원이 거든 월척도 그 말뚝)
        public readonly struct FishCaught
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;
            public readonly int Count;
            public readonly StakeInteractable Stake;

            public FishCaught(FishingArea fishing, Fish fish, int count, StakeInteractable stake)
            {
                Fishing = fishing;
                Fish = fish;
                Count = count;
                Stake = stake;
            }
        }

        // 물고기가 물길 막다른 끝에서 빠져나갔다(놓침 팻말 · 물보라). 대물이면 BossResolved도 온다
        public readonly struct FishEscaped
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;

            public FishEscaped(FishingArea fishing, Fish fish)
            {
                Fishing = fishing;
                Fish = fish;
            }
        }

        // 물때가 시작됐다(팻말 튐 · 대물이면 「대물이 온다!」)
        public readonly struct WaveStarted
        {
            public readonly FishingArea Fishing;
            public readonly bool Boss;

            public WaveStarted(FishingArea fishing, bool boss)
            {
                Fishing = fishing;
                Boss = boss;
            }
        }

        // 빈 말뚝에 대를 소환했다(등급마다 다른 반짝임 · 드묾 · 전설 글)
        public readonly struct RodSummoned
        {
            public readonly StakeInteractable Stake;

            public RodSummoned(StakeInteractable stake)
            {
                Stake = stake;
            }
        }

        // 같은 대 셋을 합쳤다: From = 비워진 짝 말뚝들(대가 Into로 날아간다)
        public readonly struct RodsMerged
        {
            public readonly StakeInteractable Into;
            public readonly StakeInteractable[] From;

            public RodsMerged(StakeInteractable into, StakeInteractable[] from)
            {
                Into = into;
                From = from;
            }
        }

        // 설계 45: 물길을 팠다(From → To 길이 사이가 새 물, 물길 그림을 다시 칠한다)
        public readonly struct StreamDug
        {
            public readonly FishingArea Fishing;
            public readonly float From;
            public readonly float To;

            public StreamDug(FishingArea fishing, float from, float to)
            {
                Fishing = fishing;
                From = from;
                To = to;
            }
        }

        // 엉덩이 쿵(물결 · 소리)
        public readonly struct Thumped
        {
            public readonly FishingArea Fishing;
            public readonly Vector2 At;

            public Thumped(FishingArea fishing, Vector2 at)
            {
                Fishing = fishing;
                At = at;
            }
        }

        // 대물이 끝났다(낚았으면 다음 단계, 놓쳤으면 같은 단계 물때 1부터)
        public readonly struct BossResolved
        {
            public readonly FishingArea Fishing;
            public readonly bool Caught;

            public BossResolved(FishingArea fishing, bool caught)
            {
                Fishing = fishing;
                Caught = caught;
            }
        }

        // 대물 뱃속 3택 1이 나왔다(팝업)
        public readonly struct RodChoiceReady
        {
            public readonly FishingArea Fishing;

            public RodChoiceReady(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }
    }
}
