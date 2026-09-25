using System;

namespace ZooTycoon.Core
{
    // 설계 11 3장 · 설계 13: 빵집과 광장을 함께 돌리고, 웜뱃 하나를 두 곳 사이로 옮긴다. 빵집 구멍 앞 나가기 → 광장 빵집 문 아래, 문 앞 들어가기 → 빵집 구멍 아래
    public sealed class Mall
    {
        public Wombat Wombat { get; }
        public BakeryArea Bakery { get; }
        public PlazaArea Plaza { get; }
        // 웜뱃이 있는 곳
        public WombatArea Active { get; private set; }

        public event Action AreaChanged;

        // 웜뱃은 빵집에서 시작한다
        public Mall(BakeryArea bakery, PlazaArea plaza)
        {
            Bakery = bakery;
            Plaza = plaza;
            Wombat = bakery.Wombat;
            Active = bakery;
            Bakery.ExitRequested += Bakery_ExitRequested;
            Plaza.DoorEntered += Plaza_DoorEntered;
        }

        // 웜뱃이 어디 있든 두 곳 다 돈다
        public void Tick(double dt)
        {
            Bakery.Tick(dt);
            Plaza.Tick(dt);
        }

        private void Bakery_ExitRequested()
        {
            MoveTo(Bakery, Plaza);
        }

        private void Plaza_DoorEntered()
        {
            MoveTo(Plaza, Bakery);
        }

        private void MoveTo(WombatArea from, WombatArea to)
        {
            from.Leave();
            to.Enter();
            Active = to;
            OnAreaChanged();
        }

        private void OnAreaChanged()
        {
            AreaChanged?.Invoke();
        }
    }
}
