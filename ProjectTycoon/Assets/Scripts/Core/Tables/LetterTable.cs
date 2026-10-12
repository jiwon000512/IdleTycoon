using System.Collections.Generic;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 59 · 데이터-테이블-규칙 8.39: 우편함 편지(LetterTable.json 행). when이 되면 한 번 온다: start:<걸음 id>(걸음이 시작될 때) · done:<걸음 id>(다 됐을 때) · area:<곳 id>(들어갈 때).
    // from은 보낸 이(StringTable sender_<from> · 도장 그림 Sprites/Letters/<from>), lines는 StringTable id들(차례로), 읽으면 coins · item을 준다.
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class LetterTable : Table<string>
    {
        public const string k_WhenStart = "start";
        public const string k_WhenDone = "done";
        public const string k_WhenArea = "area";

        public string When { get; set; }
        public string From { get; set; }
        public List<string> Lines { get; set; } = new List<string>();
        public double Coins { get; set; }
        public string Item { get; set; }
        public int ItemCount { get; set; }
    }
}
