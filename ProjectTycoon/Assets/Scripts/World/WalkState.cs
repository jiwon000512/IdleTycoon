using System;
using UnityEngine;

namespace ZooTycoon.World
{
    // 목표 지점까지 걷고, 도착하면 알린다
    public sealed class WalkState : UnitState
    {
        private readonly Unit m_unit;
        private readonly Action m_arrived;
        private Vector3 m_target;

        public WalkState(Unit unit, Action arrived)
        {
            m_unit = unit;
            m_arrived = arrived;
        }

        public void SetTarget(Vector3 target)
        {
            m_target = target;
        }

        public override void Enter()
        {
            m_unit.Face(m_target.x);
            m_unit.PlayMove();
        }

        public override void Update(float deltaTime)
        {
            if (m_unit.StepTowards(m_target, deltaTime))
            {
                m_arrived();
            }
        }
    }
}
