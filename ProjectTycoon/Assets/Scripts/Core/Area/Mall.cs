using System.Collections.Generic;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13 · 설계 16: 곳들(빵집 · 광장 · 농장)을 함께 돌리고, 웜뱃 하나를 곳 사이로 옮긴다.
    // 통로 사물이 낸 Passed를 받아 지금 곳이면 가는 곳(To)으로: 들어가는 곳은 온 곳을 알아 그 문 앞에 세운다(설계 25).
    // 설계 38: 점원 월급날은 모든 곳 공통(Payroll)이라 여기서 곳보다 먼저 돌린다.
    // 설계 39: 농장은 층마다 곳 하나(1층이 아래층을 잇는다). 닫힌 층도 처음부터 곳 목록에 든다. 설계 44: 낚시터(광장 왼쪽 끝 문)
    public sealed class Mall
    {
        private readonly EventBus m_bus;
        private readonly List<WombatArea> m_areas = new List<WombatArea>();
        private readonly List<FarmArea> m_farms = new List<FarmArea>();

        public Wombat Wombat { get; }
        public BakeryArea Bakery { get; }
        public PlazaArea Plaza { get; }
        // 농장 1층. 층 전부는 Farms(위 → 아래)
        public FarmArea Farm { get; }
        public IReadOnlyList<FarmArea> Farms => m_farms;
        public FishingArea Fishing { get; }
        public IReadOnlyList<WombatArea> Areas => m_areas;
        public Payroll Payroll { get; }
        // 웜뱃이 있는 곳
        public WombatArea Active { get; private set; }

        // 웜뱃은 빵집에서 시작한다. fishing 없이 부르면(빵집 · 농장만 보는 테스트) 씨앗 고정 난수의 낚시터를 만든다(다른 곳의 난수 순서를 건드리지 않게)
        public Mall(BakeryArea bakery, PlazaArea plaza, FarmArea farm, EventBus bus, FishingArea fishing = null)
        {
            Bakery = bakery;
            Plaza = plaza;
            Farm = farm;
            m_areas.Add(bakery);
            m_areas.Add(plaza);

            for (FarmArea floor = farm; floor != null; floor = floor.Lower)
            {
                m_farms.Add(floor);
                m_areas.Add(floor);
            }

            Fishing = fishing ?? new FishingArea(bakery.Tables, new SeededRandom(0), bakery.Wombat, bus);
            m_areas.Add(Fishing);

            m_bus = bus;
            Wombat = bakery.Wombat;
            Active = bakery;
            Payroll = new Payroll(m_areas, bakery.ClerkConfig, Wombat.Worker.Wallet, bus);
            bus.Subscribe<Events.Passed>(Bus_Passed);
        }

        // 웜뱃이 어디 있든 곳은 다 돈다
        public void Tick(double dt)
        {
            Payroll.Tick(dt);

            foreach (WombatArea area in m_areas)
            {
                area.Tick(dt);
            }

            Wombat.Worker.Wallet.Blessing.Tick(dt);
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
