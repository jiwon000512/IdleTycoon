using System;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 42: 곳이 바뀌면(AreaChanged) 곳 이름 띠. 게임을 시작할 때도 한 번. 농장은 층마다 「농장 n층」
    public sealed class LocationPresenter : IDisposable
    {
        private readonly LocationView m_view;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;

        public LocationPresenter(LocationView view, Mall mall, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_tables = tables;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.AreaChanged>(e => Show(e.Active)),
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

        private void Show(WombatArea area)
        {
            m_view.Show(area is FarmArea farm ? m_tables.Format("loc_farm", farm.Number) : m_tables.Text("loc_" + area.Id));
        }
    }
}
