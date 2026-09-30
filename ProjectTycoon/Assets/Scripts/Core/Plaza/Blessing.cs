using System;
using System.Collections.Generic;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 30: 석상 축복(가게 전부에 걸리는 시간제 효과, 플레이어 상태라 ZooState가 든다). 무료로 빌면 축복 하나를 비중으로 뽑아 seconds 동안 건다.
    // 늘 하나만 걸린다(새 축복이 덮는다). 빈 뒤 blessingCooldown초 동안은 못 빈다. 걸리거나 풀리거나 쉬는 시간이 끝나면 BlessingChanged
    public sealed class Blessing
    {
        private readonly EventBus m_bus;
        private readonly PlazaConfigTable m_config;
        private readonly IReadOnlyList<BlessingTable> m_all;

        // 걸린 축복(없으면 null) · 남은 초 · 다시 빌 때까지 남은 초
        public BlessingTable Active { get; private set; }
        public double Remaining { get; private set; }
        public double Cooldown { get; private set; }
        public bool CanPray => Cooldown <= 0d;

        public Blessing(TableSet tables, EventBus bus)
        {
            m_bus = bus;
            m_config = tables.Get<PlazaConfigTable>(PlazaConfigTable.k_Main);
            m_all = tables.GetAll<BlessingTable>();
        }

        // 쉬는 중이면 아무 일 없이 false
        public bool TryPray(IRandom random)
        {
            if (!CanPray)
            {
                return false;
            }

            Active = m_all[Pick(random)];
            Remaining = Active.Seconds;
            Cooldown = m_config.BlessingCooldown;
            m_bus.Publish(new Events.BlessingChanged(this, true));
            return true;
        }

        public void Tick(double dt)
        {
            bool changed = false;

            if (Active != null)
            {
                Remaining -= dt;

                if (Remaining <= 0d)
                {
                    Active = null;
                    Remaining = 0d;
                    changed = true;
                }
            }

            if (Cooldown > 0d)
            {
                Cooldown = Math.Max(0d, Cooldown - dt);
                changed |= Cooldown <= 0d;
            }

            if (changed)
            {
                m_bus.Publish(new Events.BlessingChanged(this, false));
            }
        }

        // 걸린 축복이 그 효과면 값(없으면 0)
        public double Boost(string effect)
        {
            return Active != null && Active.Id == effect ? Active.Value : 0d;
        }

        private int Pick(IRandom random)
        {
            double total = 0d;

            foreach (BlessingTable blessing in m_all)
            {
                total += blessing.Weight;
            }

            double roll = random.NextDouble() * total;

            for (int i = 0; i < m_all.Count; i++)
            {
                roll -= m_all[i].Weight;

                if (roll < 0d)
                {
                    return i;
                }
            }

            return m_all.Count - 1;
        }
    }
}
