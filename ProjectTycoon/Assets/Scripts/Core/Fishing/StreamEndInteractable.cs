using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 45: 물길 막다른 끝 바로 앞 땅(정해진 길에서 다음에 팔 한 칸의 가운데). 버튼은 물길 파기. 끝까지 팠으면 사물 목록에서 빠진다
    public sealed class StreamEndInteractable : Interactable
    {
        public const string k_Id = "stream_end";

        public FishingArea Fishing { get; }

        internal StreamEndInteractable(InteractableTable table, FishingArea fishing) : base(table, fishing)
        {
            Fishing = fishing;
        }

        public Vector2 Position => Fishing.Layout.PlanPointAt(Fishing.Layout.Length + (float)Fishing.Config.DigStep / 2f);

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }
    }
}
