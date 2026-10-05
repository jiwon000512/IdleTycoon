using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 30 석상 축복 팝업: 광장 석상 앞 버튼(StatueOpened)으로 열고, 카드 · 빌기를 Core Blessing에 잇는다.
    // 빌면 카드가 돌다 멈춘다. 축복이 풀리거나 쉬는 시간이 끝나면 다시 그린다. 편집 모드에서는 닫는다
    public sealed class StatuePresenter : IDisposable
    {
        private readonly StatueView m_view;
        private readonly ZooState m_state;
        private readonly TableSet m_tables;
        private readonly IDisposable[] m_subscriptions;
        private readonly List<string> m_names = new List<string>();
        private StatueInteractable m_thing;

        private Blessing Blessing => m_state.Blessing;

        public StatuePresenter(StatueView view, ZooState state, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_state = state;
            m_tables = tables;
            m_view.CloseRequested += View_CloseRequested;
            m_view.PrayClicked += View_PrayClicked;
            m_view.SetLabels(tables.Text("statue_title"), tables.Text("statue_hint"), tables.Text("statue_pray"),
                seconds => tables.Format("statue_again", BigNumberFormatter.Clock(seconds)));

            foreach (BlessingTable blessing in tables.GetAll<BlessingTable>())
            {
                m_names.Add(tables.Text(blessing.Name));
            }

            m_view.SetMedals(tables.GetAll<BlessingTable>().Select(blessing => blessing.Icon).ToList());

            m_subscriptions = new[]
            {
                bus.Subscribe<Events.StatueOpened>(Bus_StatueOpened),
                bus.Subscribe<Events.BlessingChanged>(Bus_BlessingChanged),
            };
        }

        // 효과 글: format의 {0} = 퍼센트 숫자, {1} = 배수(상단 바 축복 알약도 쓴다)
        public static string EffectText(TableSet tables, BlessingTable blessing)
        {
            return tables.Format(blessing.Format, (blessing.Value * 100d).ToString("0.#", CultureInfo.InvariantCulture),
                (1d + blessing.Value).ToString("0.#", CultureInfo.InvariantCulture));
        }

        public void Dispose()
        {
            m_view.CloseRequested -= View_CloseRequested;
            m_view.PrayClicked -= View_PrayClicked;

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

        // 걸린 축복의 표 순서 번호(메달 줄 · 이름 목록과 같은 순서, 없으면 −1)
        private int Active()
        {
            return Blessing.Active == null ? -1 : m_tables.GetAll<BlessingTable>().ToList().FindIndex(blessing => blessing.Id == Blessing.Active.Id);
        }

        private StatueView.CardData Card()
        {
            BlessingTable active = Blessing.Active;

            return active == null ? null : new StatueView.CardData
            {
                IconPath = active.Icon,
                Name = m_tables.Text(active.Name),
                Effect = EffectText(m_tables, active),
                Remaining = () => Blessing.Remaining,
            };
        }

        private void Refresh()
        {
            m_view.Show(Card(), Active(), m_tables.Text("statue_none"), () => Blessing.Cooldown, Blessing.CanPray);
        }

        private void Bus_StatueOpened(Events.StatueOpened e)
        {
            m_thing = e.Thing;
            Refresh();
            m_view.Open();
        }

        private void Bus_BlessingChanged(Events.BlessingChanged e)
        {
            if (!m_view.IsOpen)
            {
                return;
            }

            if (e.Prayed)
            {
                m_view.PlayPray(Card(), Active(), m_names, () => Blessing.Cooldown);
            }
            else if (!m_view.Spinning)
            {
                Refresh();
            }
        }

        private void View_CloseRequested()
        {
            m_view.Close();
        }

        private void View_PrayClicked()
        {
            m_thing?.TryPray();
        }
    }
}
