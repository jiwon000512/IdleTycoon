using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 44 · 데이터-테이블-규칙 8.28: 낚시터 숫자(FishingConfigTable.json 행, id main). 좌표는 낚시터 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 방 칸 수(광장처럼 파지 않는 고정 방)
        public int Cols { get; set; }
        public int Rows { get; set; }
        // 물길: 축 정렬 선분을 잇는 점(첫 점 = 물고기가 나오는 곳, 끝 점 = 끝까지 팠을 때의 막다른 끝)과 폭. 판 데는 걸을 수 없다
        public FishingPointData[] Stream { get; set; }
        public double StreamWidth { get; set; }
        // 설계 45: 처음 판 길이 · 한 번 파면 늘어나는 길이(유닛) · 처음 파기 값 · 팔 때마다 값 배수
        public double DugStart { get; set; }
        public double DigStep { get; set; }
        public double DigCost { get; set; }
        public double DigGrowth { get; set; }
        // 말뚝 자리(값 0 = 처음부터 열림)
        public FishingStakeData[] Stakes { get; set; }
        // 미끼 노점(점원이 서는 바닥, 웜뱃이 단계를 올리는 곳)
        public double HutX { get; set; }
        public double HutY { get; set; }
        // 물때: 떼가 나오는 간격 · 떼 안 물고기 간격(초) · 떼 마릿수(기본 + 단계마다) · 단계마다 무게 배수
        public double WaveSeconds { get; set; }
        public double SpawnGap { get; set; }
        public int SchoolBase { get; set; }
        public double SchoolPerStage { get; set; }
        public double WeightGrowth { get; set; }
        // 월척: 떼 물고기 하나가 월척일 확률 · 무게 배수 · 낚으면 재료 개수 · 털썩 동작 초
        public double TrophyChance { get; set; }
        public double TrophyWeightScale { get; set; }
        public int TrophyCatch { get; set; }
        public double HaulSeconds { get; set; }
        // 설계 46 대 사기: 처음 값 · 살 때마다 값 배수 · 등급 비중[보통, 드묾, 전설](뽑기) · 등급마다 감는 힘 배수
        public double SummonCost { get; set; }
        public double SummonGrowth { get; set; }
        public double[] GradeWeights { get; set; }
        public double GradeScale { get; set; }
        // 설계 46 단계 올리기(미끼 노점): 2단계로 가는 값 · 단계마다 값 배수
        public double StageCost { get; set; }
        public double StageGrowth { get; set; }
        // 설계 46 미끼 노점 업그레이드(불빛 · 소용돌이, 예전 설치물 효과) · 소용돌이가 맨 앞 물고기를 되돌리는 거리(유닛)
        public FishingUpgradeData[] HutUpgrades { get; set; }
        public double WhirlDistance { get; set; }
        // 계열 문턱: 판의 같은 계열 개수가 familySteps[i] 이상이면 그 계열 감는 힘 × familyScales[i]
        public int[] FamilySteps { get; set; }
        public double[] FamilyScales { get; set; }
        // 엉덩이 쿵: 반지름 · 멈추는 초 · 쉬는 초 · 웜뱃이 제자리에 서는 초
        public double ThumpRadius { get; set; }
        public double ThumpStun { get; set; }
        public double ThumpCooldown { get; set; }
        public double ThumpSeconds { get; set; }
    }

    // 설계 46 미끼 노점 업그레이드 한 줄. 값 = baseCost × costGrowth^단계, 단계 0 = 없음 · 최대 maxLevel. 효과는 코드가 id로 읽는다:
    // light = 모든 대 사거리 × (1 + effectPerLevel × 단계) · whirl = 되돌리는 간격 effectBase − effectPerLevel × (단계 − 1)초
    public sealed class FishingUpgradeData
    {
        public const string k_Light = "light";
        public const string k_Whirl = "whirl";

        public string Id { get; set; }
        public string Name { get; set; }
        public double BaseCost { get; set; }
        public double CostGrowth { get; set; }
        public int MaxLevel { get; set; }
        public double EffectBase { get; set; }
        public double EffectPerLevel { get; set; }
        // 효과 전후 문구({0} 지금 {1} 다음, 숫자는 효과 값)
        public string EffectFormat { get; set; }
    }

    public sealed class FishingPointData
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class FishingStakeData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Cost { get; set; }
    }
}
