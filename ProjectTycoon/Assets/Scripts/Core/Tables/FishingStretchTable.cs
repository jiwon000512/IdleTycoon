using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 53 → 설계 58 층계 웅덩이 · 데이터-테이블-규칙 8.34: 낚시터 층(FishingStretchTable.json 행). 층은 세로로 쌓인 웅덩이 하나(둑 줄 + 물 줄 waterRows), index 0이 집 층,
    // 음수는 위(상류, 바위벽 뒤) · 양수는 아래(하류, 통나무 댐 뒤). 집 쪽 이웃 층이 열려 있으면 damPieces > 0이면 댐 잔해를 끌어내 열고, 아니면 바위를 rockCost로 뚫어 연다.
    // 열린 층은 물 startCols칸으로 시작해 옆 둑 칸을 파며 maxCols까지 넓힌다(값 = cellCost × cellCostGrowth^판 칸 수, 빵집 굴과 같은 식).
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingStretchTable : Table<string>
    {
        public int Index { get; set; }
        public double RockCost { get; set; }
        public int DamPieces { get; set; }
        // 물: 처음 열 수 · 최대 열 수 · 한 칸 넓히는 값과 성장
        public int StartCols { get; set; }
        public int MaxCols { get; set; }
        public double CellCost { get; set; }
        public double CellCostGrowth { get; set; }
        public FishingStretchFishData[] Fish { get; set; } = new FishingStretchFishData[0];
        // 좌대 자리(층 가운데에서 dx, 물 위 둑 선에서 seatBack)와 놓는 값
        public FishingSeatData[] Seats { get; set; } = new FishingSeatData[0];
        // 이 층 좌대 손님이 한 마리 낚을 때 내는 코인
        public double Pay { get; set; }
    }

    public sealed class FishingStretchFishData
    {
        public string Id { get; set; }
        public double Chance { get; set; }
    }

    public sealed class FishingSeatData
    {
        public double Dx { get; set; }
        public double Cost { get; set; }
    }
}
