using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 52 · 61: 호수(물 전체가 사물 하나). 물가에 서면 대상이 되어 던지기 · 낚아채기 · 감기 버튼(행동은 ActionFactory.Fishing.cs, 하는 일은 FishingArea).
    // 물은 걷는 땅(섬 뭍 · 다리) 밖의 굴 바닥 전부라 거리는 가장 가까운 물가까지(배치에서 읽는다)
    public sealed class WaterInteractable : Interactable
    {
        public const string k_Id = "water";

        public FishingArea Fishing { get; }

        internal WaterInteractable(InteractableTable table, FishingArea fishing) : base(table, fishing)
        {
            Fishing = fishing;
        }

        // 물가까지의 거리(물 안이면 0)
        public override float DistanceTo(Vector2 p)
        {
            return Fishing.Layout.IsWater(p) ? 0f : Vector2.Distance(p, Fishing.Layout.Shore(p, out _));
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            Vector2 shore = Fishing.Layout.Shore(from, out Vector2 inward);
            return shore - inward * 0.2f;
        }
    }
}
