using System;
using System.Globalization;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 31 · 32 · 34 → 설계 36 반짝돌 뽑기: 광장 행상에게 말을 걸면(MerchantTalked) 인사가 반쯤 지난 뒤 뽑기 메인을 열고, 뽑으면(RelicsChanged Drawn)
    // 결과 팝업이 메인 위에 뜬다. 결과의 「한 번 더」는 그 자리에서 다시 뽑고, 「확인」은 결과만 닫고, 「유물 보기」는 둘 다 닫고 유물 화면(openRelics)을 연다.
    // 행상이 떠나면 · 편집 모드에서는 둘 다 닫는다
    public sealed class RelicCartPresenter : IDisposable
    {
        // 인사 말풍선이 떠 있는 시간 중 이만큼 지나 팝업이 뜬다
        private const float k_TalkShare = 0.5f;

        private readonly RelicCartView m_view;
        private readonly RelicDrawResultView m_result;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly Action m_openRelics;
        private readonly IDisposable[] m_subscriptions;
        private MerchantInteractable m_thing;

        private Relics Relics => m_state.Relics;
        private bool CanDraw => m_state.Count(Relics.Item) >= Relics.Cost && !Relics.Complete;

        public RelicCartPresenter(RelicCartView view, RelicDrawResultView result, ZooState state, EventBus bus, TableSet tables, Action openRelics)
        {
            m_view = view;
            m_result = result;
            m_state = state;
            m_tables = tables;
            m_openRelics = openRelics;
            m_view.CloseRequested += View_CloseRequested;
            m_view.DrawClicked += View_DrawClicked;
            m_result.CloseRequested += Result_CloseRequested;
            m_result.AgainClicked += View_DrawClicked;
            m_result.ViewClicked += Result_ViewClicked;
            m_view.SetLabels(tables.Text("relic_cart_title"), tables.Text("relic_draw"));
            m_result.SetLabels(tables.Text("relic_view"), tables.Text("relic_again"), tables.Text("relic_ok"));
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
            m_result.CloseRequested -= Result_CloseRequested;
            m_result.AgainClicked -= View_DrawClicked;
            m_result.ViewClicked -= Result_ViewClicked;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        public void SetEditing(bool editing)
        {
            if (editing)
            {
                CloseBoth();
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

        private void CloseBoth()
        {
            m_result.Close();
            m_view.Close();
        }

        private void Bus_MerchantTalked(Events.MerchantTalked e)
        {
            m_thing = e.Thing;
            ShowReady();
            m_view.Open((float)e.Seconds * k_TalkShare);
        }

        // 뽑은 유물의 앞면: 이름표 = 새 유물 / ★n로, 아래 글 = 칸에 끼움 / 이미 끼운 유물 / 칸이 가득. 메인의 반짝돌 글도 다시 쓴다
        private void Bus_RelicsChanged(Events.RelicsChanged e)
        {
            if (e.Change != RelicChange.Drawn || !m_view.IsOpen)
            {
                return;
            }

            RelicTable relic = e.Relic;
            int stars = Relics.Stars(relic);
            RelicCard.Data face = new RelicCard.Data
            {
                IconPath = relic.Icon,
                Name = m_tables.Text(relic.Name),
                Effect = EffectText(m_tables, relic, stars),
                Stars = stars,
                Selected = true,
                Badge = stars == 1 ? m_tables.Text("relic_new") : m_tables.Format("relic_star_up", stars),
            };
            string hint = !Relics.IsEquipped(relic) ? m_tables.Text("relic_got_full")
                : stars == 1 ? m_tables.Text("relic_got_slot")
                : m_tables.Text("relic_got_equipped");
            ShowReady();
            m_result.Show(face, stars, hint, Relics.Cost, CanDraw);
        }

        // 행상이 떠나면 둘 다 닫는다
        private void Bus_MerchantChanged(Events.MerchantChanged e)
        {
            if (!e.Merchant.IsOpen)
            {
                CloseBoth();
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

        private void Result_CloseRequested()
        {
            m_result.Close();
        }

        private void Result_ViewClicked()
        {
            CloseBoth();
            m_openRelics();
        }
    }
}
