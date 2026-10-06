using System;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 42: 곳이 바뀌면(AreaChanged) 곳 이름 띠. 게임을 시작할 때도 한 번. 농장은 층마다 「농장 n층」
    // 설계 49: 낚시터의 벽도 같은 띠로 알린다(웜뱃이 어디 서 있든 보인다): 대물이 나올 때 「대물!」 · 놓치면 「대물을 놓쳤다」 · 낚으면 「n단계!」(새 계열이 열리면 그 이름)
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
                bus.Subscribe<Events.StageRaised>(e => ShowIn(e.Fishing, StageText(e.Fishing))),
                bus.Subscribe<Events.BossSpawned>(e => ShowIn(e.Fishing, m_tables.Text("fishing_boss"))),
                bus.Subscribe<Events.BossEscaped>(e => ShowIn(e.Fishing, m_tables.Text("fishing_boss_missed"))),
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

        // 설계 49: 대물을 낚아 단계가 올랐다. 이 단계에 새 계열이 열렸으면 단계 대신 그 낚싯대 이름을 알린다
        private string StageText(FishingArea fishing)
        {
            RodTable unlocked = fishing.ShopRods.FirstOrDefault(rod => rod.UnlockStage == fishing.Stage);
            return unlocked != null ? m_tables.Format("fishing_unlock", m_tables.Text("rod_" + unlocked.Id)) : m_tables.Format("fishing_stage", fishing.Stage);
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
