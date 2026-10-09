using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13 · 리뷰 R2: 다른 곳으로 옮겨 가는 사물(굴 구멍 앞 나가기 exit · 광장 문 door). 기준점은 그 아래 바닥, 지나가면 Passed(가는 곳 id)를 내고 Mall이 옮긴다.
    // 2026-09-26 자동 이동(사용자: 「동굴과의 거리로만」): 버튼 없이, 통로 앞 띠(가로 ±range, 바닥선에서 k_Enter 이상 올라간 곳)에 들어오면 넘어간다(exit·enter는 auto).
    // 들어온 웜뱃은 바닥선(띠 밖)에 서고 조이스틱을 놓을 때까지 걷지 않으므로(Wombat.WaitRelease) 그 자리에서 되돌아가지 않는다.
    // 설계 25: 광장에 문이 여럿이라 통로마다 가는 곳(To)을 갖는다.
    // 설계 39: 농장 아래층 계단은 반대 방향(기준점에서 k_Enter 이상 내려간 띠)
    public sealed class PassageInteractable : Interactable
    {
        public const string k_Exit = "exit";
        public const string k_Door = "door";

        // 바닥선에서 이만큼 위(벽 쪽)로 올라가야 통로 앞 띠. 바닥선~벽은 약 0.48
        private const float k_Enter = 0.25f;

        private readonly Vector2 m_floor;
        private readonly bool m_down;

        // 가는 곳 id(곳의 k_Id)
        public string To { get; }
        // 설계 47: 닫힌 가게 문(광장이 건다). null이면 늘 열림
        internal Func<bool> Gate { get; set; }
        public bool IsOpen => Gate == null || Gate();

        public PassageInteractable(InteractableTable table, WombatArea area, Vector2 floor, string to, bool down = false) : base(table, area)
        {
            m_floor = floor;
            m_down = down;
            To = to;
        }

        // 띠 안이면 가로 거리, 띠 밖이면 닿지 않는다
        public override float DistanceTo(Vector2 p)
        {
            bool inBand = m_down ? p.Y <= m_floor.Y - k_Enter : p.Y >= m_floor.Y + k_Enter;
            return inBand ? Math.Abs(p.X - m_floor.X) : float.MaxValue;
        }

        // 설계 54: 길잡이가 지나갈 때 서는 점 = 띠 안쪽(닿으면 넘어간다)
        public override Vector2? GuidePoint(Vector2 from)
        {
            return m_floor + new Vector2(0f, m_down ? -k_Enter - 0.1f : k_Enter + 0.1f);
        }

        public void Pass()
        {
            Area.Bus.Publish(new Events.Passed(Area, To));
        }
    }
}
