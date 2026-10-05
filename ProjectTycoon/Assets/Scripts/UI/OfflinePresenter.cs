using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 43: 자리를 비운 동안 정산(OfflineSettled)이 오면 팝업. 코인 칸 + 늘어난(줄어든) 재료 칸, 월급을 냈으면 판매 · 월급 두 줄
    public sealed class OfflinePresenter : IDisposable
    {
        private const string k_CoinIcon = "coin";

        private readonly OfflineView m_view;
        private readonly TableSet m_tables;
        private readonly IDisposable m_subscription;

        public OfflinePresenter(OfflineView view, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_tables = tables;
            m_subscription = bus.Subscribe<Events.OfflineSettled>(e => Show(e.Report));
            m_view.Taken += m_view.Close;
        }

        public void Dispose()
        {
            m_subscription.Dispose();
            m_view.Taken -= m_view.Close;
        }

        private void Show(OfflineReport report)
        {
            List<InfoTile.Data> tiles = new List<InfoTile.Data>
            {
                new InfoTile.Data { Icon = k_CoinIcon, Value = "+" + BigNumberFormatter.Format(report.Coins), Caption = m_tables.Text("offline_coins") },
            };

            foreach (ItemTable item in m_tables.GetAll<ItemTable>())
            {
                if (report.Items.TryGetValue(item.Id, out int count))
                {
                    tiles.Add(new InfoTile.Data
                    {
                        Icon = item.Icon,
                        Value = (count > 0 ? "+" : "-") + Math.Abs(count),
                        Caption = item.Name,
                        State = count > 0 ? InfoTile.State.Normal : InfoTile.State.Bad,
                    });
                }
            }

            OfflineView.Pay pay = report.Wages > 0d ? new OfflineView.Pay
            {
                SalesName = m_tables.Text("offline_sales"),
                Sales = "+" + BigNumberFormatter.Format(report.Sales),
                WagesName = m_tables.Text("offline_wages"),
                Wages = "-" + BigNumberFormatter.Format(report.Wages),
            } : null;
            m_view.Show(m_tables.Text("offline_title"), Duration(report.Seconds), tiles, pay, m_tables.Text("offline_take"));
        }

        private string Duration(double seconds)
        {
            int minutes = (int)(seconds / 60d);
            return minutes >= 60 ? m_tables.Format("offline_hours", minutes / 60, minutes % 60) : m_tables.Format("offline_minutes", minutes);
        }
    }
}
