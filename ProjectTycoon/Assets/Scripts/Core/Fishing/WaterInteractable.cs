using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 52: 강(물 전체가 사물 하나). 둑에 서면 대상이 되어 던지기 · 낚아채기 · 감기 버튼(행동은 ActionFactory.Fishing.cs, 하는 일은 FishingArea).
    // 설계 53: 물이 찬 구간이 늘면 사각형도 넓어진다(배치에서 읽는다)
    public sealed class WaterInteractable : Interactable
    {
        public const string k_Id = "water";

        public FishingArea Fishing { get; }
        public NavRect Rect => Fishing.Layout.Water;

        internal WaterInteractable(InteractableTable table, FishingArea fishing) : base(table, fishing)
        {
            Fishing = fishing;
        }

        // 물가까지의 거리(물 안이면 0)
        public override float DistanceTo(Vector2 p)
        {
            return Rect.DistanceTo(p);
        }
    }
}
