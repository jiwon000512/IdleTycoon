using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27 · 데이터-테이블-규칙 8.22: 농장 굴 숫자(FarmConfigTable.json 행, id main)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FarmConfigTable : Table<string>
    {
        public const string k_Main = "main";

        // 한 층의 칸 범위(가로 가운데 정렬, 세로 첫 줄은 입구 줄. 칸 크기는 ConfigTable)
        public int FloorCols { get; set; }
        public int FloorRows { get; set; }
        // 새 게임에 판 줄 수(입구 줄부터, 가운데 두 열)와 갈아 둔 밭 수
        public int StartRows { get; set; }
        public int StartFields { get; set; }
        // 파기 값 = digBaseCost × digCostGrowth^(판 칸 − 시작 칸). 갈기 값은 고정
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
        public double TillCost { get; set; }
    }
}
