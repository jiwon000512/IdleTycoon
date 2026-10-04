using System.Collections.Generic;
using GameKit.Events;

namespace ZooTycoon.Core
{
    // 설계 21 → 설계 38: 가게 공통 월급날. 모든 곳(빵집 · 농장)의 점원이 같은 날 한 번에 받고, 지갑이 모자라면 그 점원만 해고된다.
    // 첫 점원을 들이면 그때부터 센다(점원이 아무도 없던 동안은 세지 않은 셈). Mall이 하나 두고 곳보다 먼저 돌린다
    public sealed class Payroll
    {
        private readonly IReadOnlyList<WombatArea> m_areas;
        private readonly ClerkConfigTable m_config;
        private readonly ZooState m_wallet;
        private readonly EventBus m_bus;
        private double m_untilPayday;

        // 설계 43: 다음 월급날까지 남은 초(저장 · 오프라인 정산)
        public double UntilPayday
        {
            get => m_untilPayday;
            internal set => m_untilPayday = value;
        }

        internal double Period => m_config.WagePeriodSeconds;

        // 다음 월급날까지 찬 정도(0~1)
        public double Progress => 1d - m_untilPayday / m_config.WagePeriodSeconds;

        // 지금 코인으로는 다음 월급날에 모두의 월급을 낼 수 없다(누군가 해고된다)
        public bool Short
        {
            get
            {
                double wages = 0d;

                foreach (Clerk clerk in Clerks)
                {
                    wages += clerk.Wage;
                }

                return m_wallet.Coins < wages;
            }
        }

        // 모든 곳의 점원(곳 순서 → 고용 순서)
        public IEnumerable<Clerk> Clerks
        {
            get
            {
                foreach (WombatArea area in m_areas)
                {
                    foreach (Clerk clerk in area.Clerks)
                    {
                        yield return clerk;
                    }
                }
            }
        }

        public Payroll(IReadOnlyList<WombatArea> areas, ClerkConfigTable config, ZooState wallet, EventBus bus)
        {
            m_areas = areas;
            m_config = config;
            m_wallet = wallet;
            m_bus = bus;
            m_untilPayday = config.WagePeriodSeconds;
            bus.Subscribe<Events.ClerkHired>(Bus_ClerkHired);
        }

        public void Tick(double dt)
        {
            m_untilPayday -= dt;

            if (m_untilPayday > 0d)
            {
                return;
            }

            m_untilPayday += m_config.WagePeriodSeconds;
            bool paid = false;

            foreach (WombatArea area in m_areas)
            {
                for (int i = area.Clerks.Count - 1; i >= 0; i--)
                {
                    Clerk clerk = area.Clerks[i];

                    if (m_wallet.TrySpendCoins(clerk.Wage))
                    {
                        paid = true;
                        m_bus.Publish(new Events.ClerkPaid(clerk, clerk.Wage));
                    }
                    else
                    {
                        area.Fire(clerk, FireReason.Unpaid);
                    }
                }
            }

            if (paid)
            {
                m_bus.Publish(new Events.Payday());
            }
        }

        private void Bus_ClerkHired(Events.ClerkHired e)
        {
            using (IEnumerator<Clerk> clerks = Clerks.GetEnumerator())
            {
                // 이 점원 하나뿐이면 첫 점원
                if (clerks.MoveNext() && clerks.Current == e.Clerk && !clerks.MoveNext())
                {
                    m_untilPayday = m_config.WagePeriodSeconds;
                }
            }
        }
    }
}
