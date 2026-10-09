using System;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 54: 할 일 알약(TopBarView 위 가운데). 지금 걸음(아이콘 · 글 · 진행 n/m)을 보이고, 누르면 다 한 걸음은 받기 · 아니면 가기(길잡이 · 메뉴 반짝).
    // 사슬이 끝나면 알약을 숨긴다. 규칙은 Core QuestLog
    public sealed class QuestPresenter : IDisposable
    {
        private const string k_Go = "quest_go";
        private const string k_Claim = "quest_claim";
        private const string k_Count = "quest_count";

        private readonly TopBarView m_view;
        private readonly QuestLog m_quests;
        private readonly TableSet m_tables;
        private readonly IDisposable m_changed;

        public QuestPresenter(TopBarView view, QuestLog quests, EventBus bus, TableSet tables)
        {
            m_view = view;
            m_quests = quests;
            m_tables = tables;
            m_changed = bus.Subscribe<Events.QuestChanged>(_ => Refresh());
            m_view.QuestClicked += View_QuestClicked;
            Refresh();
        }

        public void Dispose()
        {
            m_changed.Dispose();
            m_view.QuestClicked -= View_QuestClicked;
        }

        private void Refresh()
        {
            QuestTable step = m_quests.Current;

            if (step == null)
            {
                m_view.HideQuest();
                return;
            }

            bool done = m_quests.IsDone;
            string reward = step.Coins > 0d ? BigNumberFormatter.Format(step.Coins) : null;
            m_view.ShowQuest(Resources.Load<Sprite>(step.Icon), m_tables.Text(step.Text), m_tables.Format(k_Count, m_quests.Count, step.Count), done, reward,
                m_tables.Text(done ? k_Claim : k_Go));
        }

        private void View_QuestClicked()
        {
            if (m_quests.IsDone)
            {
                if (m_quests.TryClaim())
                {
                    SoundManager.Instance.Play(SoundTable.k_Pay);
                }

                return;
            }

            m_quests.Go();
        }
    }
}
