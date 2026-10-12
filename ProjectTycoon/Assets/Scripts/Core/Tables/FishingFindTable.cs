using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 58 물속 발견 · 데이터-테이블-규칙 8.38: 둑 칸을 파 물을 넓힐 때 확률로 나오는 것(FishingFindTable.json 행). 행 순서대로 chance를 더해 가며 난수 하나로 하나만 고른다(합이 1 미만이면 나머지는 빈손).
    // item이 있으면 재료 count개, 없으면 코인(그 칸 값 × coinShare). stretch는 층 id(null이면 모든 층).
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingFindTable : Table<string>
    {
        public string Item { get; set; }
        public int Count { get; set; }
        public double CoinShare { get; set; }
        public double Chance { get; set; }
        public string Stretch { get; set; }
    }
}
