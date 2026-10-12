using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53 → 설계 58: 닫힌 위 층(Stretch)을 막는 바위벽. 집 쪽 이웃 층(바로 아래) 둑 줄 왼쪽 끝 윗벽에 서고, 앞에 서면 대상이 되어 「뚫기」 시트(굴 파기와 같은 시트 · 동작)
    public sealed class RockInteractable : Interactable
    {
        public const string k_Id = "rock";
        // 바위 앞 띠: 발끝에서 양옆 · 아래로
        private const float k_HalfWidth = 1.2f;
        private const float k_Reach = 1.2f;
        private const float k_Drop = 0.4f;

        public FishingArea Fishing { get; }
        public FishingStretchTable Stretch { get; }
        // 바위가 서는 열린 이웃 층(바로 아래)
        public int Neighbor => Stretch.Index + 1;
        // 바위 발끝(이웃 층 물 왼쪽 옆 둑 열 가운데 · 둑 줄 윗벽 아래, 물이 넓어지면 따라 옮긴다)
        public Vector2 Position
        {
            get
            {
                FishingLayout layout = Fishing.Layout;
                return new Vector2(layout.WaterOf(Neighbor).XMin - layout.ColumnWidth * 0.5f, layout.BankTopOf(Neighbor) - k_Drop);
            }
        }

        internal RockInteractable(InteractableTable table, FishingArea fishing, FishingStretchTable stretch) : base(table, fishing)
        {
            Fishing = fishing;
            Stretch = stretch;
        }

        public override float DistanceTo(Vector2 p)
        {
            Vector2 at = Position;
            return new NavRect(at.X - k_HalfWidth, at.Y - k_Reach, at.X + k_HalfWidth, at.Y + k_Drop).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }
    }
}
