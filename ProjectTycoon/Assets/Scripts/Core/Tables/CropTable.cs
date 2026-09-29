using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 · 데이터-테이블-규칙 8.21: 밭에 심는 작물(CropTable.json 행). 심기는 첫 행을 심는다(작물이 늘면 시트로 고른다).
    // 자라는 그림은 sprite_0 ~ _(stages − 1), 마지막 그림이 익음
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class CropTable : Table<string>
    {
        // 거두면 창고에 드는 재료(ItemTable id)
        public string Item { get; set; }
        // 심고 익을 때까지(초)
        public double GrowSeconds { get; set; }
        // 한 번 거두면 나오는 개수
        public int Yield { get; set; }
        // 자라는 그림(Resources/ 기준, 확장자 없음, 뒤에 _단계)
        public string Sprite { get; set; }
        public int Stages { get; set; }
    }
}
