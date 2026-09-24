using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13: 광장의 빵집 문. 들어가기 버튼 → 광장이 Mall에 알린다
    public sealed class DoorInteractable : Interactable
    {
        public const string k_Id = "door";

        private readonly Vector2 m_floor;
        private readonly Action m_enter;

        public DoorInteractable(InteractableTable table, Vector2 floor, Action enter) : base(table)
        {
            m_floor = floor;
            m_enter = enter;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_floor);
        }

        public override void Do(string actionId, Hands hands)
        {
            if (actionId == ActionTable.k_Enter)
            {
                m_enter();
            }
        }
    }
}
