using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 물가(물길 둘레 어디든). 버튼은 엉덩이 쿵. 말뚝이 곁에 있으면 말뚝에 대상을 양보한다
    public sealed class StreamBankInteractable : Interactable
    {
        public const string k_Id = "stream_bank";

        public FishingArea Fishing { get; }

        internal StreamBankInteractable(InteractableTable table, FishingArea fishing) : base(table, fishing)
        {
            Fishing = fishing;
        }

        public override int TargetPriority => 1;

        // 물가까지 거리(물 위는 0)
        public override float DistanceTo(Vector2 p)
        {
            return Math.Max(0f, Fishing.Layout.DistanceToStream(p) - Fishing.Layout.StreamWidth / 2f);
        }
    }
}
