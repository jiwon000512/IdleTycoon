using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13 → 설계 17: 진열대 하나 = 빵 한 종류와 그 재고. 종류는 웜뱃이 처음 올린 빵이 정하고 다 팔리면 풀린다(Bread = null, 빈 진열대).
    // 웜뱃이 채우고 손님이 하나씩 집는다. 용량은 업그레이드
    public sealed class ShelfInteractable : Interactable
    {
        public const string k_Id = "shelf";

        private readonly Vector2 m_base;

        public Cell Cell { get; }
        public BakeryArea Bakery { get; }
        // 지금 진열한 빵. 빈 진열대면 null
        public BreadTable Bread { get; private set; }
        public int Stock { get; private set; }
        public int Capacity => (int)UpgradeValue(UpgradeLevel);

        public ShelfInteractable(InteractableTable table, Cell cell, BakeryArea bakery) : base(table, bakery)
        {
            Cell = cell;
            Bakery = bakery;
            m_base = bakery.Layout.ShelfBase(cell);
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_base);
        }

        // 용량 = 설정 + 업그레이드
        public override double UpgradeValue(int level)
        {
            return Bakery.Config.ShelfCapacity + Table.Upgrade.EffectPerLevel * level;
        }

        // 그 빵을 진열 중이고 자리가 남았다
        public bool HasRoomFor(BreadTable bread)
        {
            return Bread == bread && Stock < Capacity;
        }

        // 자리만큼 올려놓고 올린 수를 돌려준다. 빈 진열대는 그 빵의 진열대가 되고, 다른 빵이 있으면 0
        public int Put(BreadTable bread, int count)
        {
            if (Bread != null && Bread != bread)
            {
                return 0;
            }

            Bread = bread;
            int placed = Math.Min(count, Capacity - Stock);
            Stock += placed;
            OnChanged();
            return placed;
        }

        // 손님이 하나 집는다. 비었으면 false. 마지막 하나를 집으면 빈 진열대
        public bool TryPick()
        {
            if (Stock == 0)
            {
                return false;
            }

            Stock--;

            if (Stock == 0)
            {
                Bread = null;
            }

            OnChanged();
            return true;
        }
    }
}
