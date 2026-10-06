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

        // 물고기가 물길 막다른 끝에서 빠져나갔다(물보라 · 소리)
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

        // 물때가 시작됐다(팻말 튐)
        public readonly struct WaveStarted
        {
            public readonly FishingArea Fishing;

            public WaveStarted(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 설계 50: 대물을 낚았다(보상은 이미 들어왔다). 그림자가 Boss의 모습으로 드러나고 낚시 소식이 뜬다. 단계는 확인(ConfirmLanded)해야 오른다
        public readonly struct BossLanded
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;
            public readonly BossTable Boss;
            public readonly StakeInteractable Stake;

            public BossLanded(FishingArea fishing, Fish fish, BossTable boss, StakeInteractable stake)
            {
                Fishing = fishing;
                Fish = fish;
                Boss = boss;
                Stake = stake;
            }
        }

        // 설계 49 · 50: 낚시 소식을 확인해 단계가 올랐다(팻말 · 곳 이름 띠 「N단계!」, 새 계열이 열렸으면 그 이름)
        public readonly struct StageRaised
        {
            public readonly FishingArea Fishing;

            public StageRaised(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 설계 49: 대물이 나왔다(팻말 「대물!」 · 소리) · 대물이 막다른 끝으로 빠져나갔다(물때는 1부터 다시)
        public readonly struct BossSpawned
        {
            public readonly FishingArea Fishing;

            public BossSpawned(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        public readonly struct BossEscaped
        {
            public readonly FishingArea Fishing;

            public BossEscaped(FishingArea fishing)
            {
                Fishing = fishing;
            }
        }

        // 설계 49: 그물 계열 대가 사거리 안 물고기를 멈췄다(말뚝이 튀고 사거리에 물결)
        public readonly struct NetCast
        {
            public readonly StakeInteractable Stake;

            public NetCast(StakeInteractable stake)
            {
                Stake = stake;
            }
        }

        // 설계 49: From의 대를 Stake의 같은 종류 대에 합쳤다(From은 비고 Stake는 윗 종류)
        public readonly struct RodMerged
        {
            public readonly StakeInteractable Stake;
            public readonly StakeInteractable From;

            public RodMerged(StakeInteractable stake, StakeInteractable from)
            {
                Stake = stake;
                From = from;
            }
        }

        // 설계 46: 말뚝에 대를 샀다(단 뽑기 연출 · 드묾 · 전설 글). 옛 대가 있었으면 사라졌다
        public readonly struct RodSummoned
        {
            public readonly StakeInteractable Stake;

            public RodSummoned(StakeInteractable stake)
            {
                Stake = stake;
            }
        }

        // 설계 46: 소용돌이(미끼 노점 업그레이드)가 맨 앞 물고기를 From에서 지금 자리로 되돌렸다(물 소용돌이 · 미끄러짐)
        public readonly struct FishWhirled
        {
            public readonly FishingArea Fishing;
            public readonly Fish Fish;
            public readonly float From;

            public FishWhirled(FishingArea fishing, Fish fish, float from)
            {
                Fishing = fishing;
                Fish = fish;
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
    }
}
