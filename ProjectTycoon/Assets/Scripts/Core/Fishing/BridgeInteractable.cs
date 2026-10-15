using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 61: 닿은 섬에서 다음 섬으로 놓을 다리 자리(FishingIslandTable bridge). 다리 네모 곁 뭍에 서면 대상이 되어 「다리 놓기」 시트(좌대와 같은 규칙).
    // 놓으면 사물 목록에서 빠지고 그 섬에 닿는다(다리 그림은 배치 Layout.Bridges가 준다)
    public sealed class BridgeInteractable : Interactable
    {
        public const string k_Id = "bridge";

        public FishingArea Fishing { get; }
        // 건너갈 섬(다리는 그 섬 행의 것)
        public FishingIslandTable Island { get; }
        public NavRect Rect => Island.Bridge.ToRect();
        // 다리 가운데(그림 · 값 표식 자리)
        public Vector2 Position => new Vector2((Rect.XMin + Rect.XMax) / 2f, (Rect.YMin + Rect.YMax) / 2f);

        internal BridgeInteractable(InteractableTable table, FishingArea fishing, FishingIslandTable island) : base(table, fishing)
        {
            Fishing = fishing;
            Island = island;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Rect.DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }
    }
}
