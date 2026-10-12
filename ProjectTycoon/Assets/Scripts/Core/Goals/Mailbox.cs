using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 59(사용자 2026-10-12 「퀘스트로만 목표를 주지 말고 이야기로」): 우편함. 퀘스트 걸음이 시작 · 끝나거나 곳에 들어갈 때 표(LetterTable when)에 맞는 편지가 온다(한 통은 한 번만).
    // 읽으면 동봉(코인 · 재료)을 주고, 읽은 편지는 모아 두고 다시 읽는다. 화면은 LetterArrived를 받아 편지 카드를 띄운다
    public sealed class Mailbox
    {
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly EventBus m_bus;
        private readonly List<LetterTable> m_letters = new List<LetterTable>();
        private readonly HashSet<string> m_read = new HashSet<string>();

        // 온 편지(온 순서)
        public IReadOnlyList<LetterTable> Letters => m_letters;
        public IEnumerable<LetterTable> Unread => m_letters.Where(letter => !m_read.Contains(letter.Id));
        public int UnreadCount => Unread.Count();
        public int ReadCount => m_read.Count;

        public Mailbox(Mall mall, TableSet tables, EventBus bus)
        {
            m_mall = mall;
            m_tables = tables;
            m_bus = bus;
            bus.Subscribe<Events.QuestChanged>(_ => Check());
            bus.Subscribe<Events.AreaChanged>(e => Arrive(LetterTable.k_WhenArea + ":" + e.Active.Id));
            Check();
        }

        public bool IsRead(LetterTable letter)
        {
            return m_read.Contains(letter.Id);
        }

        // 읽기: 동봉을 받고 읽은 편지로. 온 적 없거나 이미 읽었으면 false
        public bool TryRead(LetterTable letter)
        {
            if (!m_letters.Contains(letter) || !m_read.Add(letter.Id))
            {
                return false;
            }

            if (letter.Coins > 0d || letter.Item != null && letter.ItemCount > 0)
            {
                Dictionary<string, int> items = new Dictionary<string, int>();

                if (letter.Item != null && letter.ItemCount > 0)
                {
                    items[letter.Item] = letter.ItemCount;
                }

                Rewards.Grant(m_mall.Bakery.Wallet, m_bus, letter.Coins, items);
            }

            m_bus.Publish(new Events.LetterRead(letter));
            return true;
        }

        // 지금 걸음의 시작 · 끝 편지
        private void Check()
        {
            QuestTable step = m_mall.Quests.Current;

            if (step == null)
            {
                return;
            }

            Arrive(LetterTable.k_WhenStart + ":" + step.Id);

            if (m_mall.Quests.IsDone)
            {
                Arrive(LetterTable.k_WhenDone + ":" + step.Id);
            }
        }

        private void Arrive(string when)
        {
            foreach (LetterTable letter in m_tables.GetAll<LetterTable>())
            {
                if (letter.When == when && !m_letters.Contains(letter))
                {
                    m_letters.Add(letter);
                    m_bus.Publish(new Events.LetterArrived(letter));
                }
            }
        }

        // 저장에서: 온 편지(순서대로) · 읽은 편지. 표에 없는 id는 버린다. 그 뒤 지금 걸음 편지를 다시 본다(옛 저장은 비어 있다)
        internal void Restore(IEnumerable<string> arrived, IEnumerable<string> read)
        {
            m_letters.Clear();
            m_read.Clear();

            foreach (string id in arrived)
            {
                LetterTable letter = m_tables.GetAll<LetterTable>().FirstOrDefault(row => row.Id == id);

                if (letter != null && !m_letters.Contains(letter))
                {
                    m_letters.Add(letter);
                }
            }

            m_read.UnionWith(read.Where(id => m_letters.Any(letter => letter.Id == id)));
            Check();
        }
    }
}
