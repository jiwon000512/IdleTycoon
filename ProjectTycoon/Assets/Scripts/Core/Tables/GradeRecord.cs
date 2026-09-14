namespace ZooTycoon.Core
{
    // 기획서 6.1 · 데이터-테이블-규칙 8.2
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class GradeRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int GachaWeight { get; set; }
        public string ColorHex { get; set; }
        public int SortOrder { get; set; }
    }
}
