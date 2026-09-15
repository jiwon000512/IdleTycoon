using System;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public sealed class GachaResultPresenter : IDisposable
    {
        private const string k_NewKey = "gacha_result_new";
        private const string k_DuplicateKey = "gacha_result_duplicate";
        private const string k_DetailKey = "gacha_result_detail";

        private readonly GachaResultView m_view;
        private readonly GachaService m_gachaService;
        private readonly GameTables m_tables;

        public GachaResultPresenter(GachaResultView view, GachaService gachaService, GameTables tables)
        {
            m_view = view;
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
            GradeRecord grade = m_tables.GetGrade(result.Animal.Grade);
            string title = result.Outcome == PullOutcome.NewSpecies
                ? strings.Get(k_NewKey)
                : strings.Format(k_DuplicateKey, result.Count);

            double income = IncomeCalculator.AnimalIncome(
                result.Animal.BaseIncomePerSecond, result.Count, m_tables.Config.AnimalCount.IncomeBonusPerAnimal);
            string detail = strings.Format(k_DetailKey, grade.Name, BigNumberFormatter.Format(income));

            m_view.Show(result.Animal.Sprite, grade.ColorHex, title, detail);
        }
    }
}
