using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 07 P5: 꾸미기 버튼 → 시설 팝업. 행마다 단계·효과·비용을 보여 주고 구매를 서비스에 넘긴다
    public sealed class FacilityPopupPresenter : IDisposable
    {
        private const string k_ButtonLabelKey = "facility_button_label";
        private const string k_TitleKey = "facility_popup_title";
        private const string k_CloseKey = "facility_popup_close";
        private const string k_StageKey = "facility_row_stage";
        private const string k_EffectKey = "facility_row_effect";
        private const string k_CostKey = "facility_row_cost";
        private const string k_LockedKey = "facility_row_locked";
        private const string k_MaxKey = "facility_row_max";

        private readonly FacilityButtonView m_button;
        private readonly FacilityPopupView m_popup;
        private readonly FacilityService m_facilityService;
        private readonly ZooState m_state;
        private readonly ZooLevelService m_zooLevelService;
        private readonly GameTables m_tables;
        private readonly Dictionary<FacilityRowView, FacilityRecord> m_rows = new Dictionary<FacilityRowView, FacilityRecord>();

        // 규칙 예외: 버튼·팝업 두 View와 코인·시설·레벨 세 모델을 한 화면에 모으므로 생성자 매개변수가 6개다
        public FacilityPopupPresenter(
            FacilityButtonView button, FacilityPopupView popup, FacilityService facilityService,
            ZooState state, ZooLevelService zooLevelService, GameTables tables)
        {
            m_button = button;
            m_popup = popup;
            m_facilityService = facilityService;
            m_state = state;
            m_zooLevelService = zooLevelService;
            m_tables = tables;

            m_button.Clicked += Button_Clicked;
            m_popup.CloseClicked += Popup_CloseClicked;
            m_state.CoinsChanged += State_CoinsChanged;
            m_state.FacilitiesChanged += State_FacilitiesChanged;
            m_zooLevelService.ZooLevelReached += ZooLevelService_ZooLevelReached;

            m_button.SetLabel(m_tables.Strings.Get(k_ButtonLabelKey));
            m_popup.SetTitle(m_tables.Strings.Get(k_TitleKey));
            m_popup.SetCloseLabel(m_tables.Strings.Get(k_CloseKey));

            foreach (FacilityRecord facility in m_tables.Facilities.OrderBy(f => f.SortOrder))
            {
                FacilityRowView row = m_popup.AddRow();
                row.SetName(facility.Name);
                row.UpgradeClicked += Row_UpgradeClicked;
                m_rows[row] = facility;
            }

            RefreshRows();
        }

        public void Dispose()
        {
            m_button.Clicked -= Button_Clicked;
            m_popup.CloseClicked -= Popup_CloseClicked;
            m_state.CoinsChanged -= State_CoinsChanged;
            m_state.FacilitiesChanged -= State_FacilitiesChanged;
            m_zooLevelService.ZooLevelReached -= ZooLevelService_ZooLevelReached;

            foreach (FacilityRowView row in m_rows.Keys)
            {
                row.UpgradeClicked -= Row_UpgradeClicked;
            }
        }

        private void Button_Clicked()
        {
            m_popup.Show();
        }

        private void Popup_CloseClicked()
        {
            m_popup.Hide();
        }

        private void Row_UpgradeClicked(FacilityRowView row)
        {
            m_facilityService.TryUpgrade(m_rows[row]);
        }

        private void State_CoinsChanged()
        {
            RefreshRows();
        }

        private void State_FacilitiesChanged()
        {
            RefreshRows();
        }

        private void ZooLevelService_ZooLevelReached(int level)
        {
            RefreshRows();
        }

        // 기획서 4장 꾸미기 탭: 단계 n/max · 효과 ×배수 · 비용 | Lv.n 해금 | MAX
        private void RefreshRows()
        {
            StringTable strings = m_tables.Strings;

            foreach (KeyValuePair<FacilityRowView, FacilityRecord> pair in m_rows)
            {
                FacilityRowView row = pair.Key;
                FacilityRecord facility = pair.Value;
                int stage = m_state.GetFacilityStage(facility.Id);

                row.SetStage(strings.Format(k_StageKey, stage, facility.MaxStage));
                row.SetEffect(strings.Format(
                    k_EffectKey,
                    IncomeCalculator.FacilityMultiplier(facility, stage).ToString("0.##", CultureInfo.InvariantCulture)));
                row.SetInteractable(m_facilityService.CanUpgrade(facility));

                if (!m_facilityService.IsUnlocked(facility))
                {
                    row.SetCost(strings.Format(k_LockedKey, facility.UnlockLevel));
                }
                else if (m_facilityService.IsMaxed(facility))
                {
                    row.SetCost(strings.Get(k_MaxKey));
                }
                else
                {
                    row.SetCost(strings.Format(k_CostKey, BigNumberFormatter.Format(m_facilityService.Cost(facility))));
                }
            }
        }
    }
}
