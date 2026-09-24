using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 업그레이드를 파는 사물 시트
    public enum UpgradeTarget
    {
        Shelf,
        Oven,
        Counter,
        Slot,
    }

    // 설계 08 v0.5 · 데이터-테이블-규칙 8.9: 빵집 업그레이드(ShopUpgradeTable.json 행). 비용 = baseCost × costGrowth^단계, 효과 = effectPerLevel × 단계
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ShopUpgradeTable : Table<string>
    {
        public string Name { get; set; }
        public double BaseCost { get; set; }
        public double CostGrowth { get; set; }
        public int MaxLevel { get; set; }
        public double EffectPerLevel { get; set; }
        // 이 단계부터 소품 외형이 바뀐다(0 = 안 바뀜). oven_speed만 쓴다
        public int LookLevel { get; set; }
        // 사물 터치 기획: 어느 사물 시트에서 파는지, 효과 전후 문구("용량 {0} → {1}")
        public UpgradeTarget Target { get; set; }
        public string EffectFormat { get; set; }
    }
}
