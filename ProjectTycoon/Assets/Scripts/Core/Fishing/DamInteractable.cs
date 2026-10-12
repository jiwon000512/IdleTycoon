using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53: 물이 찬 구간과 마른 강바닥 사이의 잔해 댐. 둑에서 대상이 되면 「끌어내기」 버튼으로 잔해 한 조각을 낚아(줄다리기) 끌어낸다.
    // 조각을 다 빼면 그 구간에 물이 찬다. 둑 앞 물(강) 사물보다 먼저 대상이 된다
    public sealed class DamInteractable : Interactable
    {
        public const string k_Id = "dam";
        // 댐이 막는 폭(경계 양쪽) · 둑 선에서 댐 발끝까지 깊이
        private const float k_HalfWidth = 1.1f;
        private const float k_Drop = 2.2f;

        public FishingArea Fishing { get; }
        public FishingStretchTable Stretch { get; }
        // 댐 발끝(경계 x · 물속)
        public Vector2 Position { get; }
        // 남은 조각
        public int PiecesLeft => Stretch.DamPieces - Fishing.PulledFrom(Stretch.Index);

        public override int TargetPriority => -1;

        internal DamInteractable(InteractableTable table, FishingArea fishing, FishingStretchTable stretch) : base(table, fishing)
        {
            Fishing = fishing;
            Stretch = stretch;
            Position = new Vector2(fishing.Layout.InnerEdge(stretch.Index), fishing.Layout.BankY - k_Drop);
        }

        public override float DistanceTo(Vector2 p)
        {
            float bank = Fishing.Layout.BankY;
            return new NavRect(Position.X - k_HalfWidth, Position.Y, Position.X + k_HalfWidth, bank).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return new Vector2(Position.X, Fishing.Layout.BankY + 0.2f);
        }
    }
}
