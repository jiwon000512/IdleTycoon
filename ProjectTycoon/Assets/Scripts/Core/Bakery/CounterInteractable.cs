using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13 · 리뷰 R2: 계산대. 기준점은 계산대 뒤 웜뱃 자리. 줄(빵을 집은 순서, 걸어오는 손님 포함)과 계산을 스스로 갖는다.
    // serve가 auto면 웜뱃이 range 안에 있는 동안 줄 머리 계산 타이머가 흐르고(Tick), manual이면 버튼(Serve)으로만. 속도는 업그레이드
    public sealed class CounterInteractable : Interactable
    {
        public const string k_Id = "counter";

        private readonly Vector2 m_home;
        private readonly ZooState m_till;
        private readonly bool m_serveAuto;
        private readonly List<BakeryVisitor> m_queue = new List<BakeryVisitor>();
        private BakeryVisitor m_timing;
        private double m_remaining;

        public BakeryArea Bakery { get; }
        public IReadOnlyList<BakeryVisitor> Queue => m_queue;
        // 줄 머리가 머리 자리에 서서 계산을 기다린다
        public bool HeadWaiting => m_queue.Count > 0 && m_queue[0].Phase == VisitorPhase.Queued && !m_queue[0].Moving;
        // 줄 머리의 계산 타이머가 돌기 시작했다(웜뱃이 자리를 비우면 멈춘 채 남는다). 화면이 게이지를 보인다
        public bool Serving => HeadWaiting && m_timing == m_queue[0];
        // 계산 진행 0~1(화면 게이지)
        public double Progress => Serving ? 1d - m_remaining / Bakery.Config.CheckoutSeconds : 0d;

        public CounterInteractable(InteractableTable table, BakeryArea bakery, ZooState till) : base(table, bakery)
        {
            Bakery = bakery;
            m_till = till;
            m_home = bakery.Layout.WombatHome;
            m_serveAuto = bakery.Tables.Get<ActionTable>(ActionTable.k_Serve).IsAuto;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_home);
        }

        // 줄 머리가 머리 자리에 선 뒤에만 계산이 흐른다. 웜뱃이 계산대 자리를 비우면 멈춘다
        public override void Tick(double dt)
        {
            if (!HeadWaiting || !m_serveAuto || !Area.IsInRange(this))
            {
                return;
            }

            if (m_timing != m_queue[0])
            {
                m_timing = m_queue[0];
                m_remaining = Bakery.Config.CheckoutSeconds;
            }

            m_remaining -= dt * UpgradeValue(UpgradeLevel);

            if (m_remaining <= 0d)
            {
                Serve();
            }
        }

        // 줄 머리 계산: 값을 받고 줄을 한 칸 당긴다
        public void Serve()
        {
            BakeryVisitor head = m_queue[0];
            m_queue.RemoveAt(0);
            head.Pay();
            m_till.AddCoins(head.Bread.Price);
            Bakery.Bus.Publish(new Events.BakeryVisitorPaid(head, head.Bread.Price));
            Repath();
            OnChanged();
        }

        // 빵을 집은 손님이 줄 끝에 선다(번호를 받고 그 자리로 걷는다)
        internal void Join(BakeryVisitor visitor)
        {
            m_queue.Add(visitor);
            WalkToSlot(m_queue.Count - 1);
            OnChanged();
        }

        // 배치가 바뀌거나 앞사람이 빠지면 줄에 선(서러 가는) 손님은 새 줄 자리로
        internal void Repath()
        {
            for (int i = 0; i < m_queue.Count; i++)
            {
                WalkToSlot(i);
            }
        }

        private void WalkToSlot(int index)
        {
            BakeryLayout layout = Bakery.Layout;
            m_queue[index].Mover.WalkTo(layout.Nav, layout.QueueSlots[index], layout.QueueFacing(index));
        }
    }
}
