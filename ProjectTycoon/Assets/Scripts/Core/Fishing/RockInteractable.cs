using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 53: 열린 낚시터 끝의 바위벽(다음 구간을 막음). 둑 끝 벽 앞에 서면 대상이 되어 「뚫기」 시트(굴 파기와 같은 시트 · 동작)
    public sealed class RockInteractable : Interactable
    {
        public const string k_Id = "rock";
        // 바위가 서는 둑 끝 띠: 벽에서 안쪽으로 폭 · 둑 선에서 위로 높이
        private const float k_Reach = 1.2f;
        private const float k_Height = 2.4f;

        public FishingArea Fishing { get; }
        public FishingStretchTable Stretch { get; }
        // 바위 발끝(벽 바로 안쪽 · 둑 선 위)
        public Vector2 Position { get; }
        // 벽 쪽이 오른쪽이면 1, 왼쪽이면 −1
        public int Side => Math.Sign(Stretch.Index);

        internal RockInteractable(InteractableTable table, FishingArea fishing, FishingStretchTable stretch) : base(table, fishing)
        {
            Fishing = fishing;
            Stretch = stretch;
            float edge = fishing.Layout.InnerEdge(stretch.Index);
            Position = new Vector2(edge - Side * 0.6f, fishing.Layout.BankY + 0.25f);
        }

        public override float DistanceTo(Vector2 p)
        {
            float edge = Fishing.Layout.InnerEdge(Stretch.Index);
            float inner = edge - Side * k_Reach;
            float bank = Fishing.Layout.BankY;
            return new NavRect(Math.Min(edge, inner), bank, Math.Max(edge, inner), bank + k_Height).DistanceTo(p);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }
    }
}
