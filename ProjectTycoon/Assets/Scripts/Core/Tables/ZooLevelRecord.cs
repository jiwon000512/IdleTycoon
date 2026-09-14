namespace ZooTycoon.Core
{
    // 기획서 6.4 · 데이터-테이블-규칙 8.3
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class ZooLevelRecord
    {
        public int Level { get; set; }
        public double RequiredTotalCoins { get; set; }
        public int CageCount { get; set; }
        public bool UnlocksPromotion { get; set; }
    }
}
