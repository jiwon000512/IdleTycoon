using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 53 낚시터 넓히기 · 데이터-테이블-규칙 8.34: 낚시터 구간(FishingStretchTable.json 행). 구간 = 굴 칸 stretchCols열(한 화면 폭), index 0이 처음 낚시터, 음수는 왼쪽(상류) · 양수는 오른쪽(하류).
    // 안쪽 구간이 열려 있으면 경계의 바위를 rockCost로 뚫어 땅이 열리고, 물속 댐 조각 damPieces개를 낚아 끌어내면 물이 찬다. 물이 찬 구간에서 던지면 fish 중 하나가 문다(chance 비중)
    // 규칙 예외: Newtonsoft 역직렬화에 setter가 필요하다.
    public sealed class FishingStretchTable : Table<string>
    {
        public int Index { get; set; }
        public double RockCost { get; set; }
        public int DamPieces { get; set; }
        public FishingStretchFishData[] Fish { get; set; } = new FishingStretchFishData[0];
        // 좌대 자리(구간 가운데에서 dx, 둑 선 위 seatBack)와 놓는 값. 물이 찬 구간에만 생긴다
        public FishingSeatData[] Seats { get; set; } = new FishingSeatData[0];
        // 이 구간 좌대 손님이 한 마리 낚을 때 내는 코인
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
