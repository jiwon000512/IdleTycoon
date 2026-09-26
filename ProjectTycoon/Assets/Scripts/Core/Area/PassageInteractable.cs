using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13 · 리뷰 R2: 다른 곳으로 옮겨 가는 사물(빵집 구멍 앞 나가기 exit · 광장 빵집 문 door). 기준점은 그 아래 바닥, 지나가면 Passed를 내고 Mall이 옮긴다.
    // 2026-09-26 자동 이동: 버튼 없이 range에 들어오면 넘어간다(exit·enter는 auto). 들어온 웜뱃은 통로 위에 서므로 한 번 range 밖으로 나갔다 와야 다시 넘어간다(Armed)
    public sealed class PassageInteractable : Interactable
    {
        public const string k_Exit = "exit";
        public const string k_Door = "door";

        private readonly Vector2 m_floor;

        public bool Armed { get; private set; } = true;

        public PassageInteractable(InteractableTable table, WombatArea area, Vector2 floor) : base(table, area)
        {
            m_floor = floor;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_floor);
        }

        public override void Tick(double dt)
        {
            if (!Area.IsInRange(this))
            {
                Armed = true;
            }
        }

        // 웜뱃이 이 곳에 들어와 통로 위에 섰다
        public void Disarm()
        {
            Armed = false;
        }

        public void Pass()
        {
            Armed = false;
            Area.Bus.Publish(new Events.Passed(Area));
        }
    }
}
