using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53 → 설계 58: 닫힌 아래 층(Stretch) 앞의 통나무 댐. 집 쪽 이웃 층 물 바닥 가운데에 서고, 그 위 둑에서 대상이 되면 「끌어내기」 버튼으로 잔해 한 조각을 낚아(줄다리기) 끌어낸다.
    // 조각을 다 빼면 아래 층이 열린다. 물 사물보다 먼저 대상이 된다
    public sealed class DamInteractable : Interactable
    {
        public const string k_Id = "dam";
        // 댐이 막는 폭(가운데 양쪽) · 물 바닥에서 댐 발끝까지
        private const float k_HalfWidth = 0.6f;
        private const float k_Rise = 0.6f;

        public FishingArea Fishing { get; }
        public FishingStretchTable Stretch { get; }
        // 댐이 서는 열린 이웃 층(바로 위)
        public int Neighbor => Stretch.Index - 1;
        // 댐 발끝(이웃 층 물 바닥 가운데, 물이 넓어지면 따라 옮긴다)
        public Vector2 Position
        {
            get
            {
                NavRect water = Fishing.Layout.WaterOf(Neighbor);
                return new Vector2((water.XMin + water.XMax) * 0.5f, water.YMin + k_Rise);
            }
        }

        // 남은 조각
        public int PiecesLeft => Stretch.DamPieces - Fishing.PulledFrom(Stretch.Index);
        public override int TargetPriority => -1;

        internal DamInteractable(InteractableTable table, FishingArea fishing, FishingStretchTable stretch) : base(table, fishing)
        {
            Fishing = fishing;
            Stretch = stretch;
        }

        // 댐 위 물 띠(둑 선까지)까지 거리: 그 위 둑에 서면 0
        public override float DistanceTo(Vector2 p)
        {
            Vector2 at = Position;
            return new NavRect(at.X - k_HalfWidth, at.Y, at.X + k_HalfWidth, Fishing.Layout.WaterTopOf(Neighbor)).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return new Vector2(Position.X, Fishing.Layout.WaterTopOf(Neighbor) + 0.2f);
        }
    }
}
