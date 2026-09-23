using System;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class TopBarPresenter : IDisposable
    {
        private const string k_CoinsKey = "topbar_coins";

        private readonly TopBarView m_view;
        private readonly ZooState m_state;
        private readonly GameTables m_tables;

        public TopBarPresenter(TopBarView view, ZooState state, GameTables tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;

            m_state.CoinsChanged += State_CoinsChanged;
            RefreshCoins();
        }

        public void Dispose()
        {
            m_state.CoinsChanged -= State_CoinsChanged;
        }

        private void State_CoinsChanged()
        {
            RefreshCoins();
        }

        private void RefreshCoins()
        {
            m_view.SetCoins(m_state.Coins, coins => m_tables.Strings.Format(k_CoinsKey, BigNumberFormatter.Format(coins)));
        }
    }
}
