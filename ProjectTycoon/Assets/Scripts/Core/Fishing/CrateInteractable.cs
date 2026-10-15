using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 61: 상자 섬(event crate) 아래 물에 떠내려오는 상자. 섬 아랫변 둑에 서면 물보다 먼저 대상이 되어 「끌어내기」 버튼 → 줄다리기(무게 crateWeight)로 끌어내면 코인.
    // 자리는 곳이 매 틱 왼쪽으로 옮긴다(떠내려감). 머무는 초가 다 되면 사라진다
    public sealed class CrateInteractable : Interactable
    {
        public const string k_Id = "crate";
        // 상자가 차지하는 폭(양쪽). 뭍 아랫변에서 상자까지 깊이는 FishingConfigTable crateDepth
        private const float k_HalfWidth = 0.6f;

        public FishingArea Fishing { get; }
        public FishingIslandTable Island { get; }
        // 상자 자리(물 위, 그림 발끝)
        public Vector2 Position { get; internal set; }
        // 사라지기까지 남은 초
        public double Left { get; internal set; }

        // 빈 줄이거나 내가 끄는 상자면 물보다 먼저, 줄이 나가 있으면 물에 양보한다(안 그러면 낚아채기 단추가 사라진다, 리뷰 2026-10-14)
        public override int TargetPriority => Fishing.Phase == FishingPhase.Idle || Fishing.Pulling == this ? -1 : 1;

        internal CrateInteractable(InteractableTable table, FishingArea fishing, FishingIslandTable island) : base(table, fishing)
        {
            Fishing = fishing;
            Island = island;
            NavRect land = island.Land.ToRect();
            Position = new Vector2(land.XMax + k_HalfWidth, land.YMin - (float)fishing.Config.CrateDepth);
            Left = fishing.Config.CrateSeconds;
        }

        // 상자에서 뭍 아랫변까지 세운 네모까지의 거리(둑 끝에 서면 가깝다)
        public override float DistanceTo(Vector2 p)
        {
            float bank = (float)Island.Land.Y0;
            return new NavRect(Position.X - k_HalfWidth, Position.Y, Position.X + k_HalfWidth, bank).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return new Vector2(Position.X, (float)Island.Land.Y0 + 0.2f);
        }
    }
}
