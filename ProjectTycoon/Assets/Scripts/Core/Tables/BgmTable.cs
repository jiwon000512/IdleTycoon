using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 23 · 데이터-테이블-규칙 8.19: 곳마다 배경음악(BgmTable.json 행). id = 곳. 어느 곳인지는 코드(World AreaBgm)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class BgmTable : Table<string>
    {
        public const string k_Bakery = "bakery";
        public const string k_Plaza = "plaza";

        // Resources/ 기준, 확장자 없음
        public string Clip { get; set; }
        public double Volume { get; set; }
    }
}
