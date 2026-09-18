using System;
using System.Collections.Generic;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 08 v0.5: shop_upgrades 행마다 한 줄 + 마지막 줄 "굴 넓히기"(다음 빵 해금). 코인·단계가 바뀌면 다시 그린다
    public sealed class UpgradePopupPresenter : IDisposable
    {
        private const string k_TitleKey = "upgrade_title";
        private const string k_CloseKey = "popup_close";
        private const string k_RowKey = "upgrade_row";
        private const string k_RowMaxKey = "upgrade_row_max";
        private const string k_UnlockKey = "unlock_row";

        private readonly UpgradePopupView m_view;
        private readonly ShopSim m_shop;
        private readonly ZooState m_state;
        private readonly GameTables m_tables;

        public UpgradePopupPresenter(UpgradePopupView view, ShopSim shop, ZooState state, GameTables tables)
        {
            m_view = view;
            m_shop = shop;
            m_state = state;
            m_tables = tables;

            m_view.SetTexts(tables.Strings.Get(k_TitleKey), tables.Strings.Get(k_CloseKey));
            m_view.RowClicked += View_RowClicked;
            m_view.CloseClicked += View_CloseClicked;
            m_shop.UpgradesChanged += Shop_UpgradesChanged;
            m_state.CoinsChanged += State_CoinsChanged;
            m_view.SetVisible(false);
        }

        public void Dispose()
        {
            m_view.RowClicked -= View_RowClicked;
            m_view.CloseClicked -= View_CloseClicked;
            m_shop.UpgradesChanged -= Shop_UpgradesChanged;
            m_state.CoinsChanged -= State_CoinsChanged;
        }

        public void Show()
        {
            Refresh();
            m_view.SetVisible(true);
        }

        private void Shop_UpgradesChanged()
        {
            Refresh();
        }

        private void State_CoinsChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            List<string> labels = new List<string>();
            List<bool> interactable = new List<bool>();

            foreach (ShopUpgradeRecord upgrade in m_tables.ShopUpgrades)
            {
                int level = m_shop.UpgradeLevel(upgrade.Id);

                if (m_shop.IsMaxed(upgrade.Id))
                {
                    labels.Add(m_tables.Strings.Format(k_RowMaxKey, upgrade.Name, level));
                    interactable.Add(false);
                    continue;
                }

                double cost = m_shop.UpgradeCost(upgrade.Id);
                labels.Add(m_tables.Strings.Format(k_RowKey, upgrade.Name, level, BigNumberFormatter.Format(cost)));
                interactable.Add(m_state.Coins >= cost);
            }

            BreadRecord next = m_shop.NextBread;

            if (next != null)
            {
                labels.Add(m_tables.Strings.Format(k_UnlockKey, next.Name, BigNumberFormatter.Format(next.UnlockCost)));
                interactable.Add(m_state.Coins >= next.UnlockCost);
            }

            m_view.SetRows(labels, interactable);
        }

        private void View_RowClicked(int index)
        {
            if (index < m_tables.ShopUpgrades.Count)
            {
                m_shop.TryUpgrade(m_tables.ShopUpgrades[index].Id);
                return;
            }

            // 굴을 넓히면 새 층을 보여 주도록 팝업을 닫는다
            if (m_shop.TryUnlockNextBread())
            {
                m_view.SetVisible(false);
            }
        }

        private void View_CloseClicked()
        {
            m_view.SetVisible(false);
        }
    }
}
