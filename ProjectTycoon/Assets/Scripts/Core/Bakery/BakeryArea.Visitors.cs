using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 손님 동선 설계 v0.2 6·7장 · 리뷰 R2: 빵집 손님 목록과 손님들 사이의 일(비켜 걷기·서는 자리). 손님 한 명의 할 일은 BakeryVisitor
    public sealed partial class BakeryArea
    {
        // 비켜 걷기: 이 거리 안의 손님을 피한다(손님 그림 폭). 비키는 최대 거리는 걷는 땅 여유(BurrowNav.k_Clearance 0.3) 안
        private const float k_PersonalSpace = 0.6f;
        private const float k_MaxSidestep = 0.25f;
        private const float k_SidestepRate = 8f;
        // 앞사람이 이만큼도 옆에 있지 않으면 한 줄로 마주 온 것: 오른쪽으로 비킨다
        private const float k_InLine = 0.05f;

        private readonly List<BakeryVisitor> m_visitors = new List<BakeryVisitor>();
        private int m_nextVisitorId;

        public IReadOnlyList<BakeryVisitor> Visitors => m_visitors;
        // 설계 11: 손님은 광장 빵집 문으로 들어온다(도착 타이머는 PlazaArea)
        public bool CanAdmit => m_visitors.Count < m_config.MaxCustomers;

        // 손님 한 명의 사건을 여러 손님을 보는 쪽(스포너·소리)이 한 곳에서 듣는다
        public event Action<BakeryVisitor> VisitorArrived;
        public event Action<BakeryVisitor> VisitorPicked;
        public event Action<BakeryVisitor, double> VisitorPaid;
        public event Action<BakeryVisitor> VisitorGaveUp;
        public event Action<BakeryVisitor> VisitorExited;

        // 광장 빵집 문에서 톡 들어온 손님이 구멍에서 나온다(자리 확인은 CanAdmit으로 부르는 쪽이)
        public void Admit(VisitorTable look)
        {
            BakeryVisitor visitor = new BakeryVisitor(++m_nextVisitorId, look, this);
            visitor.Picked += Visitor_Picked;
            visitor.GaveUp += Visitor_GaveUp;
            m_visitors.Add(visitor);
            OnVisitorArrived(visitor);
        }

        // 진열대 옆·앞 자리 중 아무도 잡지 않은 첫 자리. 다 찼으면 근처 빈 걷는 점
        internal Vector2 FreeSpot(Cell cell)
        {
            foreach (Vector2 spot in Layout.ShelfSpots(cell))
            {
                if (!SpotTaken(spot))
                {
                    return spot;
                }
            }

            return Layout.OverflowSpot(cell, SpotTaken);
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

                visitor.Picked -= Visitor_Picked;
                visitor.GaveUp -= Visitor_GaveUp;
                m_visitors.RemoveAt(i);
                i--;
                OnVisitorExited(visitor);
            }

            float blend = (float)Math.Min(1d, dt * k_SidestepRate);

            foreach (BakeryVisitor visitor in m_visitors)
            {
                visitor.Sidestep += (SidestepTarget(visitor) - visitor.Sidestep) * blend;
            }
        }

        // 걷는 손님은 앞에 있는 손님 반대쪽 옆으로(한 줄로 마주 오면 오른쪽으로), 선 손님은 가까운 손님 반대쪽으로 비킨다
        private Vector2 SidestepTarget(BakeryVisitor visitor)
        {
            if (visitor.Hopping)
            {
                return Vector2.Zero;
            }

            Vector2 self = visitor.Mover.Position;
            Vector2 ahead = visitor.Mover.NextNode - self;
            bool walking = ahead.LengthSquared() > 1e-6f;
            Vector2 left = walking ? Vector2.Normalize(new Vector2(-ahead.Y, ahead.X)) : Vector2.Zero;
            Vector2 push = Vector2.Zero;

            foreach (BakeryVisitor other in m_visitors)
            {
                Vector2 toOther = other.Mover.Position - self;
                float distance = toOther.Length();

                if (other == visitor || other.Hopping || distance >= k_PersonalSpace)
                {
                    continue;
                }

                float strength = 1f - distance / k_PersonalSpace;

                if (walking)
                {
                    if (Vector2.Dot(toOther, ahead) > 0f)
                    {
                        push += left * (Vector2.Dot(toOther, left) < -k_InLine ? strength : -strength);
                    }
                }
                else if (distance > 1e-4f)
                {
                    push -= toOther / distance * strength;
                }
            }

            float length = push.Length();
            return (length > 1f ? push / length : push) * k_MaxSidestep;
        }

        // 배치가 바뀌면: 줄에 선 손님은 계산대가 새 줄 자리로, 걷는 중인 손님은 같은 목적지로 새 길을 찾는다
        private void RepathVisitors()
        {
            Counter.Repath();

            foreach (BakeryVisitor visitor in m_visitors)
            {
                visitor.Repath();
            }
        }

        private bool SpotTaken(Vector2 p)
        {
            foreach (BakeryVisitor other in m_visitors)
            {
                if (other.HasSpot && Vector2.DistanceSquared(other.Spot, p) < 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private void Visitor_Picked(BakeryVisitor visitor)
        {
            VisitorPicked?.Invoke(visitor);
        }

        private void Visitor_GaveUp(BakeryVisitor visitor)
        {
            VisitorGaveUp?.Invoke(visitor);
        }

        private void Counter_Served(BakeryVisitor visitor, double coins)
        {
            VisitorPaid?.Invoke(visitor, coins);
        }

        private void OnVisitorArrived(BakeryVisitor visitor)
        {
            VisitorArrived?.Invoke(visitor);
        }

        private void OnVisitorExited(BakeryVisitor visitor)
        {
            VisitorExited?.Invoke(visitor);
        }
    }
}
