using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13: 계산대. 기준점은 계산대 뒤 웜뱃 자리. 줄은 손님 행동 트리와 얽혀 있어 빵집이 갖고, 계산대는 계산을 빵집에 부탁한다
    public sealed class CounterInteractable : Interactable
    {
        public const string k_Id = "counter";

        private readonly Vector2 m_home;

        public BakeryArea Bakery { get; }
        // 줄 머리가 머리 자리에 서서 계산을 기다린다
        public bool HeadWaiting => Bakery.HeadWaiting;

        public CounterInteractable(InteractableTable table, BakeryArea bakery) : base(table, bakery)
        {
            Bakery = bakery;
            m_home = bakery.Layout.WombatHome;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_home);
        }

        // 줄 머리 계산
        public void Serve()
        {
            Bakery.PayHead();
        }
    }
}
