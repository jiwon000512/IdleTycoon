using System;

namespace ZooTycoon.Core
{
    public enum NegotiationOutcome
    {
        Keep,
        Down,
        Up,
    }

    // 설계 21 월급 협상(타이밍 바): 표시가 바 위(0~1)를 왕복하고 탭 한 번에 멈춘다. 멈춘 색(ClerkConfigTable zones)이 유지·인하·인상 확률을,
    // 변동 금액은 기본 월급 × changeMin~changeMax 난수가 정한다. negotiateSeconds 안에 안 누르면 빨강. 화면은 Tick으로 돌리고 Marker를 읽는다
    public sealed class Negotiation
    {
        private readonly ClerkConfigTable m_config;
        private readonly IRandom m_random;
        private readonly double m_speed;
        private double m_elapsed;
        private int m_direction = 1;

        public int BaseWage { get; }
        public double Marker { get; private set; }
        public bool Done { get; private set; }
        public int Wage { get; private set; }
        public ZoneColor Zone { get; private set; }
        public NegotiationOutcome Outcome { get; private set; }

        public Negotiation(ClerkConfigTable config, IRandom random, int skill, int baseWage)
        {
            m_config = config;
            m_random = random;
            BaseWage = baseWage;
            Wage = baseWage;
            m_speed = config.MarkerSpeedMin + (config.MarkerSpeedMax - config.MarkerSpeedMin) * (skill - 1) / 99d;
        }

        public void Tick(double dt)
        {
            if (Done)
            {
                return;
            }

            m_elapsed += dt;

            if (m_elapsed >= m_config.NegotiateSeconds)
            {
                Resolve(ZoneOf(ZoneColor.Red));
                return;
            }

            double next = Marker + m_direction * m_speed * dt;

            if (next > 1d)
            {
                next = 2d - next;
                m_direction = -1;
            }
            else if (next < 0d)
            {
                next = -next;
                m_direction = 1;
            }

            Marker = next;
        }

        // 탭: 가운데에서 거리 안인 첫 색
        public void Stop()
        {
            if (Done)
            {
                return;
            }

            double distance = Math.Abs(Marker - 0.5);

            foreach (NegotiationZone zone in m_config.Zones)
            {
                if (distance <= zone.HalfWidth)
                {
                    Resolve(zone);
                    return;
                }
            }

            Resolve(ZoneOf(ZoneColor.Red));
        }

        private NegotiationZone ZoneOf(ZoneColor color)
        {
            foreach (NegotiationZone zone in m_config.Zones)
            {
                if (zone.Color == color)
                {
                    return zone;
                }
            }

            throw new InvalidOperationException($"협상 구간 '{color}'이 표에 없다.");
        }

        private void Resolve(NegotiationZone zone)
        {
            Done = true;
            Zone = zone.Color;
            double roll = m_random.NextDouble() * (zone.Keep + zone.Down + zone.Up);
            Outcome = roll < zone.Keep ? NegotiationOutcome.Keep : roll < zone.Keep + zone.Down ? NegotiationOutcome.Down : NegotiationOutcome.Up;
            int amount = Math.Max(1, (int)Math.Round(BaseWage * (m_config.ChangeMin + m_random.NextDouble() * (m_config.ChangeMax - m_config.ChangeMin))));

            switch (Outcome)
            {
                case NegotiationOutcome.Down:
                    Wage = Math.Max(1, BaseWage - amount);
                    break;
                case NegotiationOutcome.Up:
                    Wage = BaseWage + amount;
                    break;
                default:
                    Wage = BaseWage;
                    break;
            }
        }
    }
}
