using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 31 유물 수레 팝업: 광장 수레 앞 버튼(RelicCartOpened)으로 열고, 칸 · 격자 · 정보 줄 · 뽑기 · 후보를 Core Relics에 잇는다.
    // 칸이나 모은 유물을 누르면 정보 줄에 그 유물(끼우기/빼기). 뽑은 후보는 고를 때까지 남는다(다시 열어도 후보 화면). 편집 모드에서는 닫는다
    public sealed class RelicCartPresenter : IDisposable
    {
        private readonly RelicCartView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private RelicCartInteractable m_thing;
        private RelicTable m_selected;
        private int m_chosen;

        private Relics Relics => m_state.Relics;

        public RelicCartPresenter(RelicCartView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.CloseRequested += View_CloseRequested;
            m_view.DrawClicked += View_DrawClicked;
            m_view.InfoClicked += View_InfoClicked;
            m_view.SlotClicked += View_SlotClicked;
            m_view.CellClicked += View_CellClicked;
            m_view.OfferClicked += View_OfferClicked;
            m_view.SetLabels(tables.Text("relic_title"), tables.Text("relic_slots"), tables.Text("relic_empty"), tables.Text("relic_draw"),
                tables.Text("relic_pick"), tables.Text("relic_pick_hint"));
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.RelicCartOpened>(Bus_RelicCartOpened),
                bus.Subscribe<Events.RelicsChanged>(Bus_RelicsChanged),
                bus.Subscribe<Events.MerchantChanged>(Bus_MerchantChanged),
            };
        }

        // 효과 글: format의 {0} = 퍼센트 숫자(값 × 100), {1} = 값 그대로. stars = 1부터
        public static string EffectText(TableSet tables, RelicTable relic, int stars)
        {
            double value = Relics.ValueAt(relic, stars);
            return tables.Format(relic.Format, (value * 100d).ToString("0.#", CultureInfo.InvariantCulture), value.ToString("0.#", CultureInfo.InvariantCulture));
        }

        public void Dispose()
        {
            m_view.CloseRequested -= View_CloseRequested;
            m_view.DrawClicked -= View_DrawClicked;
            m_view.InfoClicked -= View_InfoClicked;
            m_view.SlotClicked -= View_SlotClicked;
            m_view.CellClicked -= View_CellClicked;
            m_view.OfferClicked -= View_OfferClicked;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        public void SetEditing(bool editing)
        {
            if (editing)
            {
                m_view.Close();
            }
        }

        private RelicCartView.CardData Card(RelicTable relic, int stars)
        {
            return new RelicCartView.CardData
            {
                IconPath = relic.Icon,
                Name = m_tables.Text(relic.Name),
                Effect = stars > 0 ? EffectText(m_tables, relic, stars) : string.Empty,
                Stars = stars,
                Owned = stars > 0,
                Selected = relic == m_selected,
            };
        }

        private void Refresh()
        {
            if (Relics.HasOffer)
            {
                m_view.ShowOffer(Offers(), false);
                return;
            }

            List<RelicCartView.CardData> slots = new List<RelicCartView.CardData>();

            foreach (RelicTable relic in Relics.Slots)
            {
                slots.Add(relic != null ? Card(relic, Relics.Stars(relic)) : null);
            }

            List<RelicCartView.CardData> cells = new List<RelicCartView.CardData>();
            int owned = 0;

            foreach (RelicTable relic in Relics.All)
            {
                cells.Add(Card(relic, Relics.Stars(relic)));
                owned += Relics.Stars(relic) > 0 ? 1 : 0;
            }

            bool equipped = m_selected != null && Relics.IsEquipped(m_selected);
            bool full = IndexOf(Relics.Slots, null) < 0;
            bool enough = m_state.Count(Relics.Item) >= Relics.Cost;
            string hint = Relics.Complete ? m_tables.Text("relic_complete")
                : m_selected != null && !equipped && full ? m_tables.Text("relic_full")
                : !enough ? m_tables.Text("relic_short")
                : m_tables.Text("relic_hint");

            m_view.ShowMain(slots, cells, m_tables.Format("relic_collection", owned, Relics.All.Count),
                m_selected != null ? Card(m_selected, Relics.Stars(m_selected)) : null,
                m_tables.Text(equipped ? "relic_unequip" : "relic_equip"), equipped || !full,
                hint, Relics.Cost, enough && !Relics.Complete);
        }

        // 후보 카드: 고른 뒤의 별과 효과, 새 유물이면 「새 유물」, 아니면 「★n로」
        private List<RelicCartView.CardData> Offers()
        {
            List<RelicCartView.CardData> offers = new List<RelicCartView.CardData>();

            foreach (RelicTable relic in Relics.Offer)
            {
                int stars = Relics.Stars(relic);
                RelicCartView.CardData card = Card(relic, stars + 1);
                card.Selected = false;
                card.Badge = stars == 0 ? m_tables.Text("relic_new") : m_tables.Format("relic_star_up", stars + 1);
                offers.Add(card);
            }

            return offers;
        }

        private void Bus_RelicCartOpened(Events.RelicCartOpened e)
        {
            m_thing = e.Thing;
            m_selected = null;
            Refresh();
            m_view.Open();
        }

        private void Bus_RelicsChanged(Events.RelicsChanged e)
        {
            if (!m_view.IsOpen)
            {
                return;
            }

            switch (e.Change)
            {
                case RelicChange.Drawn:
                    m_view.ShowOffer(Offers(), true);
                    break;
                case RelicChange.Chosen:
                    RelicTable chosen = e.Relic;
                    m_view.PlayChosen(m_chosen, () =>
                    {
                        m_selected = chosen;
                        Refresh();
                        int slot = IndexOf(Relics.Slots, chosen);
                        m_view.PulseCell(IndexOf(Relics.All, chosen));

                        if (slot >= 0)
                        {
                            m_view.PulseSlot(slot);
                        }
                    });
                    break;
                default:
                    Refresh();
                    break;
            }
        }

        // 행상이 좌판을 걷으면 팝업도 닫는다(뽑은 후보는 다음 방문까지 남는다)
        private void Bus_MerchantChanged(Events.MerchantChanged e)
        {
            if (!e.Merchant.IsOpen)
            {
                m_view.Close();
            }
        }

        private static int IndexOf(IReadOnlyList<RelicTable> list, RelicTable relic)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == relic)
                {
                    return i;
                }
            }

            return -1;
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_DrawClicked()
        {
            m_thing?.TryDraw();
        }

        private void View_OfferClicked(int index)
        {
            m_chosen = index;
            Relics.Choose(index);
        }

        private void View_SlotClicked(int index)
        {
            RelicTable relic = Relics.Slots[index];

            if (relic != null)
            {
                m_selected = relic;
                Refresh();
            }
        }

        private void View_CellClicked(int index)
        {
            RelicTable relic = Relics.All[index];

            if (Relics.Stars(relic) > 0)
            {
                m_selected = relic;
                Refresh();
            }
        }

        private void View_InfoClicked()
        {
            if (m_selected == null)
            {
                return;
            }

            if (Relics.IsEquipped(m_selected))
            {
                Relics.Unequip(m_selected);
            }
            else
            {
                Relics.TryEquip(m_selected);
            }
        }
    }
}
