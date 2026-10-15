using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53 · 61: 닿은 섬의 좌대 자리(표 FishingIslandTable seats). 놓기 전에는 대상이 되어 「좌대 놓기」 시트, 놓은 뒤에는 광장 손님이 앉아 낚는 자리
    public sealed class SeatInteractable : Interactable
    {
        public const string k_Id = "seat";
        // 손님이 앉는 윗판 높이(seat 그림의 앉는 면) · 깡충 뛰기 전에 서는 좌대 뒤 거리
        public const float k_Top = 0.425f;
        public const float k_Behind = 0.35f;

        public FishingArea Fishing { get; }
        public FishingIslandTable Island { get; }
        public int Index { get; }
        public FishingSeatData Row => Island.Seats[Index];
        // 손님이 앉는 발끝(뭍 가운데 + dx, 뭍 아랫변 위 seatBack)
        public Vector2 Position { get; }
        // 손님이 걸어와 서는 좌대 바로 뒤(여기서 깡충 뛰어 앉고, 다 낚으면 여기로 내려온다)
        public Vector2 Behind => Position + new Vector2(0f, k_Behind);
        // 좌대 앞 물가(손님 찌가 뜨는 기준 선)
        public float BankY => (float)Island.Land.Y0;
        public bool Placed { get; internal set; }

        internal SeatInteractable(InteractableTable table, FishingArea fishing, FishingIslandTable island, int index) : base(table, fishing)
        {
            Fishing = fishing;
            Island = island;
            Index = index;
            NavRect land = island.Land.ToRect();
            Position = new Vector2((land.XMin + land.XMax) / 2f + (float)island.Seats[index].Dx, land.YMin + (float)fishing.Config.SeatBack);
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }
    }
}
