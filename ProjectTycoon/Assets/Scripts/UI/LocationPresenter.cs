using System;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 42: 곳이 바뀌면(AreaChanged) 곳 이름 팻말. 게임을 시작할 때도 한 번. 농장은 층마다 「농장 n층」
    // 낚시 폴리싱: 낚시터에 있으면 같은 팻말로 「대물이 온다!」 · 「n단계!」 · 「대물을 놓쳤다」
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
                bus.Subscribe<Events.WaveStarted>(e => ShowIn(e.Fishing, e.Boss ? m_tables.Text("fishing_boss_coming") : null)),
                bus.Subscribe<Events.BossResolved>(e => ShowIn(e.Fishing, e.Caught ? m_tables.Format("fishing_stage", e.Fishing.Stage) : m_tables.Text("fishing_boss_missed"))),
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
