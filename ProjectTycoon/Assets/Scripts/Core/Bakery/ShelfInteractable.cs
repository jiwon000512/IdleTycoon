using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13: 진열대 하나 = 빵 한 종류와 그 재고. 웜뱃이 채우고 손님이 하나씩 집는다. 용량은 업그레이드
    public sealed class ShelfInteractable : Interactable
    {
        public const string k_Id = "shelf";

        private readonly Vector2 m_base;

        public Cell Cell { get; }
        public BakeryArea Bakery { get; }
        public BreadTable Bread { get; }
        public int Stock { get; private set; }
        public int Capacity => (int)UpgradeValue(UpgradeLevel);

        public ShelfInteractable(InteractableTable table, Cell cell, BreadTable bread, BakeryArea bakery) : base(table, bakery)
        {
            Cell = cell;
            Bread = bread;
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

        // 자리만큼 올려놓고 올린 수를 돌려준다
        public int Put(int count)
        {
            int placed = Math.Min(count, Capacity - Stock);
            Stock += placed;
            OnChanged();
            return placed;
        }

        // 손님이 하나 집는다. 비었으면 false
        public bool TryPick()
        {
            if (Stock == 0)
            {
                return false;
            }

            Stock--;
            OnChanged();
            return true;
        }
    }
}
