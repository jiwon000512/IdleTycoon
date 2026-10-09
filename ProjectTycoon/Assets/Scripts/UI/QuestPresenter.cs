using System;
using UnityEngine;
using GameKit.Audio;
using GameKit.Events;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 54 · 55: 할 일 카드(TopBarView 위 가운데). 지금 걸음(아이콘 · 글 · 진행 n/m)을 보이고, 누르면 다 한 걸음은 받기(도장 · 코인 연출) · 아니면 가기(길잡이 · 메뉴 반짝).
    // 길잡이가 켜진 동안 카드는 「안내 중」. 사슬이 끝나면 카드를 숨긴다. 규칙은 Core QuestLog
    public sealed class QuestPresenter : IDisposable
    {
        private const string k_Count = "quest_count";
        private const string k_Guiding = "quest_guiding";
        private const string k_Done = "quest_done";

        private readonly QuestCard m_card;
        private readonly QuestLog m_quests;
        private readonly TableSet m_tables;
        private readonly IDisposable m_changed;
        private readonly IDisposable m_guided;

        public QuestPresenter(QuestCard card, QuestLog quests, EventBus bus, TableSet tables)
        {
            m_card = card;
            m_quests = quests;
            m_tables = tables;
            m_card.SetLabels(tables.Text(k_Guiding), tables.Text(k_Done));
            m_changed = bus.Subscribe<Events.QuestChanged>(_ => Refresh());
            m_guided = bus.Subscribe<Events.GuideChanged>(e => m_card.SetTracking(e.Guide.Active));
            m_card.Clicked += Card_Clicked;
            Refresh();
        }

        public void Dispose()
        {
            m_changed.Dispose();
            m_guided.Dispose();
            m_card.Clicked -= Card_Clicked;
        }

        private void Refresh()
        {
            QuestTable step = m_quests.Current;

            if (step == null)
            {
                m_card.Hide();
                return;
            }

            string reward = step.Coins > 0d ? BigNumberFormatter.Format(step.Coins) : null;
            m_card.Show(Resources.Load<Sprite>(step.Icon), m_tables.Text(step.Text), m_tables.Format(k_Count, m_quests.Count, step.Count), m_quests.IsDone, reward);
        }

        // 받기는 연출을 먼저 걸어 두고 받는다(다음 걸음은 카드가 뒤집힐 때 보인다)
        private void Card_Clicked()
        {
            if (m_quests.IsDone)
            {
                m_card.PlayReward();

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
