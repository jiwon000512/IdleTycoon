namespace ZooTycoon.Core
{
    // 기획서 6.1 · 데이터-테이블-규칙 8.1 (version 2: 연출·행동 값 포함)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다(프로그래밍-규약 5장 불변 원칙). init은 netstandard 2.1에서 쓸 수 없다.
    public sealed class AnimalRecord : IUnitRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Grade { get; set; }
        public double BaseIncomePerSecond { get; set; }
        public int GachaWeight { get; set; }
        public string Sprite { get; set; }
        public string IdleSheet { get; set; }
        public string MoveSheet { get; set; }
        public double FrameRate { get; set; }
        public double Scale { get; set; }
        public double MoveSpeed { get; set; }
        public double IdleSecondsMin { get; set; }
        public double IdleSecondsMax { get; set; }
        public string VariantId { get; set; }
        public int SortOrder { get; set; }
    }
}
