using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 05 P3: 존 안에서 기다림(WaitState) ↔ 임의 지점으로 이동(WalkState)을 반복한다
    public sealed class Animal : Unit
    {
        private AnimalRecord m_record;
        private ZoneView m_zone;
        private WaitState m_idle;
        private WalkState m_move;

        public void Initialize(AnimalRecord record, ZoneView zone, FrameCache frames)
        {
            m_record = record;
            m_zone = zone;
            m_idle = new WaitState(this, EnterMove);
            m_move = new WalkState(this, EnterIdle);
            Setup(record, frames);
            SetLogical(zone.RandomWalkPoint());
            EnterIdle();
        }

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        private void EnterIdle()
        {
            m_idle.SetSeconds(Random.Range((float)m_record.IdleSecondsMin, (float)m_record.IdleSecondsMax));
            ChangeState(m_idle);
        }

        private void EnterMove()
        {
            m_move.SetTarget(m_zone.RandomWalkPoint());
            ChangeState(m_move);
        }
    }
}
