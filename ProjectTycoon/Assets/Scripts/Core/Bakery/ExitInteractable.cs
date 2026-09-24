using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11·13: 빵집 입구 구멍 앞. 나가기 버튼 → 빵집이 Mall에 알린다
    public sealed class ExitInteractable : Interactable
    {
        public const string k_Id = "exit";

        private readonly Vector2 m_floor;
        private readonly Action m_exit;

        public ExitInteractable(InteractableTable table, Vector2 floor, Action exit) : base(table)
        {
            m_floor = floor;
            m_exit = exit;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_floor);
        }

        public override void Do(string actionId, Hands hands)
        {
            if (actionId == ActionTable.k_Exit)
            {
                m_exit();
            }
        }
    }
}
