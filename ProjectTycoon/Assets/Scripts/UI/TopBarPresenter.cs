using System;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class TopBarPresenter : IDisposable
    {
        private const string k_CoinsKey = "topbar_coins";

        private readonly TopBarView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable m_coins;

        public TopBarPresenter(TopBarView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;

            m_coins = bus.Subscribe<Events.CoinsChanged>(Bus_CoinsChanged);
            RefreshCoins();
        }

        public void Dispose()
        {
            m_coins.Dispose();
        }

        private void Bus_CoinsChanged(Events.CoinsChanged e)
        {
            if (e.Wallet == m_state)
            {
                RefreshCoins();
            }
        }

        private void RefreshCoins()
        {
            m_view.SetCoins(m_state.Coins, coins => m_tables.Format(k_CoinsKey, BigNumberFormatter.Format(coins)));
        }
    }
}
