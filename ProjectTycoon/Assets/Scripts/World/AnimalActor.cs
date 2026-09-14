using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 05 P3: Idle ↔ Move 두 상태. 숫자는 전부 AnimalRecord에서 온다
    // 계층: 루트(땅 위 위치, 회전 없음) → ModelRoot(크기) → Sprite(빌보드 카드) + Shadow(눕힘)
    public sealed class AnimalActor : MonoBehaviour
    {
        private const float k_ArriveDistance = 0.05f;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;

        private AnimalRecord m_record;
        private Sprite[] m_idleFrames;
        private Sprite[] m_moveFrames;
        private CageView m_cage;
        private AnimalState m_state;
        private Vector3 m_destination;
        private float m_idleRemaining;

        // 설계 03 A5: 스프라이트는 카메라를 향해 세운 카드(빌보드), 그림자는 땅에 눕힌다
        public void Initialize(AnimalRecord record, Sprite[] idleFrames, Sprite[] moveFrames, CageView cage, Quaternion billboardRotation)
        {
            m_record = record;
            m_idleFrames = idleFrames;
            m_moveFrames = moveFrames;
            m_cage = cage;
            m_modelRoot.localScale = Vector3.one * (float)record.Scale;
            m_spriteRenderer.transform.rotation = billboardRotation;
            m_shadowRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            EnterIdle();
        }

        private void Update()
        {
            switch (m_state)
            {
                case AnimalState.Idle:
                    m_idleRemaining -= Time.deltaTime;

                    if (m_idleRemaining <= 0f)
                    {
                        EnterMove();
                    }

                    break;

                case AnimalState.Move:
                    transform.position = Vector3.MoveTowards(
                        transform.position, m_destination, (float)m_record.MoveSpeed * Time.deltaTime);

                    if (Vector3.Distance(transform.position, m_destination) <= k_ArriveDistance)
                    {
                        EnterIdle();
                    }

                    break;
            }
        }

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        private void EnterIdle()
        {
            m_state = AnimalState.Idle;
            m_idleRemaining = Random.Range((float)m_record.IdleSecondsMin, (float)m_record.IdleSecondsMax);
            m_animator.Play(m_idleFrames, (float)m_record.FrameRate);
        }

        private void EnterMove()
        {
            m_state = AnimalState.Move;
            m_destination = m_cage.RandomWalkPoint();
            m_spriteRenderer.flipX = m_destination.x < transform.position.x;
            m_animator.Play(m_moveFrames, (float)m_record.FrameRate);
        }
    }
}
