using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 49 · 데이터-테이블-규칙 8.29: 낚싯대 종류(RodTable.json 행 = 종류 하나, 별 없음). 같은 종류 둘을 합치면 그 계열의 Tier + 1 종류.
    // 말뚝 시트에는 Tier 1 종류가 계열 칩으로 나오고, 사면 단을 뽑아(FishingConfigTable tierWeights) 그 계열의 그 단 종류가 꽂힌다
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class RodTable : Table<string>
    {
        public const string k_Bamboo = "bamboo";
        public const string k_Iron = "iron";
        public const string k_Bait = "bait";
        public const string k_Net = "net";

        // 계열(계열 문턱을 센다) · 계열 안의 단(1부터)
        public string Family { get; set; }
        public int Tier { get; set; } = 1;
        // 낚시터 단계가 이 값 이상이면 시트에 나온다(Tier 1 행만 본다)
        public int UnlockStage { get; set; } = 1;
        // 초당 감는 힘 · 사거리 반지름(유닛)
        public double Reel { get; set; }
        public double Range { get; set; }
        // 크기 배수: 작은 것 · 큰 것(보통은 1)
        public double SmallScale { get; set; } = 1d;
        public double BigScale { get; set; } = 1d;
        // 사거리 안 물고기 속도 배수(떡밥, 1 = 그대로)
        public double Slow { get; set; } = 1d;
        // 한꺼번에 감는 마리 수(사거리 안 맨 앞부터)
        public int Targets { get; set; } = 1;
        // 0보다 크면 월척을 붙잡지 않고 그대로 감고, 월척 · 대물에 감는 힘 × 이 값
        public double BigGame { get; set; }
        // 사거리 안 대물 속도 배수(1 = 그대로)
        public double BossSlow { get; set; } = 1d;
        // StunEvery초마다 사거리 안 물고기를 Stun초 멈춘다(0 = 없음)
        public double StunEvery { get; set; }
        public double Stun { get; set; }
        // 사거리 안에서 낚인 물고기의 재료에 더하는 개수
        public int Bonus { get; set; }
        // 시트 칩 아이콘(Resources/ 기준 경로, 확장자 없음. Tier 1 행만)
        public string Icon { get; set; }
    }
}
