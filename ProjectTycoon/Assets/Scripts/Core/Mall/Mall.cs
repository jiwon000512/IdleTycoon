using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 11: 웜뱃이 있는 곳
    public enum Area
    {
        Shop,
        Plaza,
    }

    // 설계 11 3장: 빵집과 광장을 함께 돌리고, 웜뱃이 있는 곳으로 조이스틱을 보낸다. 빵집 구멍 앞 나가기 → 광장 빵집 문 아래, 문 앞 들어가기 → 빵집 구멍 아래
    public sealed class Mall
    {
        private Vector2 m_input;

        public ShopSim Shop { get; }
        public PlazaSim Plaza { get; }
        public Area Current { get; private set; }
        public IWombatArea Active => Current == Area.Shop ? (IWombatArea)Shop : Plaza;

        public event Action AreaChanged;

        // 웜뱃은 빵집에서 시작한다
        public Mall(ShopSim shop, PlazaSim plaza)
        {
            Shop = shop;
            Plaza = plaza;
            Current = Area.Shop;
            Shop.ExitRequested += Shop_ExitRequested;
            Plaza.DoorEntered += Plaza_DoorEntered;
        }

        // 웜뱃이 어디 있든 두 곳 다 돈다
        public void Tick(double dt)
        {
            Shop.Tick(dt);
            Plaza.Tick(dt);
        }

        // 옮겨 간 곳에도 누르고 있던 방향을 그대로 넘긴다
        public void SetWombatInput(Vector2 input)
        {
            m_input = input;
            Active.SetWombatInput(input);
        }

        private void Shop_ExitRequested()
        {
            Shop.RemoveWombat();
            Plaza.PlaceWombatAtDoor();
            MoveTo(Area.Plaza);
        }

        private void Plaza_DoorEntered()
        {
            Plaza.RemoveWombat();
            Shop.PlaceWombatAtHole();
            MoveTo(Area.Shop);
        }

        private void MoveTo(Area area)
        {
            Current = area;
            Active.SetWombatInput(m_input);
            OnAreaChanged();
        }

        private void OnAreaChanged()
        {
            AreaChanged?.Invoke();
        }
    }
}
