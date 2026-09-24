using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 · 데이터-테이블-규칙 8.8: 빵(BreadTable.json 행). 행 순서 = 해금 순서 = 진열 자리
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BreadTable : Table<string>
    {
        public string Name { get; set; }
        public string Sprite { get; set; }
        public double BakeSeconds { get; set; }
        public int BatchSize { get; set; }
        public double Price { get; set; }
        public int Weight { get; set; }
        public double UnlockCost { get; set; }
    }
}
