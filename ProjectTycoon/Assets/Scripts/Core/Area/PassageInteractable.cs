using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13 · 리뷰 R2: 다른 곳으로 옮겨 가는 사물(빵집 구멍 앞 나가기 exit · 광장 빵집 문 door). 기준점은 그 아래 바닥, 지나가면 Passed를 내고 Mall이 옮긴다.
    // 2026-09-26 자동 이동(사용자: 「동굴과의 거리로만」): 버튼 없이, 통로 앞 띠(가로 ±range, 바닥선에서 k_Enter 이상 올라간 곳)에 들어오면 넘어간다(exit·enter는 auto).
    // 들어온 웜뱃은 바닥선(띠 밖)에 서고 조이스틱을 놓을 때까지 걷지 않으므로(Wombat.WaitRelease) 그 자리에서 되돌아가지 않는다
    public sealed class PassageInteractable : Interactable
    {
        public const string k_Exit = "exit";
        public const string k_Door = "door";

        // 바닥선에서 이만큼 위(벽 쪽)로 올라가야 통로 앞 띠. 바닥선~벽은 약 0.48
        private const float k_Enter = 0.25f;

        private readonly Vector2 m_floor;

        public PassageInteractable(InteractableTable table, WombatArea area, Vector2 floor) : base(table, area)
        {
            m_floor = floor;
        }

        // 띠 안이면 가로 거리, 띠 아래면 닿지 않는다
        public override float DistanceTo(Vector2 p)
        {
            return p.Y < m_floor.Y + k_Enter ? float.MaxValue : Math.Abs(p.X - m_floor.X);
        }

        public void Pass()
        {
            Area.Bus.Publish(new Events.Passed(Area));
        }
    }
}
