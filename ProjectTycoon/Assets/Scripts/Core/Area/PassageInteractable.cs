using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13 · 리뷰 R2: 다른 곳으로 옮겨 가는 사물(빵집 구멍 앞 나가기 exit · 광장 빵집 문 door). 기준점은 그 아래 바닥, 지나가면 Passed를 내고 Mall이 옮긴다
    public sealed class PassageInteractable : Interactable
    {
        public const string k_Exit = "exit";
        public const string k_Door = "door";

        private readonly Vector2 m_floor;

        public PassageInteractable(InteractableTable table, WombatArea area, Vector2 floor) : base(table, area)
        {
            m_floor = floor;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_floor);
        }

        public void Pass()
        {
            Area.Bus.Publish(new Events.Passed(Area));
        }
    }
}
