using System;

namespace ZooTycoon.World
{
    // 제자리에서 정해진 시간을 기다리고, 끝나면 알린다
    public sealed class WaitState : UnitState
    {
        private readonly Unit m_unit;
        private readonly Action m_done;
        private float m_remaining;

        public WaitState(Unit unit, Action done)
        {
            m_unit = unit;
            m_done = done;
        }

        public void SetSeconds(float seconds)
        {
            m_remaining = seconds;
        }

        public override void Enter()
        {
            m_unit.PlayIdle();
        }

        public override void Update(float deltaTime)
        {
            m_remaining -= deltaTime;

            if (m_remaining <= 0f)
            {
                m_done();
            }
        }
    }
}
