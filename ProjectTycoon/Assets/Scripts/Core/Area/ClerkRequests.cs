using System.Collections.Generic;
using System.Linq;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 59 ③ 점원의 부탁(사용자 2026-10-12): 월급을 requestAfterPaydays번 받은 점원이 제 사물이 아직 1단계면 편지로 부탁한다(「한 단계 올려 주면 더 열심히」).
    // requestSeconds 안에 그 사물을 올리면 일머리 +requestSkill(♥), 넘기면 딴짓이 는다(Mood ×requestSulk, 「!!」). 점원마다 한 번에 하나. 편지는 Mailbox.Post로
    public sealed class ClerkRequests
    {
        public const string k_Upgrade = "r_upgrade";

        public sealed class Request
        {
            public Clerk Clerk;
            public string Kind;
            public double Left;
            public int LevelAt;
        }

        private readonly Mall m_mall;
        private readonly TableSet m_tables;
        private readonly ClerkConfigTable m_config;
        private readonly Dictionary<Clerk, int> m_paydays = new Dictionary<Clerk, int>();
        private readonly List<Request> m_pending = new List<Request>();

        public IReadOnlyList<Request> Pending => m_pending;

        public ClerkRequests(Mall mall, TableSet tables, EventBus bus)
        {
            m_mall = mall;
            m_tables = tables;
            m_config = tables.Get<ClerkConfigTable>(ClerkConfigTable.k_Main);
            bus.Subscribe<Events.ClerkPaid>(Bus_ClerkPaid);
        }

        public Request Of(Clerk clerk)
        {
            return m_pending.FirstOrDefault(request => request.Clerk == clerk);
        }

        // 월급날: requestAfterPaydays번째 월급부터, 1단계 사물에 붙은 점원이 부탁 하나
        private void Bus_ClerkPaid(Events.ClerkPaid e)
        {
            m_paydays.TryGetValue(e.Clerk, out int paid);
            m_paydays[e.Clerk] = ++paid;

            if (paid < m_config.RequestAfterPaydays || Of(e.Clerk) != null || e.Clerk.Thing.Table.Upgrade == null || e.Clerk.Thing.UpgradeLevel > 0)
            {
                return;
            }

            LetterTable row = m_tables.GetAll<LetterTable>().FirstOrDefault(r => r.Id == k_Upgrade);

            if (row == null)
            {
                return;
            }

            m_pending.Add(new Request { Clerk = e.Clerk, Kind = k_Upgrade, Left = m_config.RequestSeconds, LevelAt = e.Clerk.Thing.UpgradeLevel });
            m_mall.Mailbox.Post(row, e.Clerk);
        }

        public void Tick(double dt)
        {
            for (int i = m_pending.Count - 1; i >= 0; i--)
            {
                Request request = m_pending[i];
                Clerk clerk = request.Clerk;

                if (!clerk.Home.Clerks.Contains(clerk))
                {
                    m_pending.RemoveAt(i);
                    continue;
                }

                if (clerk.Thing.UpgradeLevel > request.LevelAt)
                {
                    m_pending.RemoveAt(i);
                    clerk.Grow(m_config.RequestSkill);
                    clerk.Bubble.Show(BubbleTable.k_Heart);
                    continue;
                }

                request.Left -= dt;

                if (request.Left <= 0d)
                {
                    m_pending.RemoveAt(i);
                    clerk.Sulk(m_config.RequestSulk);
                    clerk.Bubble.Show(BubbleTable.k_Angry);
                }
            }
        }

        // 저장에서: 곳 id · 점원 차례로 찾는다(없으면 버림). 월급 센 수는 다시 센다
        internal void Restore(IEnumerable<RequestSave> requests)
        {
            m_pending.Clear();

            foreach (RequestSave r in requests)
            {
                IReadOnlyList<Clerk> clerks = m_mall.Areas.FirstOrDefault(a => a.Id == r.Area)?.Clerks;
                Clerk clerk = clerks != null && r.Clerk >= 0 && r.Clerk < clerks.Count ? clerks[r.Clerk] : null;

                if (clerk != null && Of(clerk) == null)
                {
                    m_pending.Add(new Request { Clerk = clerk, Kind = r.Kind, Left = r.Left, LevelAt = r.LevelAt });
                }
            }
        }
    }
}
