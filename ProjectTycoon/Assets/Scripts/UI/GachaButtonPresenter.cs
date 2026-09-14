using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class GachaButtonPresenter : IDisposable
    {
        private const string k_LabelKey = "gacha_button_label";
        private const string k_CostKey = "gacha_button_cost";
        private const string k_GradeProbabilityKey = "gacha_grade_probability";
        private const string k_SeparatorKey = "gacha_probability_separator";
        private const string k_WaitKey = "gacha_button_wait";

        private readonly GachaButtonView m_view;
        private readonly GachaService m_gachaService;
        private readonly ZooState m_state;
        private readonly GameTables m_tables;

        public GachaButtonPresenter(GachaButtonView view, GachaService gachaService, ZooState state, GameTables tables)
        {
            m_view = view;
            m_gachaService = gachaService;
            m_state = state;
            m_tables = tables;

            m_view.Clicked += View_Clicked;
            m_state.CoinsChanged += State_CoinsChanged;
            m_gachaService.AnimalPulled += GachaService_AnimalPulled;

            m_view.SetLabel(m_tables.Strings.Get(k_LabelKey));
            m_view.SetProbabilities(BuildProbabilityLine());
            Refresh();
        }

        public void Dispose()
        {
            m_view.Clicked -= View_Clicked;
            m_state.CoinsChanged -= State_CoinsChanged;
            m_gachaService.AnimalPulled -= GachaService_AnimalPulled;
        }

        private void View_Clicked()
        {
            m_gachaService.TryPull(out _);
        }

        private void State_CoinsChanged()
        {
            Refresh();
        }

        private void GachaService_AnimalPulled(PullResult result)
        {
            Refresh();
        }

        // 기획서 4장: 비용 상시 표시, 코인 부족 시 비활성 + 남은 시간(수입 0이면 표시 없음)
        private void Refresh()
        {
            m_view.SetCost(m_tables.Strings.Format(k_CostKey, BigNumberFormatter.Format(m_gachaService.CurrentCost)));
            m_view.SetInteractable(m_gachaService.CanPull);

            double wait = m_gachaService.SecondsUntilAffordable;
            m_view.SetWait(wait > 0d && !double.IsPositiveInfinity(wait)
                ? m_tables.Strings.Format(k_WaitKey, (int)Math.Ceiling(wait))
                : string.Empty);
        }

        // 기획서 6.1: 등급 확률 한 줄
        private string BuildProbabilityLine()
        {
            IEnumerable<string> parts = m_tables.Grades
                .OrderBy(g => g.SortOrder)
                .Select(g => m_tables.Strings.Format(
                    k_GradeProbabilityKey,
                    g.Name,
                    (m_gachaService.Table.GradeProbability(g.Id) * 100d).ToString("0.#", CultureInfo.InvariantCulture)));

            return string.Join(m_tables.Strings.Get(k_SeparatorKey), parts);
        }
    }
}
