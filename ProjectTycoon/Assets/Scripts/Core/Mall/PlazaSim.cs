using System;
using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11 3장: 광장 규칙의 단일 소유자. 지상 계단으로 손님이 오고, 들를 곳 몇 곳을 들렀다 빵집 문으로(또는 구경만 하고 계단으로) 간다.
    // 빵집에서 나간 손님은 문에서 다시 나와 광장을 조금 돌고 떠난다. 웜뱃이 광장에 있으면 조이스틱으로 걷고 빵집 문에서 들어간다
    public sealed class PlazaSim : IWombatArea
    {
        public const string k_ActionEnter = "enter";

        private readonly GameTables m_tables;
        private readonly GameConfig.PlazaConfig m_config;
        private readonly GameConfig.ShopConfig m_shopConfig;
        private readonly ShopSim m_shop;
        private readonly IRandom m_random;
        private readonly List<Visitor> m_visitors = new List<Visitor>();
        private readonly HashSet<int> m_takenSpots = new HashSet<int>();
        private readonly InteractableRecord m_door;
        private readonly Mover m_wombat;
        private Vector2 m_wombatInput;
        private bool m_wombatMoving;
        private bool m_wombatPresent;
        private Interactable? m_target;
        private double m_arrivalElapsed;
        private int m_nextVisitorId;

        public PlazaLayout Layout { get; }
        public IReadOnlyList<Visitor> Visitors => m_visitors;
        public bool WombatPresent => m_wombatPresent;
        public Vector2 WombatPosition => m_wombat.Position;
        public Facing WombatFacing => m_wombat.Facing;
        public bool WombatMoving => m_wombatMoving;
        public Interactable? Target => m_target;

        // 문의 actions에서 첫 manual 행동(들어가기)
        public ActionRecord TargetAction
        {
            get
            {
                if (!m_target.HasValue)
                {
                    return null;
                }

                foreach (string id in m_door.Actions)
                {
                    ActionRecord action = FindAction(id);

                    if (!action.IsAuto)
                    {
                        return action;
                    }
                }

                return null;
            }
        }

        public event Action<Visitor> VisitorArrived;
        public event Action<Visitor> VisitorRemoved;
        public event Action<Visitor> VisitorEmoted;
        public event Action TargetChanged;
        // 빵집 문 앞에서 들어가기 버튼(Mall이 웜뱃을 빵집으로 옮긴다)
        public event Action DoorEntered;

        // 첫 손님은 첫 틱에 온다. 웜뱃은 빵집에서 시작한다
        public PlazaSim(GameTables tables, ShopSim shop, IRandom random)
        {
            m_tables = tables;
            m_config = tables.Config.Plaza;
            m_shopConfig = tables.Config.Shop;
            m_shop = shop;
            m_random = random;
            Layout = new PlazaLayout(tables.Config, tables.Decorations);
            m_door = FindElement(Interactable.k_KindIds[(int)InteractKind.Door]);
            m_wombat = new Mover(Layout.DoorFloor, Facing.Down);
            m_arrivalElapsed = m_config.ArrivalSeconds;
            m_shop.CustomerExited += Shop_CustomerExited;
        }

        // 매 프레임. 순서: 도착 → 손님 → 웜뱃
        public void Tick(double dt)
        {
            TickArrival(dt);
            TickVisitors(dt);
            TickWombat(dt);
        }

        public void SetWombatInput(Vector2 input)
        {
            float length = input.Length();
            m_wombatInput = length > 1f ? input / length : input;
        }

        public bool TryInteract()
        {
            if (TargetAction?.Id != k_ActionEnter)
            {
                return false;
            }

            OnDoorEntered();
            return true;
        }

        // 빵집에서 나가기: 빵집 문 아래 바닥에 선다
        public void PlaceWombatAtDoor()
        {
            m_wombat.Place(Layout.DoorFloor);
            m_wombat.Facing = Facing.Down;
            m_wombatPresent = true;
            RefreshTarget();
        }

        public void RemoveWombat()
        {
            m_wombatPresent = false;
            m_wombatInput = Vector2.Zero;
            m_wombatMoving = false;
            RefreshTarget();
        }

        private void TickArrival(double dt)
        {
            m_arrivalElapsed = Math.Min(m_arrivalElapsed + dt, m_config.ArrivalSeconds);

            if (m_arrivalElapsed < m_config.ArrivalSeconds || m_visitors.Count >= m_config.MaxVisitors)
            {
                return;
            }

            m_arrivalElapsed = 0d;
            VisitorRecord look = m_tables.Visitors[RandomIndex(m_tables.Visitors.Count)];
            Visitor visitor = Spawn(look, Layout.StairsInside, Layout.StairsFloor);
            visitor.VisitsLeft = m_config.VisitsMin + RandomIndex(m_config.VisitsMax - m_config.VisitsMin + 1);
            visitor.WantsShop = m_random.NextDouble() >= m_config.BrowseChance;
        }

        // 빵집에서 나간 손님은 문에서 톡 나와 0 ~ visitsMax곳 들르고 계단으로. 화나서 나갔으면 곧장
        private void Shop_CustomerExited(Customer customer)
        {
            Visitor visitor = Spawn(customer.Look, Layout.DoorInside, Layout.DoorFloor);
            visitor.VisitsLeft = customer.Angry ? 0 : RandomIndex(m_config.VisitsMax + 1);
        }

        private Visitor Spawn(VisitorRecord look, Vector2 inside, Vector2 floor)
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
                visitor.Mover.Advance(m_shopConfig.WalkSpeed * dt);

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
                double t = Math.Min(1d, 1d - visitor.Timer / m_shopConfig.HopSeconds);
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
                        m_shop.Admit(visitor.Look);
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

            if (visitor.WantsShop && m_shop.CanAdmit)
            {
                visitor.Heading = Visitor.Goal.Door;
                Walk(visitor, Layout.DoorFloor, Facing.Up);
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
            Walk(visitor, Layout.StairsFloor, Facing.Up);
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
                    if (m_shop.CanAdmit)
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
            Walk(visitor, spot.Position, spot.Facing);
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
            visitor.Timer = m_shopConfig.HopSeconds;
            visitor.HopProgress = 0d;
            visitor.HopFrom = from;
            visitor.HopTo = to;
            visitor.Mover.Place(from);
            visitor.Mover.Facing = phase == CustomerPhase.Exiting ? Facing.Up : Facing.Down;
        }

        // 걷는 중이면 지금 선분을 마저 걷고 그 끝점에서 새 길을 찾는다(ShopSim.Walk와 같은 방식)
        private void Walk(Visitor visitor, Vector2 target, Facing arrive)
        {
            Mover mover = visitor.Mover;
            Vector2 from = mover.NextNode;
            List<Vector2> path = Layout.Nav.FindPath(from, target);

            if (path.Count == 0 && Vector2.DistanceSquared(from, target) > 1e-6f)
            {
                path.Add(target);
            }

            if (mover.Moving)
            {
                path.Insert(0, from);
            }

            mover.Follow(path, arrive);
        }

        private void TickWombat(double dt)
        {
            if (!m_wombatPresent)
            {
                return;
            }

            m_wombatMoving = WombatWalker.Step(m_wombat, m_wombatInput, m_shopConfig.WombatSpeed, dt, Layout.Nav);
            RefreshTarget();
        }

        // 대상은 빵집 문 하나: 문 아래 바닥에서 range 안
        private void RefreshTarget()
        {
            Interactable? found = null;

            if (m_wombatPresent && Vector2.Distance(m_wombat.Position, Layout.DoorFloor) <= m_door.Range)
            {
                found = new Interactable(InteractKind.Door, default);
            }

            if (!Nullable.Equals(found, m_target))
            {
                m_target = found;
                OnTargetChanged();
            }
        }

        // [0, count)
        private int RandomIndex(int count)
        {
            return Math.Min(count - 1, (int)(m_random.NextDouble() * count));
        }

        private ActionRecord FindAction(string id)
        {
            foreach (ActionRecord action in m_tables.Actions)
            {
                if (action.Id == id)
                {
                    return action;
                }
            }

            throw new KeyNotFoundException($"행동 ID '{id}'가 actions.json에 없다.");
        }

        private InteractableRecord FindElement(string id)
        {
            foreach (InteractableRecord element in m_tables.Interactables)
            {
                if (element.Id == id)
                {
                    return element;
                }
            }

            throw new KeyNotFoundException($"사물 ID '{id}'가 interactables.json에 없다.");
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

        private void OnTargetChanged()
        {
            TargetChanged?.Invoke();
        }

        private void OnDoorEntered()
        {
            DoorEntered?.Invoke();
        }
    }
}
