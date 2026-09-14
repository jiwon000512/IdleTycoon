using System;
using UnityEngine;
using ZooTycoon.Core;
using Random = UnityEngine.Random;

namespace ZooTycoon.World
{
    // 설계 06 P3: Enter(구경 지점까지) → View(멈춰 구경, 코인) → Leave(반대 끝으로) → 파괴
    // 계층은 AnimalActor와 같다: 루트(위치) → ModelRoot(크기) → Sprite(빌보드) + Shadow(눕힘)
    public sealed class VisitorActor : MonoBehaviour
    {
        private const float k_ArriveDistance = 0.05f;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private CoinPopup m_coinPrefab;
        [Tooltip("길 타일 바깥에서 나타나고 사라지는 거리(유닛)")]
        [SerializeField] private float m_entryMargin = 1.5f;
        [Tooltip("구경 지점이 길 타일 양 끝에서 안쪽으로 들어오는 거리(유닛)")]
        [SerializeField] private float m_viewInset = 1f;

        private VisitorRecord m_record;
        private Sprite[] m_idleFrames;
        private Sprite[] m_moveFrames;
        private Quaternion m_billboard;
        private float m_cageCenterX;
        private Vector3 m_exit;
        private Vector3 m_viewPoint;
        private bool m_views;
        private VisitorState m_state;
        private Vector3 m_target;
        private float m_viewRemaining;

        public event Action<VisitorActor> Exited;

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장)
        public void Initialize(VisitorRecord record, Sprite[] idleFrames, Sprite[] moveFrames, CageView cage, bool views, Quaternion billboardRotation)
        {
            m_record = record;
            m_idleFrames = idleFrames;
            m_moveFrames = moveFrames;
            m_billboard = billboardRotation;
            m_views = views;
            m_cageCenterX = cage.transform.position.x;
            m_modelRoot.localScale = Vector3.one * (float)record.Scale;
            m_spriteRenderer.transform.rotation = billboardRotation;
            m_shadowRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Rect path = cage.PathBounds;
            bool leftToRight = Random.value < 0.5f;
            float z = Random.Range(path.yMin, path.yMax);
            float startX = leftToRight ? path.xMin - m_entryMargin : path.xMax + m_entryMargin;
            float exitX = leftToRight ? path.xMax + m_entryMargin : path.xMin - m_entryMargin;

            transform.position = new Vector3(startX, 0f, z);
            m_exit = new Vector3(exitX, 0f, z);
            m_viewPoint = new Vector3(Random.Range(path.xMin + m_viewInset, path.xMax - m_viewInset), 0f, z);

            EnterWalk(VisitorState.Enter, m_views ? m_viewPoint : m_exit);
        }

        private void Update()
        {
            switch (m_state)
            {
                case VisitorState.Enter:
                case VisitorState.Leave:
                    transform.position = Vector3.MoveTowards(
                        transform.position, m_target, (float)m_record.MoveSpeed * Time.deltaTime);

                    if (Vector3.Distance(transform.position, m_target) > k_ArriveDistance)
                    {
                        break;
                    }

                    if (m_state == VisitorState.Enter && m_views)
                    {
                        EnterView();
                    }
                    else
                    {
                        Exit();
                    }

                    break;

                case VisitorState.View:
                    m_viewRemaining -= Time.deltaTime;

                    if (m_viewRemaining <= 0f)
                    {
                        EnterWalk(VisitorState.Leave, m_exit);
                    }

                    break;
            }
        }

        private void EnterWalk(VisitorState state, Vector3 target)
        {
            m_state = state;
            m_target = target;
            m_spriteRenderer.flipX = target.x < transform.position.x;
            m_animator.Play(m_moveFrames, (float)m_record.FrameRate);
        }

        private void EnterView()
        {
            m_state = VisitorState.View;
            m_viewRemaining = Random.Range((float)m_record.ViewSecondsMin, (float)m_record.ViewSecondsMax);
            m_spriteRenderer.flipX = m_cageCenterX < transform.position.x;
            m_animator.Play(m_idleFrames, (float)m_record.FrameRate);

            float height = m_spriteRenderer.sprite.bounds.size.y * m_modelRoot.localScale.y;
            Vector3 head = transform.position + m_billboard * Vector3.up * height;
            CoinPopup coin = Instantiate(m_coinPrefab, head, Quaternion.identity, transform.parent);
            coin.Initialize(m_billboard);
        }

        private void Exit()
        {
            Exited?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
