using System;
using System.Collections.Generic;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 08 v0.5: 빈 오븐을 누르면 해금한 빵 목록을 보여 주고, 고르면 굽기 시작
    public sealed class BakePopupPresenter : IDisposable
    {
        private const string k_TitleKey = "bake_title";
        private const string k_CloseKey = "popup_close";
        private const string k_RowKey = "bake_row";

        private readonly BakePopupView m_view;
        private readonly ShopSim m_shop;
        private readonly GameTables m_tables;
        private int m_ovenIndex;

        public BakePopupPresenter(BakePopupView view, ShopSim shop, GameTables tables)
        {
            m_view = view;
            m_shop = shop;
            m_tables = tables;

            m_view.SetTexts(tables.Strings.Get(k_TitleKey), tables.Strings.Get(k_CloseKey));
            m_view.RowClicked += View_RowClicked;
            m_view.CloseClicked += View_CloseClicked;
            m_view.SetVisible(false);
        }

        public void Dispose()
        {
            m_view.RowClicked -= View_RowClicked;
            m_view.CloseClicked -= View_CloseClicked;
        }

        public void Show(int ovenIndex)
        {
            m_ovenIndex = ovenIndex;
            List<string> labels = new List<string>();
            List<bool> interactable = new List<bool>();

            foreach (BreadRecord bread in m_shop.UnlockedBreads)
            {
                labels.Add(m_tables.Strings.Format(k_RowKey, bread.Name, bread.BakeSeconds, bread.BatchSize, m_shop.Stock(bread.Id)));
                interactable.Add(true);
            }

            m_view.SetRows(labels, interactable);
            m_view.SetVisible(true);
        }

        private void View_RowClicked(int index)
        {
            m_shop.TryBake(m_ovenIndex, m_shop.UnlockedBreads[index].Id);
            m_view.SetVisible(false);
        }

        private void View_CloseClicked()
        {
            m_view.SetVisible(false);
        }
    }
}
