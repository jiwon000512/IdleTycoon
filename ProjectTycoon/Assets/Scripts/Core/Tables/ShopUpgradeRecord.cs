namespace ZooTycoon.Core
{
    // 설계 08 v0.5 · 데이터-테이블-규칙 8.9. 비용 = baseCost × costGrowth^단계, 효과 = effectPerLevel × 단계
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ShopUpgradeRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double BaseCost { get; set; }
        public double CostGrowth { get; set; }
        public int MaxLevel { get; set; }
        public double EffectPerLevel { get; set; }
        // 이 단계부터 소품 외형이 바뀐다(0 = 안 바뀜). version 2: oven_speed만 쓴다
        public int LookLevel { get; set; }
        // 사물 터치 기획(version 3): 어느 사물 시트에서 파는지(shelf·oven·counter), 효과 전후 문구("용량 {0} → {1}")
        public string Target { get; set; }
        public string EffectFormat { get; set; }
    }
}
