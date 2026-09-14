namespace ZooTycoon.Core
{
    // 데이터-테이블-규칙 8.5
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StringRecord
    {
        public string Id { get; set; }
        public string Ko { get; set; }
    }
}
