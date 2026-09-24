namespace ZooTycoon.Core
{
    // 설계 13 v0.6 · 데이터-테이블-규칙 8.11: 사물 행(InteractableTable)에 붙은 업그레이드 데이터. 없으면 그 사물은 업그레이드가 없다.
    // 비용 = baseCost × costGrowth^단계, 효과 = effectPerLevel × 단계(값의 뜻은 사물이 UpgradeValue로 정한다)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class UpgradeInfo
    {
        public string Name { get; set; }
        public double BaseCost { get; set; }
        public double CostGrowth { get; set; }
        public int MaxLevel { get; set; }
        public double EffectPerLevel { get; set; }
        // 이 단계부터 사물 외형이 바뀐다(0 = 안 바뀜). 오븐만 쓴다
        public int LookLevel { get; set; }
        // 효과 전후 문구("용량 {0} → {1}")
        public string EffectFormat { get; set; }

        public double Cost(int level)
        {
            return BaseCost * System.Math.Pow(CostGrowth, level);
        }
    }
}
