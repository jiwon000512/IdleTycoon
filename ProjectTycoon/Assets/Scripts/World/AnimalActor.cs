using UnityEngine;

namespace ZooTycoon.World
{
    public sealed class AnimalActor : MonoBehaviour
    {
        private const float k_ArriveDistance = 0.05f;

        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [Tooltip("걷기 속도(유닛/초)")]
        [SerializeField] private float m_walkSpeed = 2f;
        [Tooltip("도착 후 멈춰 있는 시간 범위(초)")]
        [SerializeField] private float m_idleSecondsMin = 0.5f;
        [SerializeField] private float m_idleSecondsMax = 2.5f;

        private CageView m_cage;
        private Vector3 m_destination;
        private float m_idleRemaining;

        // 설계 03 A5: 스프라이트는 카메라를 향해 세운 카드(빌보드), 그림자는 땅에 눕힌다
        public void Initialize(Sprite sprite, CageView cage, Quaternion billboardRotation)
        {
            m_spriteRenderer.sprite = sprite;
            m_cage = cage;
            m_destination = transform.position;
            m_idleRemaining = Random.Range(m_idleSecondsMin, m_idleSecondsMax);
            transform.rotation = billboardRotation;
            m_shadowRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // 설계 03 P4: 랜덤 점으로 직진 → 대기 → 반복. 깊이 정렬은 원근 카메라의 거리 정렬에 맡긴다
        private void Update()
        {
            if (m_idleRemaining > 0f)
            {
                m_idleRemaining -= Time.deltaTime;

                if (m_idleRemaining <= 0f)
                {
                    m_destination = m_cage.RandomWalkPoint();
                    m_spriteRenderer.flipX = m_destination.x < transform.position.x;
                }

                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, m_destination, m_walkSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, m_destination) <= k_ArriveDistance)
            {
                m_idleRemaining = Random.Range(m_idleSecondsMin, m_idleSecondsMax);
            }
        }
    }
}
