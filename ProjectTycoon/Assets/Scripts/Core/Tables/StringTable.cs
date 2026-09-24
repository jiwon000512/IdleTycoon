using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 데이터-테이블-규칙 8.7: 화면 문구(StringTable.json 행). 조회는 Texts.Text·Format
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class StringTable : Table<string>
    {
        public string Ko { get; set; }
    }
}
