using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 위 개체(동물·관광객)의 공통 몸체. 상태 객체(UnitState)가 매 프레임 무엇을 할지 정한다
    // 계층: 루트(땅 위 위치, 회전 없음) → ModelRoot(크기) → Sprite(빌보드 카드) + Shadow(눕힘)
    public abstract class Unit : MonoBehaviour
    {
        private const float k_ArriveDistance = 0.05f;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;

        private Sprite[] m_idleFrames;
        private Sprite[] m_moveFrames;
        private float m_frameRate;
        private float m_moveSpeed;
        private UnitState m_state;

        protected Quaternion Billboard { get; private set; }

        // 카드 높이(월드 유닛). 머리 위 연출 위치
        protected float Height => m_spriteRenderer.sprite.bounds.size.y * m_modelRoot.localScale.y;

        // 설계 03 A5: 스프라이트는 카메라를 향해 세운 카드(빌보드), 그림자는 땅에 눕힌다. 숫자는 전부 레코드에서 온다
        protected void Setup(IUnitRecord record, FrameCache frames, Quaternion billboard)
        {
            Billboard = billboard;
            m_idleFrames = frames.Get(record.IdleSheet ?? record.Sprite);
            m_moveFrames = frames.Get(record.MoveSheet ?? record.Sprite);
            m_frameRate = (float)record.FrameRate;
            m_moveSpeed = (float)record.MoveSpeed;
            m_modelRoot.localScale = Vector3.one * (float)record.Scale;
            m_spriteRenderer.transform.rotation = billboard;
            m_shadowRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        protected void ChangeState(UnitState state)
        {
            m_state = state;
            m_state.Enter();
        }

        internal void PlayIdle()
        {
            m_animator.Play(m_idleFrames, m_frameRate);
        }

        internal void PlayMove()
        {
            m_animator.Play(m_moveFrames, m_frameRate);
        }

        // 카드는 방향 1개 + 좌우 반전(기획서 2장 시점)
        internal void Face(float x)
        {
            m_spriteRenderer.flipX = x < transform.position.x;
        }

        // 목표로 한 걸음 옮기고, 도착했으면 true
        internal bool StepTowards(Vector3 target, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, m_moveSpeed * deltaTime);
            return Vector3.Distance(transform.position, target) <= k_ArriveDistance;
        }

        private void Update()
        {
            m_state?.Update(Time.deltaTime);
        }
    }
}
