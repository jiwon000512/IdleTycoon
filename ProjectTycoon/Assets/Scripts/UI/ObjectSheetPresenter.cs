using System;
using System.Collections.Generic;
using System.Globalization;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    public enum SheetTargetKind
    {
        Shelf,
        LockedShelf,
        Oven,
        LockedOven,
        Counter,
    }

    // 사물 터치 기획(2026-09-23): 탭한 사물에 맞춰 시트 내용을 만든다. 규칙은 ShopSim에서 읽기만 하고, 구매·굽기는 Try*로 부탁한다.
    // 열린 동안 재고·오븐·업그레이드·코인·웜뱃 이벤트로 갱신한다
    public sealed class ObjectSheetPresenter : IDisposable
    {
        private readonly ObjectSheetView m_view;
        private readonly ShopSim m_shop;
        private readonly ZooState m_state;
        private readonly GameTables m_tables;
        private readonly List<string> m_rowActions = new List<string>();
        private readonly List<string> m_chipBreads = new List<string>();

        private SheetTargetKind m_kind;
        private int m_index;

        public ObjectSheetPresenter(ObjectSheetView view, ShopSim shop, ZooState state, GameTables tables)
        {
            m_view = view;
            m_shop = shop;
            m_state = state;
            m_tables = tables;

            m_view.ChipClicked += View_ChipClicked;
            m_view.RowClicked += View_RowClicked;
            m_view.CloseRequested += View_CloseRequested;
            m_shop.StockChanged += Shop_Changed;
            m_shop.OvenChanged += Shop_OvenChanged;
            m_shop.UpgradesChanged += Shop_Refresh;
            m_shop.LayoutChanged += Shop_Refresh;
            m_shop.QueueChanged += Shop_Refresh;
            m_shop.WombatLeft += Shop_WombatMoved;
            m_shop.WombatReturned += Shop_Refresh;
            m_state.CoinsChanged += Shop_Refresh;
        }

        public void Dispose()
        {
            m_view.ChipClicked -= View_ChipClicked;
            m_view.RowClicked -= View_RowClicked;
            m_view.CloseRequested -= View_CloseRequested;
            m_shop.StockChanged -= Shop_Changed;
            m_shop.OvenChanged -= Shop_OvenChanged;
            m_shop.UpgradesChanged -= Shop_Refresh;
            m_shop.LayoutChanged -= Shop_Refresh;
            m_shop.QueueChanged -= Shop_Refresh;
            m_shop.WombatLeft -= Shop_WombatMoved;
            m_shop.WombatReturned -= Shop_Refresh;
            m_state.CoinsChanged -= Shop_Refresh;
        }

        public void Show(SheetTargetKind kind, int index)
        {
            m_kind = kind;
            m_index = index;
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
            StringTable s = m_tables.Strings;

            switch (m_kind)
            {
                case SheetTargetKind.Shelf:
                {
                    BreadRecord bread = m_shop.UnlockedBreads[m_index];
                    m_view.SetHeader(s.Format("sheet_shelf_title", bread.Name), s.Format("sheet_shelf_status", m_shop.Stock(bread.Id), m_shop.ShelfCapacity));
                    AddUpgradeRows(rows, "shelf");
                    break;
                }
                case SheetTargetKind.LockedShelf:
                {
                    BreadRecord next = m_shop.NextBread;
                    m_view.SetHeader(s.Get("sheet_locked_shelf_title"), next != null ? s.Format("sheet_locked_shelf_status", next.Name) : "");
                    AddUnlockRow(rows);
                    break;
                }
                case SheetTargetKind.Oven:
                {
                    Oven oven = m_shop.Ovens[m_index];
                    m_view.SetHeader(s.Format("sheet_oven_title", m_index + 1), OvenStatus(oven));
                    bool canBake = oven.IsEmpty && m_shop.WombatAtCounter;

                    foreach (BreadRecord bread in m_shop.UnlockedBreads)
                    {
                        chips.Add(new SheetChip
                        {
                            SpritePath = bread.Sprite,
                            Label = s.Format("chip_bread", bread.Name),
                            Sub = s.Format("chip_bread_sub", m_shop.Stock(bread.Id), bread.BakeSeconds),
                            Highlighted = canBake && m_shop.Stock(bread.Id) == 0,
                            Enabled = canBake,
                        });
                        m_chipBreads.Add(bread.Id);
                    }

                    if (m_shop.NextBread != null)
                    {
                        chips.Add(new SheetChip { Label = m_shop.NextBread.Name, Sub = s.Get("chip_locked_sub"), Locked = true });
                        m_chipBreads.Add(null);
                    }

                    AddUpgradeRows(rows, "oven");
                    break;
                }
                case SheetTargetKind.LockedOven:
                    m_view.SetHeader(s.Get("sheet_locked_oven_title"), "");
                    AddUpgradeRows(rows, "oven", ShopSim.k_OvenCount);
                    break;
                case SheetTargetKind.Counter:
                    m_view.SetHeader(s.Get("sheet_counter_title"), s.Format("sheet_counter_status", m_shop.Queue.Count));
                    AddUpgradeRows(rows, "counter");
                    break;
            }

            m_view.SetChips(chips);
            m_view.SetRows(rows);
        }

        private string OvenStatus(Oven oven)
        {
            StringTable s = m_tables.Strings;

            if (!oven.IsEmpty)
            {
                return oven.Ready > 0 ? s.Get("sheet_oven_full") : s.Format("sheet_oven_baking", oven.Bread.Name, Math.Ceiling(oven.Remaining));
            }

            return m_shop.WombatAtCounter ? s.Get("sheet_oven_empty") : s.Get("sheet_oven_errand");
        }

        // shop_upgrades.target이 같은 행마다 구매 행. only가 있으면 그 id만
        private void AddUpgradeRows(List<SheetRow> rows, string target, string only = null)
        {
            foreach (ShopUpgradeRecord upgrade in m_tables.ShopUpgrades)
            {
                if (upgrade.Target != target || (only != null && upgrade.Id != only))
                {
                    continue;
                }

                int level = m_shop.UpgradeLevel(upgrade.Id);
                SheetRow row = new SheetRow { Name = m_tables.Strings.Format("row_upgrade", upgrade.Name, level) };

                if (m_shop.IsMaxed(upgrade.Id))
                {
                    row.Effect = Effect(upgrade, level, level);
                    row.Cost = m_tables.Strings.Get("row_max");
                    row.State = SheetRowState.Max;
                }
                else
                {
                    double cost = m_shop.UpgradeCost(upgrade.Id);
                    row.Effect = Effect(upgrade, level, level + 1);
                    row.Cost = BigNumberFormatter.Format(cost);
                    row.State = m_state.Coins >= cost ? SheetRowState.Enabled : SheetRowState.Poor;
                }

                rows.Add(row);
                m_rowActions.Add(upgrade.Id);
            }
        }

        private string Effect(ShopUpgradeRecord upgrade, int fromLevel, int toLevel)
        {
            return string.Format(CultureInfo.InvariantCulture, upgrade.EffectFormat, m_shop.UpgradeValue(upgrade.Id, fromLevel), m_shop.UpgradeValue(upgrade.Id, toLevel));
        }

        private void AddUnlockRow(List<SheetRow> rows)
        {
            BreadRecord next = m_shop.NextBread;

            if (next == null)
            {
                return;
            }

            rows.Add(new SheetRow
            {
                Name = m_tables.Strings.Format("row_unlock", next.Name),
                Effect = m_tables.Strings.Get("row_unlock_effect"),
                Cost = BigNumberFormatter.Format(next.UnlockCost),
                State = m_state.Coins >= next.UnlockCost ? SheetRowState.Enabled : SheetRowState.Poor,
            });
            m_rowActions.Add(null);
        }

        private void View_ChipClicked(int index)
        {
            string breadId = m_chipBreads[index];

            if (breadId != null && m_shop.TryBake(m_index, breadId))
            {
                m_view.Close();
            }
        }

        private void View_RowClicked(int index)
        {
            string upgradeId = m_rowActions[index];
            bool bought = upgradeId == null ? m_shop.TryUnlockNextBread() : m_shop.TryUpgrade(upgradeId);

            if (bought)
            {
                m_view.FlashRow(index);

                if (m_kind == SheetTargetKind.LockedShelf || m_kind == SheetTargetKind.LockedOven)
                {
                    m_view.Close();
                }
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void Shop_Changed(string breadId)
        {
            Shop_Refresh();
        }

        private void Shop_OvenChanged(int oven)
        {
            Shop_Refresh();
        }

        private void Shop_WombatMoved(int oven, double seconds)
        {
            Shop_Refresh();
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
