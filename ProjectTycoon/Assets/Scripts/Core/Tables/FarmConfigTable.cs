using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 · 데이터-테이블-규칙 8.22: 농장 굴 숫자(FarmConfigTable.json 행, id main)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FarmConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 방 크기(칸): 가로는 가운데 정렬, 세로 첫 줄은 입구 줄(칸 크기는 ConfigTable)
        public int Cols { get; set; }
        public int Rows { get; set; }
        // 새 게임에 놓여 있는 밭 수
        public int StartPlots { get; set; }
    }
}
