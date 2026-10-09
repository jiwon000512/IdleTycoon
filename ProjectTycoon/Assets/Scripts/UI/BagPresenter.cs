using System;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 55: 가방 = 창고(재료) + 유물. 가방 단추(창고 화면의 여는 단추)는 마지막에 본 탭을 열고(2026-10-09 사용자), 탭을 누르면 같은 자리에서 다른 팝업으로 바꾼다
    public sealed class BagPresenter : IDisposable
    {
        public const int k_Items = 0;
        public const int k_Relics = 1;

        private readonly InventoryView m_itemsView;
        private readonly RelicView m_relicsView;
        private readonly InventoryPresenter m_items;
        private readonly RelicPresenter m_relics;
        private readonly string[] m_labels;
        private int m_last = k_Items;

        public BagPresenter(InventoryView itemsView, RelicView relicsView, InventoryPresenter items, RelicPresenter relics, TableSet tables)
        {
            m_itemsView = itemsView;
            m_relicsView = relicsView;
            m_items = items;
            m_relics = relics;
            m_labels = new[] { tables.Text("inventory_title"), tables.Text("relic_title") };
            m_itemsView.OpenClicked += Open;
            m_itemsView.BagTabs.Clicked += Show;
            m_relicsView.BagTabs.Clicked += Show;
        }

        public void Dispose()
        {
            m_itemsView.OpenClicked -= Open;
            m_itemsView.BagTabs.Clicked -= Show;
            m_relicsView.BagTabs.Clicked -= Show;
        }

        public void Open()
        {
            Show(m_last);
        }

        public void Show(int tab)
        {
            m_last = tab;

            if (tab == k_Relics)
            {
                m_itemsView.Close();
                m_relics.Open();
            }
            else
            {
                m_relicsView.Close();
                m_items.Open();
            }

            m_itemsView.BagTabs.Show(tab, m_labels);
            m_relicsView.BagTabs.Show(tab, m_labels);
        }
    }
}
