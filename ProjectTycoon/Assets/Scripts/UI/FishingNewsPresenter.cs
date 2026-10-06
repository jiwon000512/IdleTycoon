using System;
using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 50 낚시 소식: 대물을 낚으면 드러나는 연출(bossShowSeconds) 뒤에 뜬다. 웜뱃이 다른 곳에 있을 때 낚였으면 낚시터에 들어올 때 뜬다(저장을 넘어서도).
    // 확인하면 다음 단계가 시작된다(FishingArea.ConfirmLanded)
    public sealed class FishingNewsPresenter : IDisposable
    {
        private readonly FishingNewsView m_view;
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        // 소식을 띄운 낚시터
        private FishingArea m_fishing;

        public FishingNewsPresenter(FishingNewsView view, Mall mall, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_mall = mall;
            m_tables = tables;
            m_view.Confirmed += View_Confirmed;
            m_subscriptions = new[]
            {
                bus.Subscribe<Events.BossLanded>(e => Show(e.Fishing, (float)e.Fishing.Config.BossShowSeconds)),
                bus.Subscribe<Events.AreaChanged>(e => Show(e.Active as FishingArea, 0f)),
            };
            Show(mall.Active as FishingArea, 0f);
        }

        public void Dispose()
        {
            m_view.Confirmed -= View_Confirmed;

            foreach (IDisposable subscription in m_subscriptions)
            {
                subscription.Dispose();
            }
        }

        // 웜뱃이 있는 낚시터에 확인을 기다리는 대물이 있으면 소식을 띄운다. 아니면 뜨려던 소식을 거둔다(드러나는 동안 나갔으면 들어올 때 뜬다)
        private void Show(FishingArea fishing, float delay)
        {
            if (fishing == null || fishing.Landed == null || fishing != m_mall.Active)
            {
                m_view.Cancel();
                return;
            }

            m_fishing = fishing;
            BossTable boss = fishing.Landed;
            string key = "boss_" + boss.Id;
            m_view.Show(new FishingNewsView.Data
            {
                Masthead = m_tables.Text("news_title"),
                Title = m_tables.Text(key + "_title"),
                Picture = boss.Sprite,
                Line = m_tables.Text(key + "_line"),
                BoxLabel = m_tables.Text("news_gain"),
                Rewards = RewardTiles(fishing, boss),
                Extra = NextText(fishing),
                Button = m_tables.Text("fishing_news_next"),
            });
            m_view.Open(delay);
        }

        // 이미 받은 것: 재료(× 단계 보람) · 반짝돌
        private List<InfoTile.Data> RewardTiles(FishingArea fishing, BossTable boss)
        {
            List<InfoTile.Data> tiles = new List<InfoTile.Data>();

            if (boss.Item != null)
            {
                tiles.Add(new InfoTile.Data { Icon = m_tables.Get<ItemTable>(boss.Item).Icon, Value = m_tables.Format("eval_times", boss.Count * fishing.Yield) });
            }

            if (boss.Gems > 0)
            {
                tiles.Add(new InfoTile.Data { Icon = m_tables.Get<ItemTable>(fishing.Config.GemItem).Icon, Value = m_tables.Format("eval_times", boss.Gems) });
            }

            return tiles;
        }

        // 다음 단계에 달라지는 것: 새로 열리는 낚싯대 · 한 마리 재료(달라지는 것이 없으면 null)
        private string NextText(FishingArea fishing)
        {
            int next = fishing.Stage + 1;
            RodTable unlocked = m_tables.GetAll<RodTable>().FirstOrDefault(rod => rod.Tier == 1 && rod.UnlockStage == next);
            string[] lines =
            {
                unlocked != null ? m_tables.Format("fishing_unlock", m_tables.Text("rod_" + unlocked.Id)) : null,
                fishing.YieldAt(next) > fishing.Yield ? m_tables.Format("fishing_news_yield", fishing.YieldAt(next)) : null,
            };
            return string.Join("\n", lines.Where(line => line != null));
        }

        private void View_Confirmed()
        {
            m_fishing.ConfirmLanded();
            m_view.Close();
        }
    }
}
