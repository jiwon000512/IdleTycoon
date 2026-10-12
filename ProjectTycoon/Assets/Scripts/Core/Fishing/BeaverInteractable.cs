using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 60(사용자 2026-10-12 「비버한테 물고기를 주면 늘릴 수 있게」): 열린 층마다 물 오른쪽 둑에 앉은 비버. 톡하면 시트 줄 둘 —
    // 둑 갉기(물고기를 주면 둑 한 칸을 갉아 물이 한 열 넓어진다, 표 feeds) · 다 넓히면 댐 헐기(아래 층 댐 조각을 하나씩, 표 damFeeds, 0이 되면 아래 층이 열린다)
    public sealed class BeaverInteractable : Interactable
    {
        public const string k_Id = "beaver";
        // 물 오른쪽 끝에서 비버까지 · 물 윗선에서 발끝까지
        private const float k_Beside = 0.8f;
        private const float k_Up = 0.9f;

        public FishingArea Fishing { get; }
        public FishingStretchTable Stretch { get; }
        public int Index => Stretch.Index;
        // 발끝(물이 넓어지면 따라 옮긴다)
        public Vector2 Position => new Vector2(Fishing.Layout.WaterOf(Index).XMax + k_Beside, Fishing.Layout.WaterTopOf(Index) + k_Up);
        public int Cols => Fishing.Waters[Index].Cells.Count;
        public bool CanWiden => Cols < Stretch.MaxCols;
        // 다음 둑 갉기 먹이(다 넓혔으면 null)
        public FishingFeedData NextWiden => CanWiden ? Stretch.Feeds[Cols - Stretch.StartCols] : null;
        // 바로 아래 닫힌 층의 댐(없으면 null)
        public DamInteractable Dam => Fishing.DamBelow(Index);
        // 다음 댐 헐기 먹이(물을 다 넓혀야 하고 댐이 있어야 한다)
        public FishingFeedData NextDamFeed => !CanWiden && Dam != null ? Dam.Stretch.DamFeeds[Fishing.PulledFrom(Dam.Stretch.Index)] : null;

        internal BeaverInteractable(InteractableTable table, FishingArea fishing, FishingStretchTable stretch) : base(table, fishing)
        {
            Fishing = fishing;
            Stretch = stretch;
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
