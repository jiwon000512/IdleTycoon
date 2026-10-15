using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 52 강가 낚시 · 설계 61 지하 호수의 섬들 · 데이터-테이블-규칙 8.28: 낚시터 숫자(FishingConfigTable.json 행, id main). 좌표는 낚시터 원점(구멍 위 가운데) 기준 유닛, y 위
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 설계 61 호수: 보이는 섬 뭍 둘레에 이만큼 더 판 칸이 굴(물)이 된다 · 던지면 물가에서 물 안쪽으로 닿는 최대 거리 · 찌는 그 깊이의 이 비율에 뜬다
        public double LakeMargin { get; set; }
        public double CastReach { get; set; }
        public double BobberIn { get; set; }
        // 입질: 던진 뒤 물기까지 초(사이에서 난수) · 그 전 가짜 입질 최대 횟수 · 물고 나서 낚아챌 수 있는 초
        public double BiteWaitMin { get; set; }
        public double BiteWaitMax { get; set; }
        public int NibbleMax { get; set; }
        public double BiteWindow { get; set; }
        // 줄다리기: 거리 = 무게 × distancePerKg(0이 되면 낚음) · 달리기 초 · 쉼 초 · 달릴 때 놓으면 물고기가 되찾는 거리/초 · 달릴 때 감으면 오르는 긴장/초 · 놓으면 내리는 긴장/초
        public double DistancePerKg { get; set; }
        public double RunSeconds { get; set; }
        public double RestSeconds { get; set; }
        public double RunPull { get; set; }
        public double TensionRise { get; set; }
        public double TensionFall { get; set; }
        // 낚싯대(1회차는 하나): 쉴 때 감는 거리/초 · 줄 힘(긴장이 이 값에 닿으면 끊김)
        public FishingRodData Rod { get; set; }
        // 낚은 뒤 웜뱃이 물고기를 들고 만세하는 초
        public double LandSeconds { get; set; }
        // 설계 61 그림자(event shadow 섬): 나타나는 간격 초 · 보이는 초 · 헤엄 속도(유닛/초) · 찌가 이 거리 안이면 명물이 문다 · 뭍 아랫변 아래 헤엄치는 깊이(lakeMargin 안)
        public double ShadowEvery { get; set; }
        public double ShadowSeconds { get; set; }
        public double ShadowSpeed { get; set; }
        public double ShadowReach { get; set; }
        public double ShadowDepth { get; set; }
        // 설계 61 상자(event crate 섬): 떠내려오는 간격 초 · 머무는 초 · 떠내려가는 속도(유닛/초) · 뭍 아랫변 아래 떠 있는 깊이(lakeMargin 안) · 끌어내는 무게(거리 = 무게 × distancePerKg) · 쉬는 초(사이 runSeconds 「걸림」) · 끌어내면 코인
        public double CrateEvery { get; set; }
        public double CrateSeconds { get; set; }
        public double CrateSpeed { get; set; }
        public double CrateDepth { get; set; }
        public double CrateWeight { get; set; }
        public double CrateRestSeconds { get; set; }
        public double CrateCoins { get; set; }
        // 설계 53 좌대 손님: 좌대가 뭍 아랫변에서 뒤로 물러난 거리 · 앉아 낚는 초(사이 난수) · 한 마리 낚는 간격
        public double SeatBack { get; set; }
        public double CustomerSecondsMin { get; set; }
        public double CustomerSecondsMax { get; set; }
        public double CustomerCatchEvery { get; set; }
    }

    public sealed class FishingRodData
    {
        public double Reel { get; set; }
        public double Line { get; set; }
    }
}
