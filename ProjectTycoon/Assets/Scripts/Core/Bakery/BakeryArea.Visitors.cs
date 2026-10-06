using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 6·7장 · 리뷰 R2: 빵집 손님 목록과 손님들 사이의 일(비켜 걷기·서는 자리). 손님 한 명의 할 일은 BakeryVisitor
    public sealed partial class BakeryArea
    {
        private readonly List<BakeryVisitor> m_visitors = new List<BakeryVisitor>();
        private int m_nextVisitorId;

        public IReadOnlyList<BakeryVisitor> Visitors => m_visitors;
        // 설계 11: 손님은 광장 빵집 문으로 들어온다(도착 타이머는 PlazaArea)
        public bool CanAdmit => m_visitors.Count < m_config.MaxCustomers;

        // 광장 빵집 문에서 톡 들어온 손님이 구멍에서 나온다(자리 확인은 CanAdmit으로 부르는 쪽이)
        public void Admit(VisitorTable look)
        {
            BakeryVisitor visitor = new BakeryVisitor(++m_nextVisitorId, look, this);
            m_visitors.Add(visitor);
            Bus.Publish(new Events.BakeryVisitorArrived(visitor));
        }

        // 진열대 옆·앞 자리 중 아무도 잡지 않은 첫 자리. 다 찼으면 근처 빈 걷는 점
        internal Vector2 FreeSpot(ShelfInteractable shelf)
        {
            foreach (Vector2 spot in Layout.ShelfSpots(shelf))
            {
                if (!SpotTaken(spot))
                {
                    return spot;
                }
            }

            return Layout.OverflowSpot(shelf, SpotTaken);
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                BakeryVisitor visitor = m_visitors[i];

                if (visitor.Tick(dt))
                {
                    continue;
                }

                m_visitors.RemoveAt(i);
                i--;
                Bus.Publish(new Events.BakeryVisitorLeft(visitor));
            }

            Crowd.Sidestep(m_visitors, dt);
        }

        // 배치가 바뀌면: 줄에 선 손님은 계산대가 새 줄 자리로, 걷는 중인 손님은 같은 목적지로 새 길을 찾는다
        private void RepathVisitors()
        {
            foreach (CounterInteractable counter in m_counters)
            {
                counter.Repath();
            }

            foreach (BakeryVisitor visitor in m_visitors)
            {
                visitor.Repath();
            }
        }

        // 설계 24: 똥에 막혀 포기한 손님은 줄에서 빠진다
        internal void LeaveQueue(BakeryVisitor visitor)
        {
            foreach (CounterInteractable counter in m_counters)
            {
                counter.Leave(visitor);
            }
        }

        // 웜뱃이 서 있는 자리, 똥 둘레 안 자리도 찬 자리다(손님이 웜뱃·똥 위에 서지 않게)
        private bool SpotTaken(Vector2 p)
        {
            if ((WombatPresent && Vector2.Distance(Wombat.Mover.Position, p) < Visitor.k_YieldRadius) || NearPoop(p))
            {
                return true;
            }

            foreach (BakeryVisitor other in m_visitors)
            {
                if (other.HasSpot && Vector2.DistanceSquared(other.Spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
