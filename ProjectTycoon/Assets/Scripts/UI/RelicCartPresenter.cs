using System;
using System.Collections.Generic;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 31 · 32 · 34 좌판 팝업: 광장 행상에게 말을 걸면(MerchantTalked) 인사가 반쯤 지난 뒤 열고, 뽑기 · 후보 · 고른 결과를 Core Relics에 잇는다.
    // 뽑은 후보는 고를 때까지 남는다(다시 열어도 후보). 「유물 보기」는 유물 화면(openRelics)을 연다. 행상이 떠나면 · 편집 모드에서는 닫는다
    public sealed class RelicCartPresenter : IDisposable
    {
        // 인사 말풍선이 떠 있는 시간 중 이만큼 지나 팝업이 뜬다
        private const float k_TalkShare = 0.5f;

        private readonly RelicCartView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly Action m_openRelics;
        private readonly IDisposable[] m_subscriptions;
        private MerchantInteractable m_thing;
        private int m_chosen;

        private Relics Relics => m_state.Relics;
        private bool CanDraw => m_state.Count(Relics.Item) >= Relics.Cost && !Relics.Complete;

        public RelicCartPresenter(RelicCartView view, ZooState state, EventBus bus, TableSet tables, Action openRelics)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_openRelics = openRelics;
            m_view.CloseRequested += View_CloseRequested;
            m_view.DrawClicked += View_DrawClicked;
            m_view.ViewClicked += View_ViewClicked;
            m_view.OfferClicked += View_OfferClicked;
            m_view.SetLabels(tables.Text("relic_cart_title"), tables.Text("relic_draw"), tables.Text("relic_again"), tables.Text("relic_view"));
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.MerchantTalked>(Bus_MerchantTalked),
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
            m_view.ViewClicked -= View_ViewClicked;
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

        private void ShowReady()
        {
            int have = m_state.Count(Relics.Item);
            string hint = Relics.Complete ? m_tables.Text("relic_complete")
                : have < Relics.Cost ? m_tables.Text("relic_short")
                : m_tables.Format("relic_have", have);
            m_view.ShowReady(m_tables.Format("relic_cart_lead", Relics.Cost), hint, Relics.Cost, CanDraw);
        }

        private void ShowOffer(bool animate)
        {
            m_view.ShowOffer(Offers(), m_tables.Text("relic_pick"), m_tables.Text("relic_pick_hint"), animate);
        }

        // 후보 카드: 고른 뒤의 별과 효과, 새 유물이면 「새 유물」, 아니면 「★n로」
        private List<RelicCard.Data> Offers()
        {
            List<RelicCard.Data> offers = new List<RelicCard.Data>();

            foreach (RelicTable relic in Relics.Offer)
            {
                int stars = Relics.Stars(relic) + 1;
                offers.Add(new RelicCard.Data
                {
                    IconPath = relic.Icon,
                    Name = m_tables.Text(relic.Name),
                    Effect = EffectText(m_tables, relic, stars),
                    Stars = stars,
                    Badge = stars == 1 ? m_tables.Text("relic_new") : m_tables.Format("relic_star_up", stars),
                });
            }

            return offers;
        }

        private void Bus_MerchantTalked(Events.MerchantTalked e)
        {
            m_thing = e.Thing;

            if (Relics.HasOffer)
            {
                ShowOffer(false);
            }
            else
            {
                ShowReady();
            }

            m_view.Open((float)e.Seconds * k_TalkShare);
        }

        // 고른 결과: 위 글 = 새 유물 / 별 오름, 아래 글 = 칸에 끼움 / 이미 끼운 유물 / 칸이 가득
        private void Bus_RelicsChanged(Events.RelicsChanged e)
        {
            if (!m_view.IsOpen)
            {
                return;
            }

            if (e.Change == RelicChange.Drawn)
            {
                ShowOffer(true);
            }
            else if (e.Change == RelicChange.Chosen)
            {
                int stars = Relics.Stars(e.Relic);
                string label = stars == 1 ? m_tables.Text("relic_got_new") : m_tables.Format("relic_got_star", stars);
                string hint = !Relics.IsEquipped(e.Relic) ? m_tables.Text("relic_got_full")
                    : stars == 1 ? m_tables.Text("relic_got_slot")
                    : m_tables.Text("relic_got_equipped");
                m_view.ShowChosen(m_chosen, stars, label, hint, Relics.Cost, CanDraw);
            }
        }

        // 행상이 좌판을 접으면 팝업도 닫는다(뽑은 후보는 다음 방문까지 남는다)
        private void Bus_MerchantChanged(Events.MerchantChanged e)
        {
            if (!e.Merchant.IsOpen)
            {
                m_view.Close();
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_DrawClicked()
        {
            m_thing?.TryDraw();
        }

        private void View_ViewClicked()
        {
            m_view.Close();
            m_openRelics();
        }

        private void View_OfferClicked(int index)
        {
            m_chosen = index;
            Relics.Choose(index);
        }
    }
}
