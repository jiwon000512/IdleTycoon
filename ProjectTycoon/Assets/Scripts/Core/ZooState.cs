using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 지갑(코인)과 창고(재료, 설계 25). 웜뱃의 일꾼(Worker)이 갖고, 계산대가 값을 넣고 밭이 재료를 넣고 오븐이 재료를 뺀다.
    // 바뀌면 CoinsChanged · ItemsChanged 사건
    public sealed class ZooState
    {
        private readonly EventBus m_bus;
        private readonly Dictionary<string, int> m_items = new Dictionary<string, int>(StringComparer.Ordinal);

        public double Coins { get; private set; }
        // 설계 30: 석상 축복(가게 전부에 걸리는 시간제 효과)
        public Blessing Blessing { get; private set; }

        private ZooState(double coins, EventBus bus)
        {
            Coins = coins;
            m_bus = bus;
        }

        // 기획서 6.4: 시작 상태 — 코인 350. 설계 25: 창고에는 재료마다 ItemTable start개
        public static ZooState CreateNew(TableSet tables, EventBus bus)
        {
            ZooState state = new ZooState(tables.Get<ConfigTable>(ConfigTable.k_StartCoins).Value, bus);
            state.Blessing = new Blessing(tables, bus);

            foreach (ItemTable item in tables.GetAll<ItemTable>())
            {
                state.m_items[item.Id] = item.Start;
            }

            return state;
        }

        public void AddCoins(double amount)
        {
            Coins += amount;
            OnCoinsChanged();
        }

        public bool TrySpendCoins(double amount)
        {
            if (Coins < amount)
            {
                return false;
            }

            Coins -= amount;
            OnCoinsChanged();
            return true;
        }

        // 설계 25: 창고에 든 그 재료 수
        public int Count(string itemId)
        {
            m_items.TryGetValue(itemId, out int count);
            return count;
        }

        public void AddItem(string itemId, int count)
        {
            m_items[itemId] = Count(itemId) + count;
            OnItemsChanged();
        }

        // 레시피 재료가 다 있다
        public bool Has(IReadOnlyList<IngredientData> ingredients)
        {
            foreach (IngredientData ingredient in ingredients)
            {
                if (Count(ingredient.Item) < ingredient.Count)
                {
                    return false;
                }
            }

            return true;
        }

        // 레시피만큼 뺀다. 하나라도 모자라면 아무것도 빼지 않고 false
        public bool TrySpendItems(IReadOnlyList<IngredientData> ingredients)
        {
            if (!Has(ingredients))
            {
                return false;
            }

            foreach (IngredientData ingredient in ingredients)
            {
                m_items[ingredient.Item] = Count(ingredient.Item) - ingredient.Count;
            }

            OnItemsChanged();
            return true;
        }

        // 한 가지 재료를 count개 뺀다. 모자라면 빼지 않고 false
        public bool TrySpendItem(string itemId, int count)
        {
            if (Count(itemId) < count)
            {
                return false;
            }

            m_items[itemId] = Count(itemId) - count;
            OnItemsChanged();
            return true;
        }

        private void OnCoinsChanged()
        {
            m_bus.Publish(new Events.CoinsChanged(this));
        }

        private void OnItemsChanged()
        {
            m_bus.Publish(new Events.ItemsChanged(this));
        }
    }
}
