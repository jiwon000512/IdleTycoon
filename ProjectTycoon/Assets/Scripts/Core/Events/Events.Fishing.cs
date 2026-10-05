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
