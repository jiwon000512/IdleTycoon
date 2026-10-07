using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 사물 터치 기획 → 설계 09 → 설계 13 v0.5: 상호작용 버튼으로 연 사물의 시트. 줄은 그 사물의 시트 행동(InteractableTable.actions 중 mode sheet)이 내놓고,
    // 여기서는 행동 id별 서식으로 글자만 만든다(굽기 · 심기 = 칩, 나머지 = 행). 제목·상태 줄만 사물 종류로 가른다. 줄을 누르면 대상의 곳(m_target.Area)에 TryChoose로 부탁한다(설계 27: 굴 파기 시트는 농장에서도 열린다).
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
        // 웜뱃의 대상이 아닌 사물의 시트(편집 모드에서 누른 흙 · 낚시판의 말뚝): 웜뱃 대상이 바뀌어도 닫지 않는다
        private bool m_remote;

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
                bus.Subscribe<Events.ItemsChanged>(Bus_ItemsChanged),
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
            m_remote = target.Area.Target != target;
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

            foreach (SheetAction action in m_target.Area.SheetActions(m_target))
            {
                foreach (SheetOption option in action.Options(m_target.Area.Wombat.Worker, m_target))
                {
                    if (action.Table.Id == ActionTable.k_Bake || action.Table.Id == ActionTable.k_Plant)
                    {
                        chips.Add(action.Table.Id == ActionTable.k_Bake ? Chip(option) : CropChip(option));
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
                    m_view.SetHeader(shelf.Bread == null ? m_tables.Text("sheet_shelf_empty_title") : m_tables.Format("sheet_shelf_title", shelf.Bread.Name), m_tables.Format("sheet_shelf_status", shelf.Stock, shelf.Capacity));
                    break;
                case OvenInteractable oven:
                    m_view.SetHeader(m_tables.Format("sheet_oven_title", IndexOf(oven) + 1), OvenStatus(oven));
                    break;
                case CounterInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_counter_title"), m_tables.Format("sheet_counter_status", ((CounterInteractable)m_target).Queue.Count));
                    break;
                case DigInteractable dig:
                    m_view.SetHeader(m_tables.Text("sheet_dig_title"), m_tables.Format("sheet_dig_status", dig.Grid.Cells.Count));
                    break;
                // 설계 39: 계단 자리 = 아래층 파기
                case StairInteractable stair:
                    m_view.SetHeader(m_tables.Text("sheet_stair_title"), m_tables.Format("sheet_stair_status", stair.Farm.Lower.Number));
                    break;
                // 설계 47: 수조 = 담긴 물고기 / 용량 · 도마 = 주문 수 · 닫힌 횟집 문 = 열기
                case TankInteractable tank:
                    m_view.SetHeader(m_tables.Text("sheet_tank_title"), m_tables.Format("sheet_tank_status", tank.Stock, tank.Capacity));
                    break;
                case CuttingBoardInteractable board:
                    m_view.SetHeader(m_tables.Text("sheet_board_title"), m_tables.Format("sheet_board_status", board.Orders.Count));
                    break;
                case ShopGateInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_gate_title"), m_tables.Text("sheet_gate_status"));
                    break;
                // 설계 53: 낚시터 끝 바위 · 좌대 자리
                case RockInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_rock_title"), m_tables.Text("sheet_rock_status"));
                    break;
                case SeatInteractable _:
                    m_view.SetHeader(m_tables.Text("sheet_seat_title"), m_tables.Text("sheet_seat_status"));
                    break;
                // 설계 35: 거름은 심을 때 하나씩 저절로 든다
                case PlotInteractable plot:
                    m_view.SetHeader(m_tables.Text("sheet_plot_title"), m_tables.Format("sheet_plot_status", m_shop.Wallet.Count(plot.Farm.Config.ManureItem)));
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

        // 굽기 칩: 해금된 빵(빈 오븐이고 재료가 있을 때만 고를 수 있고, 재고 배지 · 재고가 없으면 강조) · 다음 빵 해금(자물쇠 + 값, 설계 17). 둘 다 레시피(설계 25)
        private SheetChip Chip(SheetOption option)
        {
            BreadTable bread = m_tables.Get<BreadTable>(option.Option);
            bool enabled = option.State == SheetOptionState.Enabled;

            if (bread == m_shop.NextBread)
            {
                return new SheetChip
                {
                    SpritePath = bread.Sprite,
                    Label = m_tables.Format("chip_bread", bread.Name),
                    Cost = BigNumberFormatter.Format(option.Cost),
                    CostPoor = option.State == SheetOptionState.Poor,
                    Locked = true,
                    Enabled = enabled,
                    Ingredients = Recipe(bread),
                };
            }

            int stock = m_shop.StockOf(bread);

            return new SheetChip
            {
                SpritePath = bread.Sprite,
                Label = m_tables.Format("chip_bread", bread.Name),
                Count = m_tables.Format("chip_count", stock),
                CountEmpty = stock == 0,
                Seconds = m_tables.Format("chip_seconds", bread.BakeSeconds),
                Highlighted = enabled && stock == 0,
                Enabled = enabled,
                Ingredients = Recipe(bread),
            };
        }

        // 설계 35 작물 칩: 연 작물(창고 개수 배지, 창고가 비면 강조) · 다음 작물 해금(자물쇠 + 값)
        private SheetChip CropChip(SheetOption option)
        {
            CropTable crop = m_tables.Get<CropTable>(option.Option);
            ItemTable item = m_tables.Get<ItemTable>(crop.Item);

            if (crop == ((PlotInteractable)m_target).Farm.NextCrop)
            {
                return new SheetChip
                {
                    SpritePath = item.Icon,
                    Label = item.Name,
                    Cost = BigNumberFormatter.Format(option.Cost),
                    CostPoor = option.State == SheetOptionState.Poor,
                    Locked = true,
                    Enabled = option.State == SheetOptionState.Enabled,
                };
            }

            int stock = m_shop.Wallet.Count(crop.Item);

            return new SheetChip
            {
                SpritePath = item.Icon,
                Label = item.Name,
                Count = m_tables.Format("chip_count", stock),
                CountEmpty = stock == 0,
                Seconds = m_tables.Format("chip_seconds", crop.GrowSeconds),
                Highlighted = stock == 0,
                Enabled = option.State == SheetOptionState.Enabled,
            };
        }

        private List<ChipIngredient> Recipe(BreadTable bread)
        {
            List<ChipIngredient> recipe = new List<ChipIngredient>();

            foreach (IngredientData ingredient in bread.Ingredients)
            {
                recipe.Add(new ChipIngredient
                {
                    IconPath = m_tables.Get<ItemTable>(ingredient.Item).Icon,
                    Count = m_tables.Format("chip_ingredient", ingredient.Count),
                    Short = m_shop.Wallet.Count(ingredient.Item) < ingredient.Count,
                });
            }

            return recipe;
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
                case ActionTable.k_Dig:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_dig"),
                        Effect = m_tables.Text("row_dig_effect"),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                case ActionTable.k_DigFloor:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_dig_floor"),
                        Effect = m_tables.Format("row_dig_floor_effect", ((StairInteractable)m_target).Farm.Lower.Number),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                case ActionTable.k_BreakRock:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_break_rock"),
                        Effect = m_tables.Text("row_break_rock_effect"),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                case ActionTable.k_PlaceSeat:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_place_seat"),
                        Effect = m_tables.Format("row_place_seat_effect", ((SeatInteractable)m_target).Stretch.Pay.ToString("0", CultureInfo.InvariantCulture)),
                        Cost = BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                // 설계 47: 빵집 별이 모자라면 값 자리에 「★n 필요」
                case ActionTable.k_OpenShop:
                    return new SheetRow
                    {
                        Name = m_tables.Text("row_open_shop"),
                        Effect = m_tables.Text("row_open_shop_effect"),
                        Cost = option.State == SheetOptionState.Locked
                            ? m_tables.Format("row_star_needed", ((ShopGateInteractable)m_target).Shop.Config.OpenStar)
                            : BigNumberFormatter.Format(option.Cost),
                        State = RowState(option.State),
                    };
                default:
                    throw new InvalidOperationException($"행동 '{actionId}'의 시트 서식이 없다.");
            }
        }

        private string Cost(SheetOption option)
        {
            if (option.State == SheetOptionState.Locked)
            {
                return m_tables.Format("row_star_needed", m_shop.Wallet.Stars.StarFor(m_target.Area.Id, Stars.k_Upgrade, option.Level + 1));
            }

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

            if (m_target.Area.TryChoose(action, m_target, option))
            {
                m_view.Close();
            }
        }

        // 빈 자리·파기 자리·계단 자리는 사면 사물이 바뀌어 시트를 닫는다
        private void View_RowClicked(int index)
        {
            (string action, string option) = m_rows[index];

            if (!m_target.Area.TryChoose(action, m_target, option))
            {
                return;
            }

            m_view.FlashRow(index);

            if (m_target is DigInteractable || m_target is StairInteractable || m_target is ShopGateInteractable)
            {
                m_view.Close();
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        // 열린 시트의 곳(설계 27: 굴 파기 시트는 농장에서도 열린다)
        private WombatArea Area => m_target?.Area;

        private void Bus_ThingChanged(Events.ThingChanged e)
        {
            if (e.Thing.Area == Area)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_Upgraded(Events.Upgraded e)
        {
            if (e.Area == Area)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_LayoutChanged(Events.LayoutChanged e)
        {
            if (e.Area == Area)
            {
                RefreshIfOpen();
            }
        }

        private void Bus_CoinsChanged(Events.CoinsChanged e)
        {
            RefreshIfOpen();
        }

        private void Bus_ItemsChanged(Events.ItemsChanged e)
        {
            RefreshIfOpen();
        }

        // 걸어서 다른 사물로 가면(또는 배치가 바뀌어 대상이 사라지면) 닫는다
        private void Bus_TargetChanged(Events.TargetChanged e)
        {
            if (e.Area == Area && m_view.IsVisible && !m_remote && Area.Target != m_target)
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
