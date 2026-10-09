using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 38: 농장 점원 자리(입구 오른쪽 모종 작업대 앞, 그림은 농장 입구 장식). 놓는 사물이 아니라 편집 · 길 막기 · 웜뱃 대상에 들지 않는다.
    // 기준점 = FarmConfigTable barnX · barnY, 점원이 서는 곳은 InteractableTable barn의 worker 자리
    public sealed class BarnInteractable : Interactable, IPlaced
    {
        public const string k_Id = "barn";

        public FarmArea Farm { get; }
        public Vector2 Position { get; }
        public IPlacedKind Kind => Table;

        public BarnInteractable(InteractableTable table, FarmArea farm, Vector2 position) : base(table, farm)
        {
            Farm = farm;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public override Vector2? GuidePoint(Vector2 from)
        {
            return Position;
        }

        public void MoveTo(Vector2 position)
        {
            throw new InvalidOperationException("농장 작업대는 옮기지 않는다.");
        }
    }
}
