using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 닫힌 가게 문 앞(광장 문 아래 바닥). 가게가 닫힌 동안만 광장 사물 목록에 들고, 돋보기 버튼(open) → 시트 줄(open_shop)에서 값을 치러 연다.
    // 문 통로는 그동안 막혀 있다(PassageInteractable.Gate)
    public sealed class ShopGateInteractable : Interactable
    {
        public const string k_Id = "shop_gate";

        private readonly Vector2 m_floor;

        public RestaurantArea Shop { get; }
        public Vector2 Floor => m_floor;

        public ShopGateInteractable(InteractableTable table, WombatArea area, Vector2 floor, RestaurantArea shop) : base(table, area)
        {
            m_floor = floor;
            Shop = shop;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_floor);
        }
    }
}
