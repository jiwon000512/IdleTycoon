using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 53 → 설계 58 층계 웅덩이 · 데이터-테이블-규칙 8.34: 낚시터 층(FishingStretchTable.json 행). 층은 세로로 쌓인 웅덩이 하나(둑 줄 + 물 줄 waterRows), index 0이 집 층,
    // 음수는 위(상류, 바위벽 뒤) · 양수는 아래(하류, 통나무 댐 뒤). 집 쪽 이웃 층이 열려 있으면 damPieces > 0이면 댐 잔해를 끌어내 열고, 아니면 바위를 rockCost로 뚫어 연다.
    // 열린 층은 물 startCols칸으로 시작해 비버에게 먹이(feeds, 한 열마다)를 주면 한 열씩 maxCols까지 넓어진다(설계 60). 다 넓히면 아래 층 댐을 damFeeds로 하나씩 헐어 연다.
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingStretchTable : Table<string>
    {
        public int Index { get; set; }
        public double RockCost { get; set; }
        public int DamPieces { get; set; }
        // 물: 처음 열 수 · 최대 열 수 · 한 열마다 비버 먹이(maxCols − startCols개) · 아래 층이면 댐 조각마다 먹이(damPieces개)
        public int StartCols { get; set; }
        public int MaxCols { get; set; }
        public FishingFeedData[] Feeds { get; set; } = new FishingFeedData[0];
        public FishingFeedData[] DamFeeds { get; set; } = new FishingFeedData[0];
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

    public sealed class FishingFeedData
    {
        public string Item { get; set; }
        public int Count { get; set; }
    }

    public sealed class FishingSeatData
    {
        public double Dx { get; set; }
        public double Cost { get; set; }
    }
}
