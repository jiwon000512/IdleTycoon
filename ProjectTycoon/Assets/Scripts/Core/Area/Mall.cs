using System.Collections.Generic;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13 · 설계 16: 곳들(빵집 · 광장 · 농장)을 함께 돌리고, 웜뱃 하나를 곳 사이로 옮긴다.
    // 통로 사물이 낸 Passed를 받아 지금 곳이면 가는 곳(To)으로: 들어가는 곳은 온 곳을 알아 그 문 앞에 세운다(설계 25)
    public sealed class Mall
    {
        private readonly EventBus m_bus;
        private readonly List<WombatArea> m_areas = new List<WombatArea>();

        public Wombat Wombat { get; }
        public BakeryArea Bakery { get; }
        public PlazaArea Plaza { get; }
        public FarmArea Farm { get; }
        public IReadOnlyList<WombatArea> Areas => m_areas;
        // 웜뱃이 있는 곳
        public WombatArea Active { get; private set; }

        // 웜뱃은 빵집에서 시작한다
        public Mall(BakeryArea bakery, PlazaArea plaza, FarmArea farm, EventBus bus)
        {
            Bakery = bakery;
            Plaza = plaza;
            Farm = farm;
            m_areas.Add(bakery);
            m_areas.Add(plaza);
            m_areas.Add(farm);
            m_bus = bus;
            Wombat = bakery.Wombat;
            Active = bakery;
            bus.Subscribe<Events.Passed>(Bus_Passed);
        }

        // 웜뱃이 어디 있든 곳은 다 돈다
        public void Tick(double dt)
        {
            foreach (WombatArea area in m_areas)
            {
                area.Tick(dt);
            }
        }

        private void Bus_Passed(Events.Passed e)
        {
            if (e.From != Active)
            {
                return;
            }

            WombatArea to = m_areas.Find(area => area.Id == e.To);
            Active.Leave();
            to.Enter(e.From);
            Active = to;
            m_bus.Publish(new Events.AreaChanged(Active));
        }
    }
}
