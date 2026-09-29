using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class TopBarPresenter : IDisposable
    {
        private const string k_CoinsKey = "topbar_coins";
        private const string k_ItemKey = "topbar_item";

        private readonly TopBarView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable m_coins;
        private readonly IDisposable m_items;

        private double m_last;
        private double m_spent;

        public TopBarPresenter(TopBarView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;

            m_last = state.Coins;
            m_coins = bus.Subscribe<Events.CoinsChanged>(Bus_CoinsChanged);
            m_items = bus.Subscribe<Events.ItemsChanged>(Bus_ItemsChanged);
            RefreshCoins();
            RefreshItems();
        }

        public void Dispose()
        {
            m_coins.Dispose();
            m_items.Dispose();
        }

        private void Bus_ItemsChanged(Events.ItemsChanged e)
        {
            if (e.Wallet == m_state)
            {
                RefreshItems();
            }
        }

        // 설계 25: 재료마다 알약 하나(ItemTable 순서, 0개여도 보인다)
        private void RefreshItems()
        {
            List<(string, string)> items = new List<(string, string)>();

            foreach (ItemTable item in m_tables.GetAll<ItemTable>())
            {
                items.Add((item.Icon, m_tables.Format(k_ItemKey, m_state.Count(item.Id))));
            }

            m_view.SetItems(items);
        }

        private void Bus_CoinsChanged(Events.CoinsChanged e)
        {
            if (e.Wallet != m_state)
            {
                return;
            }

            // 쓴 코인: 표시가 떠 있는 동안 이어 쓰면 합친다(월급날은 점원마다 한 번씩 빠진다)
            if (m_state.Coins < m_last)
            {
                m_spent = (m_view.SpentVisible ? m_spent : 0d) + m_last - m_state.Coins;
                m_view.ShowSpent("-" + BigNumberFormatter.Format(m_spent));
            }

            m_last = m_state.Coins;
            RefreshCoins();
        }

        private void RefreshCoins()
        {
            m_view.SetCoins(m_state.Coins, coins => m_tables.Format(k_CoinsKey, BigNumberFormatter.Format(coins)));
        }
    }
}
