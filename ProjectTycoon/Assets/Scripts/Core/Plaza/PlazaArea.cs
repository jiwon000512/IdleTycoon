using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13: 광장. 지상 계단으로 손님이 오고, 들를 곳 몇 곳을 들렀다 빵집 문으로(또는 구경만 하고 계단으로) 간다.
    // 빵집에서 나간 손님은 문에서 다시 나와 광장을 조금 돌고 떠난다. 사물은 빵집 문 하나(들어가기)
    public sealed class PlazaArea : WombatArea
    {
        private readonly PlazaConfigTable m_config;
        private readonly double m_walkSpeed;
        private readonly double m_hopSeconds;
        private readonly BakeryArea m_bakery;
        private readonly IRandom m_random;
        private readonly List<Visitor> m_visitors = new List<Visitor>();
        private readonly HashSet<int> m_takenSpots = new HashSet<int>();
        private double m_arrivalElapsed;
        private int m_nextVisitorId;

        public PlazaLayout Layout { get; }
        public IReadOnlyList<Visitor> Visitors => m_visitors;

        public event Action<Visitor> VisitorArrived;
        public event Action<Visitor> VisitorRemoved;
        public event Action<Visitor> VisitorEmoted;
        // 빵집 문 앞에서 들어가기 버튼(Mall이 웜뱃을 빵집으로 옮긴다)
        public event Action DoorEntered;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.DoorFloor;

        // 첫 손님은 첫 틱에 온다. 웜뱃은 빵집에서 시작한다
        public PlazaArea(TableSet tables, BakeryArea bakery, IRandom random, Wombat wombat) : base(tables, wombat)
        {
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_walkSpeed = tables.Get<ConfigTable>(ConfigTable.k_WalkSpeed).Value;
            m_hopSeconds = tables.Get<ConfigTable>(ConfigTable.k_HopSeconds).Value;
            m_bakery = bakery;
            m_random = random;
            Layout = new PlazaLayout(tables);
            Things.Add(new DoorInteractable(tables.Get<InteractableTable>(DoorInteractable.k_Id), Layout.DoorFloor, OnDoorEntered));
            m_arrivalElapsed = m_config.ArrivalSeconds;
            m_bakery.CustomerExited += Bakery_CustomerExited;
        }

        // 매 프레임(웜뱃 다음). 순서: 도착 → 손님
        protected override void TickArea(double dt)
        {
            TickArrival(dt);
            TickVisitors(dt);
        }

        private void TickArrival(double dt)
        {
            m_arrivalElapsed = Math.Min(m_arrivalElapsed + dt, m_config.ArrivalSeconds);

            if (m_arrivalElapsed < m_config.ArrivalSeconds || m_visitors.Count >= m_config.MaxVisitors)
            {
                return;
            }

            m_arrivalElapsed = 0d;
            IReadOnlyList<VisitorTable> looks = Tables.GetAll<VisitorTable>();
            VisitorTable look = looks[RandomIndex(looks.Count)];
            Visitor visitor = Spawn(look, Layout.StairsInside, Layout.StairsFloor);
            visitor.VisitsLeft = m_config.VisitsMin + RandomIndex(m_config.VisitsMax - m_config.VisitsMin + 1);
            visitor.WantsShop = m_random.NextDouble() >= m_config.BrowseChance;
        }

        // 빵집에서 나간 손님은 문에서 톡 나와 0 ~ visitsMax곳 들르고 계단으로. 화나서 나갔으면 곧장
        private void Bakery_CustomerExited(Customer customer)
        {
            Visitor visitor = Spawn(customer.Look, Layout.DoorInside, Layout.DoorFloor);
            visitor.VisitsLeft = customer.Angry ? 0 : RandomIndex(m_config.VisitsMax + 1);
        }

        private Visitor Spawn(VisitorTable look, Vector2 inside, Vector2 floor)
        {
            Visitor visitor = new Visitor(++m_nextVisitorId, look, inside);
            StartHop(visitor, CustomerPhase.Entering, inside, floor);
            m_visitors.Add(visitor);
            OnVisitorArrived(visitor);
            return visitor;
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                Visitor visitor = m_visitors[i];
                visitor.Mover.Advance(m_walkSpeed * dt);

                if (!TickVisitor(visitor, dt))
                {
                    ReleaseSpot(visitor);
                    m_visitors.RemoveAt(i);
                    i--;
                    OnVisitorRemoved(visitor);
                }
            }
        }

        // false면 광장을 떠났다(빵집 문·계단으로 들어감)
        private bool TickVisitor(Visitor visitor, double dt)
        {
            if (visitor.Phase == CustomerPhase.Entering || visitor.Phase == CustomerPhase.Exiting)
            {
                visitor.Timer -= dt;
                double t = Math.Min(1d, 1d - visitor.Timer / m_hopSeconds);
                visitor.HopProgress = t;
                visitor.Mover.Place(Vector2.Lerp(visitor.HopFrom, visitor.HopTo, (float)t));

                if (visitor.Timer > 0d)
                {
                    return true;
                }

                if (visitor.Phase == CustomerPhase.Exiting)
                {
                    // ponytail: 문 앞에서 자리를 확인하고 톡 뛰는 사이에 다른 손님이 먼저 들어가면 한 명 넘칠 수 있다
                    if (visitor.Heading == Visitor.Goal.Door)
                    {
                        m_bakery.Admit(visitor.Look);
                    }

                    return false;
                }

                visitor.Phase = CustomerPhase.Walking;
                Next(visitor);
                return true;
            }

            if (visitor.Moving)
            {
                return true;
            }

            // 들를 곳에서 머무는 중
            if (visitor.Timer > 0d)
            {
                visitor.Timer -= dt;

                if (visitor.Timer <= 0d)
                {
                    ReleaseSpot(visitor);
                    visitor.VisitsLeft--;
                    Next(visitor);
                }

                return true;
            }

            Arrive(visitor);
            return true;
        }

        // 다음 갈 곳: 들를 곳이 남았으면 빈 들를 곳, 아니면 빵집(자리가 있을 때) 또는 계단
        private void Next(Visitor visitor)
        {
            if (visitor.VisitsLeft > 0 && TryTakeSpot(visitor))
            {
                return;
            }

            if (visitor.WantsShop && m_bakery.CanAdmit)
            {
                visitor.Heading = Visitor.Goal.Door;
                visitor.Mover.WalkTo(Layout.Nav, Layout.DoorFloor, Facing.Up);
                return;
            }

            // 빵집이 꽉 찼으면 한 곳 더 들르고, 그때도 꽉 찼거나 들를 곳이 없으면 떠난다
            if (visitor.WantsShop && !visitor.Waited && TryTakeSpot(visitor))
            {
                visitor.Waited = true;
                visitor.VisitsLeft = 1;
                return;
            }

            visitor.WantsShop = false;
            visitor.Heading = Visitor.Goal.Stairs;
            visitor.Mover.WalkTo(Layout.Nav, Layout.StairsFloor, Facing.Up);
        }

        private void Arrive(Visitor visitor)
        {
            switch (visitor.Heading)
            {
                case Visitor.Goal.Spot:
                    visitor.Timer = m_config.VisitSecondsMin + m_random.NextDouble() * (m_config.VisitSecondsMax - m_config.VisitSecondsMin);

                    if (m_random.NextDouble() < m_config.EmoteChance)
                    {
                        OnVisitorEmoted(visitor);
                    }

                    break;
                case Visitor.Goal.Door:
                    if (m_bakery.CanAdmit)
                    {
                        StartHop(visitor, CustomerPhase.Exiting, Layout.DoorFloor, Layout.DoorInside);
                    }
                    else
                    {
                        Next(visitor);
                    }

                    break;
                default:
                    StartHop(visitor, CustomerPhase.Exiting, Layout.StairsFloor, Layout.StairsInside);
                    break;
            }
        }

        private bool TryTakeSpot(Visitor visitor)
        {
            int count = Layout.Spots.Count;

            if (m_takenSpots.Count >= count)
            {
                return false;
            }

            int index = RandomIndex(count);

            while (m_takenSpots.Contains(index))
            {
                index = (index + 1) % count;
            }

            m_takenSpots.Add(index);
            visitor.Spot = index;
            visitor.Heading = Visitor.Goal.Spot;
            PlazaSpot spot = Layout.Spots[index];
            visitor.Mover.WalkTo(Layout.Nav, spot.Position, spot.Facing);
            return true;
        }

        private void ReleaseSpot(Visitor visitor)
        {
            if (visitor.Spot >= 0)
            {
                m_takenSpots.Remove(visitor.Spot);
                visitor.Spot = -1;
            }
        }

        private void StartHop(Visitor visitor, CustomerPhase phase, Vector2 from, Vector2 to)
        {
            visitor.Phase = phase;
            visitor.Timer = m_hopSeconds;
            visitor.HopProgress = 0d;
            visitor.HopFrom = from;
            visitor.HopTo = to;
            visitor.Mover.Place(from);
            visitor.Mover.Facing = phase == CustomerPhase.Exiting ? Facing.Up : Facing.Down;
        }

        // [0, count)
        private int RandomIndex(int count)
        {
            return Math.Min(count - 1, (int)(m_random.NextDouble() * count));
        }

        private void OnVisitorArrived(Visitor visitor)
        {
            VisitorArrived?.Invoke(visitor);
        }

        private void OnVisitorRemoved(Visitor visitor)
        {
            VisitorRemoved?.Invoke(visitor);
        }

        private void OnVisitorEmoted(Visitor visitor)
        {
            VisitorEmoted?.Invoke(visitor);
        }

        private void OnDoorEntered()
        {
            DoorEntered?.Invoke();
        }
    }
}
