using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 사물 터치 기획 → 설계 09 → 설계 13 v0.5: 상호작용 버튼으로 연 사물의 시트. 줄은 그 사물의 시트 행동(InteractableTable.actions 중 mode sheet)이 내놓고,
    // 여기서는 행동 id별 서식으로 글자만 만든다(굽기 = 칩, 나머지 = 행). 제목·상태 줄만 사물 종류로 가른다. 줄을 누르면 곳에 TryChoose로 부탁한다.
    // 열린 동안 사물(계산대 줄 포함)·업그레이드·배치·코인 이벤트로 갱신하고, 웜뱃의 대상이 바뀌면 닫는다
    public sealed class ObjectSheetPresenter : IDisposable
    {
        private readonly ObjectSheetView m_view;
        private readonly BakeryArea m_shop;
        private readonly TableSet m_tables;
        // 칩·행 번호 → (행동 id, 고른 것)
        private readonly List<(string action, string option)> m_chips = new List<(string action, string option)>();
        private readonly List<(string action, string option)> m_rows = new List<(string action, string option)>();
        private readonly IDisposable[] m_subscriptions;

        private Interactable m_target;

        public ObjectSheetPresenter(ObjectSheetView view, BakeryArea shop, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_shop = shop;
            m_tables = tables;

            m_view.ChipClicked += View_ChipClicked;
            m_view.RowClicked += View_RowClicked;
            m_view.CloseRequested += View_CloseRequested;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.ThingChanged>(Bus_ThingChanged),
                bus.Subscribe<Events.Upgraded>(Bus_Upgraded),
                bus.Subscribe<Events.LayoutChanged>(Bus_LayoutChanged),
                bus.Subscribe<Events.TargetChanged>(Bus_TargetChanged),
                bus.Subscribe<Events.CoinsChanged>(Bus_CoinsChanged),
            };
        }

        public void Dispose()
        {
            m_view.ChipClicked -= View_ChipClicked;
            m_view.RowClicked -= View_RowClicked;
            m_view.CloseRequested -= View_CloseRequested;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        public void Show(Interactable target)
        {
            m_target = target;
            Refresh();
            m_view.Open();
        }

        public void Hide()
        {
            m_view.Close();
        }

        private void Refresh()
        {
            m_chips.Clear();
            m_rows.Clear();
            List<SheetChip> chips = new List<SheetChip>();
            List<SheetRow> rows = new List<SheetRow>();
            SetHeader();

            foreach (SheetAction action in m_shop.SheetActions(m_target))
            {
                foreach (SheetOption option in action.Options(m_shop.Wombat.Worker, m_target))
                {
                    if (action.Table.Id == ActionTable.k_Bake)
                    {
                        chips.Add(Chip(option));
                        m_chips.Add((action.Table.Id, option.Option));
                    }
                    else
                    {
                        rows.Add(Row(action.Table.Id, option));
                        m_rows.Add((action.Table.Id, option.Option));
                    }
                }
            }

            m_view.SetChips(chips);
            m_view.SetRows(rows);
        }

        private void SetHeader()
        {
            switch (m_target)
            {
                case ShelfInteractable shelf:
                    m_view.SetHeader(m_tables.Format("sheet_shelf_title", shelf.Bread.Name), m_tables.Format("sheet_shelf_status", shelf.Stock, shelf.Capacity));
                    break;
                case OvenInteractable oven:
                    m_view.SetHeader(m_tables.Format("sheet_oven_title", IndexOf(oven) + 1), OvenStatus(oven));
                    break;
                case CounterInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_counter_title"), m_tables.Format("sheet_counter_status", m_shop.Counter.Queue.Count));
                    break;
                case SlotInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_slot_title"), m_tables.Text("sheet_slot_status"));
                    break;
                case DigInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_dig_title"), m_tables.Format("sheet_dig_status", m_shop.Grid.Cells.Count));
                    break;
            }
        }

        private string OvenStatus(OvenInteractable oven)
        {
            if (!oven.IsEmpty)
            {
                return oven.Ready > 0 ? m_tables.Text("sheet_oven_full") : m_tables.Format("sheet_oven_baking", oven.Bread.Name, Math.Ceiling(oven.Remaining));
            }

            return m_tables.Text("sheet_oven_empty");
        }

        // 굽기 칩: 해금된 빵(빈 오븐일 때만 고를 수 있고, 재고가 없으면 강조) · 다음 빵 잠김
        private SheetChip Chip(SheetOption option)
        {
            BreadTable bread = m_tables.Get<BreadTable>(option.Option);

            if (option.State == SheetOptionState.Locked)
            {
                return new SheetChip { Label = bread.Name, Sub = m_tables.Text("chip_locked_sub"), Locked = true };
            }

            int stock = m_shop.ShelfOf(bread.Id).Stock;
            bool enabled = option.State == SheetOptionState.Enabled;

            return new SheetChip
            {
                SpritePath = bread.Sprite,
                Label = m_tables.Format("chip_bread", bread.Name),
                Sub = m_tables.Format("chip_bread_sub", stock, bread.BakeSeconds),
                Highlighted = enabled && stock == 0,
                Enabled = enabled,
            };
        }

        private SheetRow Row(string actionId, SheetOption option)
        {
            switch (actionId)
            {
                // 업그레이드: 사물 행의 이름 Lv · 효과 전후 · 가격
                case ActionTable.k_Upgrade:
                {
                    UpgradeInfo upgrade = m_target.Table.Upgrade;

                    return new SheetRow
                    {
                        Name = m_tables.Format("row_upgrade", upgrade.Name, option.Level),
                        Effect = string.Format(CultureInfo.InvariantCulture, upgrade.EffectFormat, option.Before, option.After),
                        Cost = Cost(option),
                        State = RowState(option.State),
                    };
                }
                case ActionTable.k_PlaceOven:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_place_oven"),
                        Effect = m_tables.Format("row_place_oven_effect", option.Before, option.After),
                        Cost = Cost(option),
                        State = RowState(option.State),
                    };
                case ActionTable.k_Unlock:
                    return new SheetRow
                    {
                        Name = m_tables.Format("row_unlock", m_tables.Get<BreadTable>(option.Option).Name),
                        Effect = m_tables.Text(option.State == SheetOptionState.Blocked ? "row_unlock_blocked" : "row_unlock_effect"),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                case ActionTable.k_Dig:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_dig"),
                        Effect = m_tables.Text("row_dig_effect"),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                default:
                    throw new InvalidOperationException($"행동 '{actionId}'의 시트 서식이 없다.");
            }
        }

        private string Cost(SheetOption option)
        {
            return option.State == SheetOptionState.Max ? m_tables.Text("row_max") : BigNumberFormatter.Format(option.Cost);
        }

        private static SheetRowState RowState(SheetOptionState state)
        {
            switch (state)
            {
                case SheetOptionState.Enabled: return SheetRowState.Enabled;
                case SheetOptionState.Poor: return SheetRowState.Poor;
                case SheetOptionState.Max: return SheetRowState.Max;
                default: return SheetRowState.Blocked;
            }
        }

        // 오븐 번호(시트 제목 "오븐 1")
        private int IndexOf(OvenInteractable oven)
        {
            for (int i = 0; i < m_shop.Ovens.Count; i++)
            {
                if (m_shop.Ovens[i] == oven)
                {
                    return i;
                }
            }

            return -1;
        }

        private void View_ChipClicked(int index)
        {
            (string action, string option) = m_chips[index];

            if (m_shop.TryChoose(action, m_target, option))
            {
                m_view.Close();
            }
        }

        // 빈 자리·파기 자리는 사면 사물이 바뀌어 시트를 닫는다
        private void View_RowClicked(int index)
        {
            (string action, string option) = m_rows[index];

            if (!m_shop.TryChoose(action, m_target, option))
            {
                return;
            }

            m_view.FlashRow(index);

            if (m_target is SlotInteractable || m_target is DigInteractable)
            {
                m_view.Close();
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void Bus_ThingChanged(Events.ThingChanged e)
        {
            if (e.Thing.Area == m_shop)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_Upgraded(Events.Upgraded e)
        {
            if (e.Area == m_shop)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Bakery == m_shop)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_CoinsChanged(Events.CoinsChanged e)
        {
            RefreshIfOpen();
        }

        // 걸어서 다른 사물로 가면(또는 배치가 바뀌어 대상이 사라지면) 닫는다
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area == m_shop && m_view.IsVisible && m_shop.Target != m_target)
            {
                m_view.Close();
            }
        }

        private void RefreshIfOpen()
        {
            if (m_view.IsVisible)
            {
                Refresh();
            }
        }
    }
}
