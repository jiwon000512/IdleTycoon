using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 40 · 데이터-테이블-규칙 8.26: 가게 별 평가(StarConfigTable.json 행 = 가게, id = 곳 id). n = 지금 별 수(다음 평가는 ★n+1). 숫자는 모두 시작값
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StarConfigTable : Table<string>
    {
        // 평가 시간 = min(timeBase + timePerStar × n, timeMax). 큰 평가(다음 별이 bigEvery의 배수)는 bigTime
        public double TimeBase { get; set; }
        public double TimePerStar { get; set; }
        public double TimeMax { get; set; }
        public double BigTime { get; set; }
        public int BigEvery { get; set; }
        // 평가 중 손님 간격 ÷ rush(몰려오는 손님 = 맛 평가단)
        public double Rush { get; set; }
        // 평가단 만족(계산까지) = serveBase + servePerStar × n명
        public int ServeBase { get; set; }
        public int ServePerStar { get; set; }
        // 정해진 빵 = sellBase + ⌊sellPerStar × n⌋개. 해금한 빵을 별마다 돌리고, 큰 평가는 다음 빵도
        public int SellBase { get; set; }
        public double SellPerStar { get; set; }
        // 실망한 평가단(빈손으로 나감) 상한 = max(0, lostBase − ⌊n / lostEvery⌋)
        public int LostBase { get; set; }
        public int LostEvery { get; set; }
        // 별마다 쌓이는 능력(축복 · 유물과 같은 효과 배수에 곱한다): 빵 값 · 손님
        public double PriceBonus { get; set; }
        public double VisitorsBonus { get; set; }
        // 팁: tipFrom별부터 계산마다 확률 min(tipMax, tipBase + tipPerStar × (n − tipFrom)), 팁 = 그 계산 값 × tipRate
        public int TipFrom { get; set; }
        public double TipBase { get; set; }
        public double TipPerStar { get; set; }
        public double TipMax { get; set; }
        public double TipRate { get; set; }
        // 통과 보상 재료(ItemTable) · 개수. 떨어진 뒤 다시 부를 때까지 쉬는 시간(초)
        public string RewardItem { get; set; }
        public int RewardCount { get; set; }
        public double Cooldown { get; set; }
        // 평가판(입구 칠판) 앞 기준점 · 평가단장이 서는 곳(가게 원점 기준 유닛) · 평가단장 외형(VisitorTable judge 행)
        public double BoardX { get; set; }
        public double BoardY { get; set; }
        public double JudgeX { get; set; }
        public double JudgeY { get; set; }
        public string JudgeLook { get; set; }
        // 광장 손님 감탄 ✨ 확률 = admirePerStar × 별(과시)
        public double AdmirePerStar { get; set; }
    }
}
