using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 24 → 설계 37: 웜뱃이 떨군 네모 똥 하나(id poop). 기준점 = 똥 자리. 빵집 · 광장 · 농장 어디서나 그 곳이 목록을 맞추고, 치우면 목록에서 빠진다
    public sealed class PoopInteractable : Interactable
    {
        public const string k_Id = "poop";

        public Vector2 Position { get; }

        public PoopInteractable(InteractableTable table, Vector2 position, WombatArea area) : base(table, area)
        {
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }
    }
}
