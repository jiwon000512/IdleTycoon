using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 월드 위 개체(동물·관광객)의 공통 몸체. 상태 객체(UnitState)가 매 프레임 무엇을 할지 정한다
    // 계층: 루트(화면 위치 = Iso.ToScreen(논리 위치)) → ModelRoot(크기) → Sprite + Shadow(납작한 타원)
    public abstract class Unit : MonoBehaviour
    {
        private const float k_ArriveDistance = 0.05f;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;

        private Sprite[] m_idleFrames;
        private Sprite[] m_moveFrames;
        private Sprite[] m_backIdleFrames;
        private Sprite[] m_backMoveFrames;
        private float m_idleFrameRate;
        private float m_moveFrameRate;
        private bool m_facingBack;
        private float m_moveSpeed;
        private UnitState m_state;

        // 논리 위치(XZ, 유닛). 이동·판정은 전부 이 좌표로 하고 화면 위치는 여기서만 만든다(설계 07-2)
        public Vector2 Logical { get; private set; }

        // 카드 높이(월드 유닛). 머리 위 연출 위치
        protected float Height => m_spriteRenderer.sprite.bounds.size.y * m_modelRoot.localScale.y;

        // 숫자는 전부 레코드에서 온다
        protected void Setup(IUnitRecord record, FrameCache frames)
        {
            m_idleFrames = frames.Get(record.IdleSheet ?? record.Sprite);
            m_moveFrames = frames.Get(record.MoveSheet ?? record.Sprite);
            m_backIdleFrames = record.BackIdleSheet != null ? frames.Get(record.BackIdleSheet) : m_idleFrames;
            m_backMoveFrames = record.BackMoveSheet != null ? frames.Get(record.BackMoveSheet) : m_moveFrames;
            m_idleFrameRate = (float)record.IdleFrameRate;
            m_moveFrameRate = (float)record.MoveFrameRate;
            m_moveSpeed = (float)record.MoveSpeed;
            m_modelRoot.localScale = Vector3.one * (float)record.Scale;
            m_shadowRenderer.transform.localScale = new Vector3(1f, Iso.k_Y / Iso.k_X, 1f);
        }

        public void SetLogical(Vector2 logical)
        {
            Logical = logical;
            transform.position = ToScreen(logical);
        }

        // 지상은 쿼터뷰 변환. 가게 안(설계 08)은 논리 좌표가 곧 화면 좌표라 재정의한다
        protected virtual Vector3 ToScreen(Vector2 logical)
        {
            return Iso.ToScreen(logical);
        }

        // 설계 08: 가게 손님은 Core가 정한 시간 안에 도착하도록 구간마다 속도를 바꾼다
        protected void SetMoveSpeed(float speed)
        {
            m_moveSpeed = speed;
        }

        protected void ChangeState(UnitState state)
        {
            m_state = state;
            m_state.Enter();
        }

        internal void PlayIdle()
        {
            m_animator.Play(m_facingBack ? m_backIdleFrames : m_idleFrames, m_idleFrameRate);
        }

        internal void PlayMove()
        {
            m_animator.Play(m_facingBack ? m_backMoveFrames : m_moveFrames, m_moveFrameRate);
        }

        // 좌우는 반전, 위로 가면 뒷모습·아래로 가면 앞모습. 옆으로만 가면 직전 방향을 유지한다. 화면 좌표로 판정한다
        internal void Face(Vector2 target)
        {
            Vector3 delta = ToScreen(target) - transform.position;
            m_spriteRenderer.flipX = delta.x < 0f;

            if (Mathf.Abs(delta.y) > k_ArriveDistance)
            {
                m_facingBack = delta.y > 0f;
            }
        }

        // 목표로 한 걸음 옮기고, 도착했으면 true
        internal bool StepTowards(Vector2 target, float deltaTime)
        {
            SetLogical(Vector2.MoveTowards(Logical, target, m_moveSpeed * deltaTime));
            return Vector2.Distance(Logical, target) <= k_ArriveDistance;
        }

        protected virtual void Update()
        {
            m_state?.Update(Time.deltaTime);
        }
    }
}
