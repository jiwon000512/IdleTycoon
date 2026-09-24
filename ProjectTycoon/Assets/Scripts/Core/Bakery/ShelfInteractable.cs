using System;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 08·09·13: 진열대 하나 = 빵 한 종류와 그 재고. 웜뱃이 채우고(fill) 손님이 하나씩 집는다. 용량은 업그레이드
    public sealed class ShelfInteractable : Interactable
    {
        public const string k_Id = "shelf";

        private readonly Vector2 m_base;
        private readonly BakeryConfigTable m_config;
        private readonly Upgrades m_upgrades;

        public Cell Cell { get; }
        public BreadTable Bread { get; }
        public int Stock { get; private set; }
        public int Capacity => m_config.ShelfCapacity + (int)m_upgrades.Effect(ShopUpgradeTable.k_ShelfCapacity);

        public ShelfInteractable(InteractableTable table, Cell cell, BreadTable bread, BakeryLayout layout, BakeryConfigTable config, Upgrades upgrades) : base(table)
        {
            Cell = cell;
            Bread = bread;
            m_base = layout.ShelfBase(cell);
            m_config = config;
            m_upgrades = upgrades;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, m_base);
        }

        public override bool CanDo(string actionId, Hands hands)
        {
            return actionId != ActionTable.k_Fill || (hands.Bread == Bread && Stock < Capacity);
        }

        // 채우기: 자리만큼 한 번에 내려놓고 남은 건 계속 든다
        public override void Do(string actionId, Hands hands)
        {
            if (actionId != ActionTable.k_Fill)
            {
                return;
            }

            int count = Math.Min(hands.Count, Capacity - Stock);
            Stock += count;
            OnChanged();
            hands.Remove(count);
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
