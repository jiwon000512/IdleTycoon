using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 사물 터치 기획(2026-09-23) → 설계 09: 상호작용 버튼으로 연 사물에 맞춰 시트 내용을 만든다. 규칙은 BakeryArea에서 읽기만 하고, 구매·굽기는 Try*로 부탁한다.
    // 열린 동안 재고·오븐·업그레이드·코인 이벤트로 갱신하고, 웜뱃의 대상이 바뀌면 닫는다. 굴 격자 설계 v0.5: 빈 자리(진열대·오븐 놓기)·굴 파기 시트
    public sealed class ObjectSheetPresenter : IDisposable
    {
        private readonly ObjectSheetView m_view;
        private readonly BakeryArea m_shop;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private const string k_UnlockAction = "unlock";
        private const string k_DigAction = "dig";

        private readonly List<string> m_rowActions = new List<string>();
        private readonly List<string> m_chipBreads = new List<string>();

        private Interactable m_target;

        public ObjectSheetPresenter(ObjectSheetView view, BakeryArea shop, ZooState state, TableSet tables)
        {
            m_view = view;
            m_shop = shop;
            m_state = state;
            m_tables = tables;

            m_view.ChipClicked += View_ChipClicked;
            m_view.RowClicked += View_RowClicked;
            m_view.CloseRequested += View_CloseRequested;
            m_shop.ThingChanged += Shop_ThingChanged;
            m_shop.UpgradesChanged += Shop_Refresh;
            m_shop.LayoutChanged += Shop_Refresh;
            m_shop.QueueChanged += Shop_Refresh;
            m_shop.TargetChanged += Shop_TargetChanged;
            m_state.CoinsChanged += Shop_Refresh;
        }

        public void Dispose()
        {
            m_view.ChipClicked -= View_ChipClicked;
            m_view.RowClicked -= View_RowClicked;
            m_view.CloseRequested -= View_CloseRequested;
            m_shop.ThingChanged -= Shop_ThingChanged;
            m_shop.UpgradesChanged -= Shop_Refresh;
            m_shop.LayoutChanged -= Shop_Refresh;
            m_shop.QueueChanged -= Shop_Refresh;
            m_shop.TargetChanged -= Shop_TargetChanged;
            m_state.CoinsChanged -= Shop_Refresh;
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
            m_rowActions.Clear();
            m_chipBreads.Clear();
            List<SheetChip> chips = new List<SheetChip>();
            List<SheetRow> rows = new List<SheetRow>();

            switch (m_target)
            {
                case ShelfInteractable shelf:
                {
                    m_view.SetHeader(m_tables.Format("sheet_shelf_title", shelf.Bread.Name), m_tables.Format("sheet_shelf_status", shelf.Stock, shelf.Capacity));
                    AddUpgradeRows(rows, UpgradeTarget.Shelf);

                    // 빈 자리가 없어 다음 빵을 못 놓을 때만 안내 행
                    if (m_shop.NextBread != null && !HasEmptySlot())
                    {
                        AddUnlockRow(rows, SheetRowState.Blocked, m_tables.Text("row_unlock_blocked"));
                    }

                    break;
                }
                case SlotInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_slot_title"), m_tables.Text("sheet_slot_status"));
                    AddUnlockRow(rows, null, null);
                    AddUpgradeRows(rows, UpgradeTarget.Slot);
                    break;
                case DigInteractable _:
                {
                    double cost = m_shop.Grid.DigCost;
                    m_view.SetHeader(m_tables.Text("sheet_dig_title"), m_tables.Format("sheet_dig_status", m_shop.Grid.Cells.Count));
                    rows.Add(new SheetRow
                    {
                        Name = m_tables.Text("row_dig"),
                        Effect = m_tables.Text("row_dig_effect"),
                        Cost = BigNumberFormatter.Format(cost),
                        State = m_state.Coins >= cost ? SheetRowState.Enabled : SheetRowState.Poor,
                    });
                    m_rowActions.Add(k_DigAction);
                    break;
                }
                case OvenInteractable oven:
                {
                    m_view.SetHeader(m_tables.Format("sheet_oven_title", IndexOf(oven) + 1), OvenStatus(oven));
                    bool canBake = oven.IsEmpty;

                    foreach (BreadTable bread in m_shop.UnlockedBreads)
                    {
                        chips.Add(new SheetChip
                        {
                            SpritePath = bread.Sprite,
                            Label = m_tables.Format("chip_bread", bread.Name),
                            Sub = m_tables.Format("chip_bread_sub", m_shop.ShelfOf(bread.Id).Stock, bread.BakeSeconds),
                            Highlighted = canBake && m_shop.ShelfOf(bread.Id).Stock == 0,
                            Enabled = canBake,
                        });
                        m_chipBreads.Add(bread.Id);
                    }

                    if (m_shop.NextBread != null)
                    {
                        chips.Add(new SheetChip { Label = m_shop.NextBread.Name, Sub = m_tables.Text("chip_locked_sub"), Locked = true });
                        m_chipBreads.Add(null);
                    }

                    AddUpgradeRows(rows, UpgradeTarget.Oven);
                    break;
                }
                case CounterInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_counter_title"), m_tables.Format("sheet_counter_status", m_shop.Queue.Count));
                    AddUpgradeRows(rows, UpgradeTarget.Counter);
                    break;
            }

            m_view.SetChips(chips);
            m_view.SetRows(rows);
        }

        private string OvenStatus(OvenInteractable oven)
        {
            if (!oven.IsEmpty)
            {
                return oven.Ready > 0 ? m_tables.Text("sheet_oven_full") : m_tables.Format("sheet_oven_baking", oven.Bread.Name, Math.Ceiling(oven.Remaining));
            }

            return m_tables.Text("sheet_oven_empty");
        }

        // ShopUpgradeTable.Target이 같은 행마다 구매 행. only가 있으면 그 id만
        private void AddUpgradeRows(List<SheetRow> rows, UpgradeTarget target, string only = null)
        {
            foreach (ShopUpgradeTable upgrade in m_tables.GetAll<ShopUpgradeTable>())
            {
                if (upgrade.Target != target || (only != null && upgrade.Id != only))
                {
                    continue;
                }

                int level = m_shop.Upgrades.Level(upgrade.Id);
                SheetRow row = new SheetRow { Name = m_tables.Format("row_upgrade", upgrade.Name, level) };

                if (m_shop.Upgrades.IsMaxed(upgrade.Id))
                {
                    row.Effect = Effect(upgrade, level, level);
                    row.Cost = m_tables.Text("row_max");
                    row.State = SheetRowState.Max;
                }
                else
                {
                    double cost = m_shop.Upgrades.Cost(upgrade.Id);
                    row.Effect = Effect(upgrade, level, level + 1);
                    row.Cost = BigNumberFormatter.Format(cost);
                    row.State = m_state.Coins >= cost ? SheetRowState.Enabled : SheetRowState.Poor;
                }

                rows.Add(row);
                m_rowActions.Add(upgrade.Id);
            }
        }

        private string Effect(ShopUpgradeTable upgrade, int fromLevel, int toLevel)
        {
            return string.Format(CultureInfo.InvariantCulture, upgrade.EffectFormat, m_shop.Upgrades.Value(upgrade.Id, fromLevel), m_shop.Upgrades.Value(upgrade.Id, toLevel));
        }

        // 다음 빵 진열대 행. state·effect를 주면 그대로(안내용), 아니면 코인에 따라
        private void AddUnlockRow(List<SheetRow> rows, SheetRowState? state, string effect)
        {
            BreadTable next = m_shop.NextBread;

            if (next == null)
            {
                return;
            }

            rows.Add(new SheetRow
            {
                Name = m_tables.Format("row_unlock", next.Name),
                Effect = effect ?? m_tables.Text("row_unlock_effect"),
                Cost = BigNumberFormatter.Format(next.UnlockCost),
                State = state ?? (m_state.Coins >= next.UnlockCost ? SheetRowState.Enabled : SheetRowState.Poor),
            });
            m_rowActions.Add(k_UnlockAction);
        }

        private bool HasEmptySlot()
        {
            return m_shop.Slots.Count > 0;
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
            string breadId = m_chipBreads[index];

            if (breadId != null && m_shop.TryBake((OvenInteractable)m_target, breadId))
            {
                m_view.Close();
            }
        }

        private void View_RowClicked(int index)
        {
            string action = m_rowActions[index];
            bool bought;

            switch (action)
            {
                case k_UnlockAction:
                    bought = m_shop.TryUnlockNextBread(((SlotInteractable)m_target).Cell);
                    break;
                case k_DigAction:
                    bought = m_shop.Grid.TryDig(((DigInteractable)m_target).Cell);
                    break;
                case ShopUpgradeTable.k_OvenCount:
                    bought = m_shop.TryAddOven(((SlotInteractable)m_target).Cell);
                    break;
                default:
                    bought = m_shop.TryUpgrade(action);
                    break;
            }

            if (bought)
            {
                m_view.FlashRow(index);

                if (m_target is SlotInteractable || m_target is DigInteractable)
                {
                    m_view.Close();
                }
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void Shop_ThingChanged(Interactable thing)
        {
            Shop_Refresh();
        }

        // 걸어서 다른 사물로 가면(또는 배치가 바뀌어 대상이 사라지면) 닫는다
        private void Shop_TargetChanged()
        {
            if (m_view.IsVisible && m_shop.Target != m_target)
            {
                m_view.Close();
            }
        }

        private void Shop_Refresh()
        {
            if (m_view.IsVisible)
            {
                Refresh();
            }
        }
    }
}
