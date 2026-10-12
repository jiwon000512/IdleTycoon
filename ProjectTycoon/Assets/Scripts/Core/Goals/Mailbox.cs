using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 59: 우편함의 편지 한 통(표 행 + 보낸 점원). 점원 부탁 편지(ⓒ)는 보낸 이가 그 점원이라 글에 이름 · 사물을 끼운다
    public sealed class Letter
    {
        public LetterTable Row { get; }
        public Clerk Clerk { get; }
        public string Id => Row.Id;
        // 저장 키: 행 id, 점원 편지는 행id@곳id:점원 차례(이름은 겹칠 수 있다)
        public string Key => Clerk == null ? Id : Id + "@" + Clerk.Home.Id + ":" + Clerk.Index;

        public Letter(LetterTable row, Clerk clerk = null)
        {
            Row = row;
            Clerk = clerk;
        }
    }

    // 설계 59(사용자 2026-10-12 「퀘스트로만 목표를 주지 말고 이야기로」): 우편함. 퀘스트 걸음이 시작 · 끝나거나 곳에 들어갈 때 표(LetterTable when)에 맞는 편지가 온다(한 통은 한 번만).
    // 읽으면 동봉(코인 · 재료)을 주고, 읽은 편지는 모아 두고 다시 읽는다. 화면은 LetterArrived를 받아 편지 카드를 띄운다. 점원 부탁 편지는 ClerkRequests가 Post로 넣는다
    public sealed class Mailbox
    {
        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly EventBus m_bus;
        private readonly List<Letter> m_letters = new List<Letter>();
        private readonly HashSet<Letter> m_read = new HashSet<Letter>();

        // 온 편지(온 순서)
        public IReadOnlyList<Letter> Letters => m_letters;
        public IEnumerable<Letter> Unread => m_letters.Where(letter => !m_read.Contains(letter));
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

        public bool IsRead(Letter letter)
        {
            return m_read.Contains(letter);
        }

        // 읽기: 동봉을 받고 읽은 편지로. 온 적 없거나 이미 읽었으면 false
        public bool TryRead(Letter letter)
        {
            if (!m_letters.Contains(letter) || !m_read.Add(letter))
            {
                return false;
            }

            LetterTable row = letter.Row;

            if (row.Coins > 0d || row.Item != null && row.ItemCount > 0)
            {
                Dictionary<string, int> items = new Dictionary<string, int>();

                if (row.Item != null && row.ItemCount > 0)
                {
                    items[row.Item] = row.ItemCount;
                }

                Rewards.Grant(m_mall.Bakery.Wallet, m_bus, row.Coins, items);
            }

            m_bus.Publish(new Events.LetterRead(letter));
            return true;
        }

        // 점원 편지를 넣는다. 같은 점원의 같은 편지(앞선 부탁)는 뺀다(리뷰: 저장 키가 같아 불러올 때 하나만 남았다. 우편함도 끝없이 늘지 않는다)
        internal void Post(LetterTable row, Clerk clerk)
        {
            m_letters.RemoveAll(letter => letter.Row == row && letter.Clerk == clerk);
            m_read.RemoveWhere(letter => letter.Row == row && letter.Clerk == clerk);
            Add(new Letter(row, clerk));
        }

        private void Add(Letter letter)
        {
            m_letters.Add(letter);
            m_bus.Publish(new Events.LetterArrived(letter));
        }

        // 지금 걸음의 시작 · 끝 편지
        private void Check()
        {
            QuestTable step = m_mall.Quests.Current;

            // 숨는 걸음은 보일 때 편지가 온다
            if (step == null || m_mall.Quests.Hidden)
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
            foreach (LetterTable row in m_tables.GetAll<LetterTable>())
            {
                if (row.When == when && m_letters.All(letter => letter.Row != row))
                {
                    Add(new Letter(row));
                }
            }
        }

        // 저장에서: 온 편지(순서대로, Letter.Key) · 읽은 편지. 표에 없는 행 · 없는 점원은 버린다. 그 뒤 지금 걸음 편지를 다시 본다(옛 저장은 비어 있다)
        internal void Restore(IEnumerable<string> arrived, IEnumerable<string> read)
        {
            m_letters.Clear();
            m_read.Clear();
            HashSet<string> readKeys = new HashSet<string>(read);

            foreach (string key in arrived)
            {
                string[] parts = key.Split('@');
                LetterTable row = m_tables.GetAll<LetterTable>().FirstOrDefault(r => r.Id == parts[0]);
                Clerk clerk = parts.Length > 1 ? FindClerk(parts[1]) : null;

                if (row == null || parts.Length > 1 && clerk == null || m_letters.Any(letter => letter.Key == key))
                {
                    continue;
                }

                Letter letter = new Letter(row, clerk);
                m_letters.Add(letter);

                if (readKeys.Contains(key))
                {
                    m_read.Add(letter);
                }
            }

            Check();
        }

        private Clerk FindClerk(string key)
        {
            int colon = key.IndexOf(':');
            IReadOnlyList<Clerk> clerks = colon < 0 ? null : m_mall.Areas.FirstOrDefault(a => a.Id == key.Substring(0, colon))?.Clerks;
            return clerks != null && int.TryParse(key.Substring(colon + 1), out int index) && index >= 0 && index < clerks.Count ? clerks[index] : null;
        }
    }
}
