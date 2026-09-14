using System;
using System.Globalization;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class TopBarPresenter : IDisposable
    {
        private const string k_CoinsKey = "topbar_coins";
        private const string k_IncomeKey = "topbar_income";
        private const string k_LevelKey = "topbar_level";
        private const string k_ProgressKey = "topbar_progress";
        private const string k_ProgressMaxKey = "topbar_progress_max";

        private readonly TopBarView m_view;
        private readonly ZooState m_state;
        private readonly ZooLevelService m_zooLevelService;
        private readonly GameTables m_tables;

        public TopBarPresenter(TopBarView view, ZooState state, ZooLevelService zooLevelService, GameTables tables)
        {
            m_view = view;
            m_state = state;
            m_zooLevelService = zooLevelService;
            m_tables = tables;

            m_state.CoinsChanged += State_CoinsChanged;
            m_state.AnimalsChanged += State_AnimalsChanged;
            m_zooLevelService.ZooLevelReached += ZooLevelService_ZooLevelReached;

            RefreshCoins();
            RefreshIncome();
            RefreshZooLevel();
        }

        public void Dispose()
        {
            m_state.CoinsChanged -= State_CoinsChanged;
            m_state.AnimalsChanged -= State_AnimalsChanged;
            m_zooLevelService.ZooLevelReached -= ZooLevelService_ZooLevelReached;
        }

        private void State_CoinsChanged()
        {
            RefreshCoins();
        }

        private void State_AnimalsChanged()
        {
            RefreshIncome();
        }

        private void ZooLevelService_ZooLevelReached(int level)
        {
            RefreshZooLevel();
        }

        private void RefreshCoins()
        {
            m_view.SetCoins(m_tables.Strings.Format(k_CoinsKey, BigNumberFormatter.Format(m_state.Coins)));
            RefreshProgress();
        }

        private void RefreshIncome()
        {
            double income = IncomeCalculator.TotalIncomePerSecond(m_state, m_tables);
            m_view.SetIncome(m_tables.Strings.Format(k_IncomeKey, BigNumberFormatter.Format(income)));
        }

        private void RefreshZooLevel()
        {
            m_view.SetZooLevel(m_tables.Strings.Format(k_LevelKey, m_zooLevelService.Level));
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            double? nextThreshold = m_zooLevelService.NextThreshold;

            if (nextThreshold == null)
            {
                m_view.SetProgress(m_tables.Strings.Get(k_ProgressMaxKey));
                return;
            }

            m_view.SetProgress(m_tables.Strings.Format(
                k_ProgressKey,
                m_state.TotalCoinsEarned.ToString("N0", CultureInfo.InvariantCulture),
                nextThreshold.Value.ToString("N0", CultureInfo.InvariantCulture)));
        }
    }
}
