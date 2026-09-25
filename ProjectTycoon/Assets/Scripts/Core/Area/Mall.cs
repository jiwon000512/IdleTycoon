using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13 · 설계 16: 빵집과 광장을 함께 돌리고, 웜뱃 하나를 두 곳 사이로 옮긴다.
    // 통로 사물이 낸 Passed를 받아 지금 곳이면 다른 곳으로: 빵집 구멍 앞 나가기 → 광장 빵집 문 아래, 문 앞 들어가기 → 빵집 구멍 아래
    public sealed class Mall
    {
        private readonly EventBus m_bus;

        public Wombat Wombat { get; }
        public BakeryArea Bakery { get; }
        public PlazaArea Plaza { get; }
        // 웜뱃이 있는 곳
        public WombatArea Active { get; private set; }

        // 웜뱃은 빵집에서 시작한다
        public Mall(BakeryArea bakery, PlazaArea plaza, EventBus bus)
        {
            Bakery = bakery;
            Plaza = plaza;
            m_bus = bus;
            Wombat = bakery.Wombat;
            Active = bakery;
            bus.Subscribe<Events.Passed>(Bus_Passed);
        }

        // 웜뱃이 어디 있든 두 곳 다 돈다
        public void Tick(double dt)
        {
            Bakery.Tick(dt);
            Plaza.Tick(dt);
        }

        private void Bus_Passed(Events.Passed e)
        {
            if (e.From != Active)
            {
                return;
            }

            WombatArea to = Active == Bakery ? (WombatArea)Plaza : Bakery;
            Active.Leave();
            to.Enter();
            Active = to;
            m_bus.Publish(new Events.AreaChanged(Active));
        }
    }
}
