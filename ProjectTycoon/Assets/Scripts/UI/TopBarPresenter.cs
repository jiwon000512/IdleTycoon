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
        private readonly IncomeService m_income;
        private readonly GameTables m_tables;

        // 규칙 예외: 상단 바는 코인·수입·레벨 세 모델을 한 줄에 모으는 화면이라 생성자 매개변수가 5개다
        public TopBarPresenter(
            TopBarView view, ZooState state, ZooLevelService zooLevelService, IncomeService income, GameTables tables)
        {
            m_view = view;
            m_state = state;
            m_zooLevelService = zooLevelService;
            m_income = income;
            m_tables = tables;

            m_state.CoinsChanged += State_CoinsChanged;
            m_income.IncomeChanged += Income_IncomeChanged;
            m_zooLevelService.ZooLevelReached += ZooLevelService_ZooLevelReached;

            RefreshCoins();
            RefreshIncome();
            RefreshZooLevel();
        }

        public void Dispose()
        {
            m_state.CoinsChanged -= State_CoinsChanged;
            m_income.IncomeChanged -= Income_IncomeChanged;
            m_zooLevelService.ZooLevelReached -= ZooLevelService_ZooLevelReached;
        }

        private void State_CoinsChanged()
        {
            RefreshCoins();
        }

        private void Income_IncomeChanged()
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
            m_view.SetIncome(m_tables.Strings.Format(k_IncomeKey, BigNumberFormatter.Format(m_income.IncomePerSecond)));
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
