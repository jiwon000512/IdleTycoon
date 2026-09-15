namespace ZooTycoon.Core
{
    // 기획서 6.4 시설 · 데이터-테이블-규칙 8.6 (version 1)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FacilityRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sprite { get; set; }
        public double SlotX { get; set; }
        public double SlotZ { get; set; }
        public int UnlockLevel { get; set; }
        public int MaxStage { get; set; }
        public double MultiplierPerStage { get; set; }
        public int BaseCost { get; set; }
        public double CostGrowth { get; set; }
        public int SortOrder { get; set; }
    }
}
