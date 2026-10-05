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
        // 물길: 축 정렬 선분을 잇는 점(첫 점 = 물고기가 나오는 곳, 끝 점 = 빠지는 구멍)과 폭. 걸을 수 없다
        public FishingPointData[] Stream { get; set; }
        public double StreamWidth { get; set; }
        // 말뚝 자리(값 0 = 처음부터 열림)
        public FishingStakeData[] Stakes { get; set; }
        // 점원 오두막 자리(점원이 서는 바닥) · 낚은 대물이 눕는 자리(뱃속 3택 1)
        public double HutX { get; set; }
        public double HutY { get; set; }
        public double CatchX { get; set; }
        public double CatchY { get; set; }
        // 물때: 떼가 나오는 간격 · 떼 안 물고기 간격(초) · 떼 마릿수(기본 + 단계마다) · 단계마다 무게 배수
        public double WaveSeconds { get; set; }
        public double SpawnGap { get; set; }
        public int SchoolBase { get; set; }
        public double SchoolPerStage { get; set; }
        public double WeightGrowth { get; set; }
        // 대물: 이 물때마다 한 번(마지막 물때) · FishTable 행
        public int BossEvery { get; set; }
        public string BossFish { get; set; }
        // 월척: 떼 물고기 하나가 월척일 확률 · 무게 배수 · 낚으면 재료 개수 · 점원이 거들기까지 버티는 초 · 털썩 동작 초
        public double TrophyChance { get; set; }
        public double TrophyWeightScale { get; set; }
        public int TrophyCatch { get; set; }
        public double TrophyHoldSeconds { get; set; }
        public double HaulSeconds { get; set; }
        // 소환: 처음 값 · 소환마다 값 배수 · 등급 비중[보통, 드묾, 전설] · 등급마다 감는 힘 배수 · 합치는 개수
        public double SummonCost { get; set; }
        public double SummonGrowth { get; set; }
        public double[] GradeWeights { get; set; }
        public double GradeScale { get; set; }
        public int MergeCount { get; set; }
        // 계열 문턱: 판의 같은 계열 개수가 familySteps[i] 이상이면 그 계열 감는 힘 × familyScales[i]
        public int[] FamilySteps { get; set; }
        public double[] FamilyScales { get; set; }
        // 엉덩이 쿵: 반지름 · 멈추는 초 · 쉬는 초 · 웜뱃이 제자리에 서는 초
        public double ThumpRadius { get; set; }
        public double ThumpStun { get; set; }
        public double ThumpCooldown { get; set; }
        public double ThumpSeconds { get; set; }
        // 대물 때 앉은 말뚝의 감는 힘 배수
        public double SitScale { get; set; }
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
