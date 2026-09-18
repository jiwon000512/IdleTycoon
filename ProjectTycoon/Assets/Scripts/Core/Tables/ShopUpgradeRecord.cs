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
    }
}
