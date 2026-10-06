using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 횟집 손님 목록과 손님들 사이의 일(비켜 걷기 · 수조 앞 서는 자리 · 탁자 자리). 손님 한 명의 할 일은 RestaurantVisitor
    public sealed partial class RestaurantArea
    {
        // 수조 앞 자리가 다 찼을 때 첫 자리 둘레에서 빈 걷는 점을 찾는 거리
        private const float k_OverflowDistance = 2f;

        private readonly List<RestaurantVisitor> m_visitors = new List<RestaurantVisitor>();
        private int m_nextVisitorId;

        public IReadOnlyList<RestaurantVisitor> Visitors => m_visitors;
        public bool CanAdmit => IsOpen && m_visitors.Count < Config.MaxCustomers;
        public bool HasFreeSeat => FindSeat(out _, out _);

        public void Admit(VisitorTable look)
        {
            RestaurantVisitor visitor = new RestaurantVisitor(++m_nextVisitorId, look, this);
            m_visitors.Add(visitor);
            Bus.Publish(new Events.RestaurantVisitorArrived(visitor));
        }

        // 수조 앞 자리 중 아무도 잡지 않은 첫 자리. 다 찼으면 근처 빈 걷는 점
        internal Vector2 FreeSpot(TankInteractable tank)
        {
            IReadOnlyList<Vector2> spots = Layout.TankSpots(tank);

            foreach (Vector2 spot in spots)
            {
                if (!SpotTaken(spot))
                {
                    return spot;
                }
            }

            Vector2 around = spots.Count > 0 ? spots[0] : tank.Position;
            return Layout.Nav.TryNearestFree(around, SpotTaken, k_OverflowDistance, out Vector2 free) ? free : around;
        }

        // 아무도 잡지 않은 탁자 자리(탁자 순서 · 자리 순서)
        internal bool FindSeat(out DiningTableInteractable table, out int seat)
        {
            foreach (DiningTableInteractable each in m_tables)
            {
                foreach (RestaurantLayout.Seat s in Layout.Seats(each))
                {
                    if (!SeatTaken(each, s.Index))
                    {
                        table = each;
                        seat = s.Index;
                        return true;
                    }
                }
            }

            table = null;
            seat = -1;
            return false;
        }

        // 그 손님이 잡은 자리(배치가 바뀌어 걷는 땅에서 빠졌으면 탁자 첫 자리)
        internal RestaurantLayout.Seat SeatOf(RestaurantVisitor visitor)
        {
            IReadOnlyList<RestaurantLayout.Seat> seats = Layout.Seats(visitor.Table);

            foreach (RestaurantLayout.Seat seat in seats)
            {
                if (seat.Index == visitor.Seat)
                {
                    return seat;
                }
            }

            return seats.Count > 0 ? seats[0] : new RestaurantLayout.Seat(visitor.Seat, visitor.Table.Position, Facing.Up);
        }

        private bool Seated(DiningTableInteractable table)
        {
            foreach (RestaurantVisitor visitor in m_visitors)
            {
                if (visitor.Table == table)
                {
                    return true;
                }
            }

            return false;
        }

        private bool SeatTaken(DiningTableInteractable table, int seat)
        {
            foreach (RestaurantVisitor visitor in m_visitors)
            {
                if (visitor.Table == table && visitor.Seat == seat)
                {
                    return true;
                }
            }

            return false;
        }

        // 웜뱃이 서 있는 자리, 똥 둘레 안 자리도 찬 자리다
        private bool SpotTaken(Vector2 p)
        {
            if (WombatPresent && Vector2.Distance(Wombat.Mover.Position, p) < Visitor.k_YieldRadius || NearPoop(p))
            {
                return true;
            }

            foreach (RestaurantVisitor other in m_visitors)
            {
                if (other.HasSpot && Vector2.DistanceSquared(other.Spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                RestaurantVisitor visitor = m_visitors[i];

                if (visitor.Tick(dt))
                {
                    continue;
                }

                m_visitors.RemoveAt(i);
                i--;
                Bus.Publish(new Events.RestaurantVisitorLeft(visitor));
            }

            Crowd.Sidestep(m_visitors, dt);
        }

        // 배치가 바뀌면: 자리로 가거나 앉은 손님은 새 자리로, 걷는 중인 손님은 같은 목적지로 새 길을 찾는다
        private void RepathVisitors()
        {
            foreach (RestaurantVisitor visitor in m_visitors)
            {
                visitor.Relayout();
            }
        }
    }
}
