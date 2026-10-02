using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 22: 딴짓 중인 점원을 감싼 사물(id clerk). 기준점 = 점원(또는 외출 중이면 광장 그림) 위치. 곳이 딴짓하는 점원마다 하나 두고, 딴짓이 끝나면 목록에서 뺀다
    public sealed class ClerkInteractable : Interactable
    {
        public const string k_Id = "clerk";

        private readonly Func<Vector2> m_position;

        public Clerk Clerk { get; }

        public ClerkInteractable(InteractableTable table, Clerk clerk, WombatArea area, Func<Vector2> position) : base(table, area)
        {
            Clerk = clerk;
            m_position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_position());
        }
    }
}
