using System;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 42: 곳이 바뀌면(AreaChanged) 곳 이름 띠. 게임을 시작할 때도 한 번. 농장은 층마다 「농장 n층」
    // 설계 52: 우물 낚시의 끊김 · 놓침도 같은 띠로 알린다(웜뱃이 어디 서 있든 보인다)
    public sealed class LocationPresenter : IDisposable
    {
        private readonly LocationView m_view;
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;

        public LocationPresenter(LocationView view, Mall mall, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_mall = mall;
            m_tables = tables;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.AreaChanged>(e => Show(e.Active)),
                bus.Subscribe<Events.FishingEnded>(e => ShowIn(e.Fishing, EndText(e.Reason))),
            };
            Show(mall.Active);
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        // 끊기면 「줄이 끊어졌다!」 · 틈을 넘겨 놓치면 「놓쳤다…」, 거둔 것은 조용히
        private string EndText(FishingEnd reason)
        {
            return reason == FishingEnd.Snapped ? m_tables.Text("fishing_snapped") : reason == FishingEnd.Missed ? m_tables.Text("fishing_missed") : null;
        }

        private void ShowIn(WombatArea area, string text)
        {
            if (text != null && area == m_mall.Active)
            {
                m_view.Show(text);
            }
        }

        private void Show(WombatArea area)
        {
            m_view.Show(area is FarmArea farm ? m_tables.Format("loc_farm", farm.Number) : m_tables.Text("loc_" + area.Id));
        }
    }
}
