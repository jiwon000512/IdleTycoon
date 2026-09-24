using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13: 계산대. 기준점은 계산대 뒤 웜뱃 자리. 줄은 손님 행동 트리와 얽혀 있어 빵집이 갖고, 계산대는 serve를 빵집에 부탁한다
    public sealed class CounterInteractable : Interactable
    {
        public const string k_Id = "counter";

        private readonly BakeryArea m_bakery;
        private readonly Vector2 m_home;

        public CounterInteractable(InteractableTable table, BakeryArea bakery) : base(table)
        {
            m_bakery = bakery;
            m_home = bakery.Layout.WombatHome;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_home);
        }

        // serve가 auto면 버튼도 자동 행동도 아니고, range 안에 있는 동안 흐르는 계산 타이머다(BakeryArea.TickCheckout)
        public override bool CanDo(string actionId, Hands hands)
        {
            return actionId != ActionTable.k_Serve || (m_bakery.HeadWaiting && !m_bakery.ServeAuto);
        }

        // manual serve: 버튼 한 번에 서 있는 줄 머리 계산
        public override void Do(string actionId, Hands hands)
        {
            if (actionId == ActionTable.k_Serve)
            {
                m_bakery.PayHead();
            }
        }
    }
}
