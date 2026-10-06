using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44 · 46: 미끼 노점(오두막 앞). 점원 자리이자 웜뱃이 단계를 올리는 곳(버튼 → 시트 줄). 놓는 사물이 아니라 편집 · 길 막기에 들지 않는다.
    // 기준점 = FishingConfigTable hutX · hutY, 점원이 서는 곳은 InteractableTable fishing_hut의 worker 자리
    public sealed class FishingHutInteractable : Interactable, IPlaced
    {
        public const string k_Id = "fishing_hut";

        public FishingArea Fishing { get; }
        public Vector2 Position { get; }
        public IPlacedKind Kind => Table;

        internal FishingHutInteractable(InteractableTable table, FishingArea fishing, Vector2 position) : base(table, fishing)
        {
            Fishing = fishing;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public void MoveTo(Vector2 position)
        {
            throw new InvalidOperationException("낚시터 오두막은 옮기지 않는다.");
        }
    }
}
