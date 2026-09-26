using System;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 22: 대화 하나(DialogueTable 행). 줄을 lineSeconds 간격으로 DialogueLine 사건으로 내고 마지막 줄 시간이 지나면 DialogueEnded.
    // 말하는 이(점원·웜뱃)는 대화를 건 곳이 넘긴다. 조작을 막지 않으므로 화면은 사건만 받아 말풍선을 띄운다
    public sealed class Dialogue
    {
        private readonly DialogueTable m_table;
        private readonly IRandom m_random;
        private readonly EventBus m_bus;
        private double m_untilNext;
        private int m_next;

        public Clerk Clerk { get; }
        public Wombat Wombat { get; }
        public bool Done { get; private set; }

        public Dialogue(DialogueTable table, IRandom random, EventBus bus, Clerk clerk, Wombat wombat)
        {
            m_table = table;
            m_random = random;
            m_bus = bus;
            Clerk = clerk;
            Wombat = wombat;
        }

        // 첫 틱에 첫 줄
        public void Tick(double dt)
        {
            if (Done)
            {
                return;
            }

            m_untilNext -= dt;

            if (m_untilNext > 0d)
            {
                return;
            }

            if (m_next >= m_table.Lines.Count)
            {
                Done = true;
                m_bus.Publish(new Events.DialogueEnded(this));
                return;
            }

            DialogueLineData line = m_table.Lines[m_next++];
            string text = line.Texts[Math.Min(line.Texts.Count - 1, (int)(m_random.NextDouble() * line.Texts.Count))];
            object speaker = line.Speaker == DialogueSpeaker.Wombat ? (object)Wombat : Clerk;

            if (line.Bubble != null)
            {
                (line.Speaker == DialogueSpeaker.Wombat ? Wombat.Bubble : Clerk.Bubble).Show(line.Bubble);
            }

            m_untilNext = m_table.LineSeconds;
            m_bus.Publish(new Events.DialogueLine(this, line.Speaker, speaker, text, m_table.LineSeconds));
        }
    }
}
