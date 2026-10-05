using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 44: 낚은 대물(구멍 옆 둑에 누워 있다). 대물 뱃속 3택 1이 남아 있는 동안만 있고, 버튼으로 시트를 열어 특별한 대 하나를 고른다
    public sealed class BossCatchInteractable : Interactable
    {
        public const string k_Id = "boss_catch";

        public FishingArea Fishing { get; }
        public Vector2 Position { get; }

        internal BossCatchInteractable(InteractableTable table, FishingArea fishing, Vector2 position) : base(table, fishing)
        {
            Fishing = fishing;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }
    }
}
