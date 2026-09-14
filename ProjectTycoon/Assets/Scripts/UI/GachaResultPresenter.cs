using System;
using GameKit.UI;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class GachaResultPresenter : IDisposable
    {
        private const string k_NewKey = "gacha_result_new";
        private const string k_LevelUpKey = "gacha_result_level_up";
        private const string k_DetailKey = "gacha_result_detail";

        private readonly GachaService m_gachaService;
        private readonly GameTables m_tables;

        public GachaResultPresenter(GachaService gachaService, GameTables tables)
        {
            m_gachaService = gachaService;
            m_tables = tables;

            m_gachaService.AnimalPulled += GachaService_AnimalPulled;
        }

        public void Dispose()
        {
            m_gachaService.AnimalPulled -= GachaService_AnimalPulled;
        }

        // 기획서 4장·8장: 결과 카드 — 새 종 / 한 마리 추가
        private void GachaService_AnimalPulled(PullResult result)
        {
            StringTable strings = m_tables.Strings;
            string title = result.Outcome == PullOutcome.Placed
                ? strings.Get(k_NewKey)
                : strings.Format(k_LevelUpKey, result.Level);

            double income = IncomeCalculator.AnimalIncome(
                result.Animal.BaseIncomePerSecond, result.Level, m_tables.Config.AnimalLevel.IncomeBonusPerLevel);
            string detail = strings.Format(
                k_DetailKey, m_tables.GetGrade(result.Animal.Grade).Name, BigNumberFormatter.Format(income));

            GachaResultView view = UIManager.Instance.Open<GachaResultView>();
            view.Show(
                AnimalVisuals.SpriteOf(result.Animal),
                AnimalVisuals.GradeColorOf(m_tables, result.Animal),
                title,
                detail);
        }
    }
}
