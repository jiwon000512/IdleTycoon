using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 52: 물(한 층의 웅덩이가 사물 하나). 둑에 서면 대상이 되어 던지기 · 낚아채기 · 감기 버튼(행동은 ActionFactory.Fishing.cs, 하는 일은 FishingArea).
    // 설계 58: 층마다 하나. 네모는 배치에서 읽는다(둑 칸을 파면 넓어진다)
    public sealed class WaterInteractable : Interactable
    {
        public const string k_Id = "water";

        public FishingArea Fishing { get; }
        public int Index { get; }
        public FishingStretchTable Stretch => Fishing.StretchOf(Index);
        public NavRect Rect => Fishing.Layout.WaterOf(Index);

        internal WaterInteractable(InteractableTable table, FishingArea fishing, int index) : base(table, fishing)
        {
            Fishing = fishing;
            Index = index;
        }

        // 물가까지의 거리(물 안이면 0)
        public override float DistanceTo(Vector2 p)
        {
            return Rect.DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return new Vector2(System.Math.Clamp(from.X, Rect.XMin, Rect.XMax), Rect.YMax + 0.2f);
        }
    }
}
