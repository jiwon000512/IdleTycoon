using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 39 · 데이터-테이블-규칙 8.25: 농장 층(FarmFloorTable.json 행 = 층, 행 순서가 위 → 아래). 행 id가 곧 그 층의 곳 id(첫 행 = FarmArea.k_Id)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FarmFloorTable : Table<string>
    {
        // 한 층의 칸 범위(가로 가운데 정렬, 세로 첫 줄은 입구 줄. 칸 크기는 ConfigTable)
        public int FloorCols { get; set; }
        public int FloorRows { get; set; }
        // 처음 판 줄 수(입구 줄부터, 가운데 두 열)와 갈아 둔 밭 수
        public int StartRows { get; set; }
        public int StartFields { get; set; }
        // 파기 값 = digBaseCost × digCostGrowth^(판 칸 − 시작 칸). 갈기 값은 고정
        public double DigBaseCost { get; set; }
        public double DigCostGrowth { get; set; }
        public double TillCost { get; set; }
        // 이 층을 여는 계단 값(위층을 다 판 뒤 치른다). 첫 층은 0(처음부터 열림)
        public double OpenCost { get; set; }
        // 굴 그림에 곱하는 색(#RRGGBB). 층별 그림이 오기 전 임시
        public string Tint { get; set; }
    }
}
