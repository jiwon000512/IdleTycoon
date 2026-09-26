using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13 · 리뷰 R2 · 설계 18: 광장. 지상 계단으로 손님이 오고(도착 타이머), 빵집에서 나간 손님은 문에서 다시 나온다.
    // 손님 한 명의 할 일은 PlazaVisitor, 여기는 손님 목록과 들를 곳 자리표. 사물은 빵집 문 하나(들어가기). 장식은 자유 배치(WombatArea.Placement)
    public sealed class PlazaArea : WombatArea
    {
        private readonly PlazaConfigTable m_config;
        private readonly IRandom m_random;
        private readonly List<PlazaVisitor> m_visitors = new List<PlazaVisitor>();
        private readonly List<DecorationData> m_decor = new List<DecorationData>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly HashSet<int> m_takenSpots = new HashSet<int>();
        private double m_arrivalElapsed;
        private int m_nextVisitorId;

        public PlazaLayout Layout { get; }
        public BakeryArea Bakery { get; }
        public IReadOnlyList<PlazaVisitor> Visitors => m_visitors;
        public IReadOnlyList<DecorationData> Decor => m_decor;
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.DoorFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;
        protected override IEnumerable<IPlaced> PlacedThings => m_decor;

        // 첫 손님은 첫 틱에 온다. 웜뱃은 빵집에서 시작한다. 시작 장식은 PlazaDecorTable
        public PlazaArea(TableSet tables, BakeryArea bakery, IRandom random, Wombat wombat, EventBus bus) : base(tables, wombat, bus)
        {
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_random = random;
            Bakery = bakery;
            Layout = new PlazaLayout(tables);
            Placed.Add(new PassageInteractable(tables.Get<InteractableTable>(PassageInteractable.k_Door), this, Layout.DoorFloor));
            m_arrivalElapsed = m_config.ArrivalSeconds;
            bus.Subscribe<Events.BakeryVisitorLeft>(Bus_BakeryVisitorLeft);

            foreach (DecorationTable row in tables.GetAll<DecorationTable>())
            {
                m_shopKinds.Add(row);
            }

            foreach (PlazaDecorTable placed in tables.GetAll<PlazaDecorTable>())
            {
                m_decor.Add(new DecorationData(tables.Get<DecorationTable>(placed.Decoration), new Vector2((float)placed.X, (float)placed.Y)));
            }

            Layout.Rebuild(m_decor);
        }

        // 들를 곳에서 머무는 초 · ♥를 띄울지
        internal double VisitSeconds()
        {
            return m_config.VisitSecondsMin + m_random.NextDouble() * (m_config.VisitSecondsMax - m_config.VisitSecondsMin);
        }

        internal bool RollEmote()
        {
            return m_random.NextDouble() < m_config.EmoteChance;
        }

        // 빈 들를 곳 하나를 잡는다(난수 자리부터 돌며). 다 찼으면 false
        internal bool TryTakeSpot(out int index)
        {
            int count = Layout.Spots.Count;

            if (count == 0 || m_takenSpots.Count >= count)
            {
                index = -1;
                return false;
            }

            index = RandomIndex(count);

            while (m_takenSpots.Contains(index))
            {
                index = (index + 1) % count;
            }

            m_takenSpots.Add(index);
            return true;
        }

        internal void ReleaseSpot(int index)
        {
            m_takenSpots.Remove(index);
        }

        // 매 프레임(웜뱃 다음). 순서: 도착 → 손님
        protected override void TickArea(double dt)
        {
            TickArrival(dt);
            TickVisitors(dt);
        }

        protected override IPlacedKind KindOf(string kindId)
        {
            return Tables.Get<DecorationTable>(kindId);
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            DecorationData placed = new DecorationData((DecorationTable)kind, at);
            m_decor.Add(placed);
            return placed;
        }

        protected override void Destroy(IPlaced thing)
        {
            m_decor.Remove((DecorationData)thing);
        }

        // 들를 곳 번호가 바뀌므로 손님은 들르던 곳을 놓고 다음으로, 걷던 손님은 새 땅에서 길을 다시 찾는다
        protected override void OnPlacementChanged()
        {
            Layout.Rebuild(m_decor);
            m_takenSpots.Clear();

            foreach (PlazaVisitor visitor in m_visitors)
            {
                visitor.Relayout();
            }

            Bus.Publish(new Events.LayoutChanged(this));
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
            int visits = m_config.VisitsMin + RandomIndex(m_config.VisitsMax - m_config.VisitsMin + 1);
            bool wantsShop = m_random.NextDouble() >= m_config.BrowseChance;
            Spawn(new PlazaVisitor(++m_nextVisitorId, look, this, Layout.StairsInside, Layout.StairsFloor, visits, wantsShop));
        }

        // 빵집에서 나간 손님은 문에서 톡 나와 0 ~ visitsMax곳 들르고 계단으로. 화나서 나갔으면 곧장
        private void Bus_BakeryVisitorLeft(Events.BakeryVisitorLeft e)
        {
            if (e.Visitor.Bakery != Bakery)
            {
                return;
            }

            BakeryVisitor customer = e.Visitor;
            int visits = customer.Angry ? 0 : RandomIndex(m_config.VisitsMax + 1);
            Spawn(new PlazaVisitor(++m_nextVisitorId, customer.Look, this, Layout.DoorInside, Layout.DoorFloor, visits, false));
        }

        private void Spawn(PlazaVisitor visitor)
        {
            m_visitors.Add(visitor);
            Bus.Publish(new Events.PlazaVisitorArrived(visitor));
        }

        private void TickVisitors(double dt)
        {
            for (int i = 0; i < m_visitors.Count; i++)
            {
                PlazaVisitor visitor = m_visitors[i];

                if (visitor.Tick(dt))
                {
                    continue;
                }

                m_visitors.RemoveAt(i);
                i--;
                Bus.Publish(new Events.PlazaVisitorLeft(visitor));
            }
        }

        // [0, count)
        private int RandomIndex(int count)
        {
            return Math.Min(count - 1, (int)(m_random.NextDouble() * count));
        }
    }
}
