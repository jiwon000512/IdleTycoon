using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 26 창고(인벤토리): 칸 = ItemTable 행 순서(0개는 흐리게, 4열을 채우도록 빈 칸, 최소 두 줄). 처음에는 아무 칸도 고르지 않는다.
    // 칸을 누르면 정보 창: 이름 · 개수 · 설명(ItemTable), 획득처 = 그 재료를 거두는 작물(CropTable: 농장 밭 · 자라는 초 · 한 번에 몇 개), 사용처 = 레시피에 그 재료가 든 빵(BreadTable).
    // 설계 28: 작물 · 빵 표에서 찾을 수 없는 획득처 · 사용처(똥 치우기 · 심을 때 자동 · 거둘 때 덤 · 석상)는 ItemTable source · use 글 한 줄.
    // 열린 동안 ItemsChanged로 갱신, 편집 모드에서는 버튼을 숨긴다
    public sealed class InventoryPresenter : IDisposable
    {
        private const int k_Columns = 4;
        private const int k_MinSlots = 8;

        private readonly InventoryView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable m_items;
        // 정보 창에 보이는 재료. 없으면 null
        private ItemTable m_shown;

        public InventoryPresenter(InventoryView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.OpenClicked += View_OpenClicked;
            m_view.CloseRequested += View_CloseRequested;
            m_view.SlotClicked += View_SlotClicked;
            m_view.InfoCloseRequested += View_InfoCloseRequested;
            m_items = bus.Subscribe<Events.ItemsChanged>(Bus_ItemsChanged);
            m_view.SetLabels(tables.Text("inventory_title"), tables.Text("inventory_source"), tables.Text("inventory_uses"), tables.Text("inventory_none"));
        }

        public void Dispose()
        {
            m_view.OpenClicked -= View_OpenClicked;
            m_view.CloseRequested -= View_CloseRequested;
            m_view.SlotClicked -= View_SlotClicked;
            m_view.InfoCloseRequested -= View_InfoCloseRequested;
            m_items.Dispose();
        }

        // 편집 모드에서는 버튼을 숨긴다(점원 버튼과 같이)
        public void SetEditing(bool editing)
        {
            m_view.SetButtonVisible(!editing);

            if (editing)
            {
                m_view.Close();
            }
        }

        private void RefreshSlots()
        {
            IReadOnlyList<ItemTable> items = m_tables.GetAll<ItemTable>();
            int count = Math.Max(k_MinSlots, (items.Count + k_Columns - 1) / k_Columns * k_Columns);
            List<InventoryView.SlotData> slots = new List<InventoryView.SlotData>();

            for (int i = 0; i < count; i++)
            {
                if (i >= items.Count)
                {
                    slots.Add(new InventoryView.SlotData());
                    continue;
                }

                int have = m_state.Count(items[i].Id);
                slots.Add(new InventoryView.SlotData
                {
                    IconPath = items[i].Icon,
                    Count = m_tables.Format("inventory_count", have),
                    Name = items[i].Name,
                    Dim = have == 0,
                });
            }

            m_view.SetSlots(slots);
        }

        private void ShowInfo(ItemTable item)
        {
            m_shown = item;
            List<InventoryView.SourceData> sources = new List<InventoryView.SourceData>();

            foreach (CropTable crop in m_tables.GetAll<CropTable>())
            {
                if (crop.Item == item.Id)
                {
                    sources.Add(new InventoryView.SourceData
                    {
                        Place = m_tables.Text("inventory_farm"),
                        Seconds = m_tables.Format("inventory_seconds", crop.GrowSeconds),
                        IconPath = item.Icon,
                        Yield = m_tables.Format("inventory_yield", crop.Yield),
                    });
                }
            }

            if (item.Source != null)
            {
                sources.Add(new InventoryView.SourceData { Place = m_tables.Text(item.Source) });
            }

            List<InventoryView.UseData> uses = new List<InventoryView.UseData>();

            foreach (BreadTable bread in m_tables.GetAll<BreadTable>())
            {
                foreach (IngredientData ingredient in bread.Ingredients)
                {
                    if (ingredient.Item == item.Id)
                    {
                        uses.Add(new InventoryView.UseData { IconPath = bread.Sprite, Name = bread.Name, Count = m_tables.Format("inventory_yield", ingredient.Count) });
                    }
                }
            }

            if (item.Use != null)
            {
                uses.Add(new InventoryView.UseData { Name = m_tables.Text(item.Use) });
            }

            m_view.ShowInfo(item.Icon, item.Name, m_tables.Format("inventory_have", m_state.Count(item.Id)), item.Desc, sources, uses);
        }

        private void View_OpenClicked()
        {
            m_shown = null;
            RefreshSlots();
            m_view.Open();
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_SlotClicked(int index)
        {
            ShowInfo(m_tables.GetAll<ItemTable>()[index]);
        }

        private void View_InfoCloseRequested()
        {
            m_shown = null;
            m_view.HideInfo();
        }

        private void Bus_ItemsChanged(Events.ItemsChanged e)
        {
            if (e.Wallet != m_state || !m_view.IsOpen)
            {
                return;
            }

            RefreshSlots();

            if (m_shown != null && m_view.IsInfoOpen)
            {
                ShowInfo(m_shown);
            }
        }
    }
}
