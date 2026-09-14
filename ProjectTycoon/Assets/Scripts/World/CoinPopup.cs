using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 06 P8: 관광객 머리 위에서 떠올라 사라지는 코인 연출. 숫자는 프리팹 인스펙터
    public sealed class CoinPopup : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_renderer;
        [SerializeField] private float m_riseHeight = 0.8f;
        [SerializeField] private float m_seconds = 0.7f;

        private Vector3 m_up;
        private float m_elapsed;

        public void Initialize(Quaternion billboardRotation)
        {
            transform.rotation = billboardRotation;
            m_up = billboardRotation * Vector3.up;
        }

        private void Update()
        {
            m_elapsed += Time.deltaTime;
            transform.position += m_up * (m_riseHeight * Time.deltaTime / m_seconds);

            Color color = m_renderer.color;
            color.a = 1f - m_elapsed / m_seconds;
            m_renderer.color = color;

            if (m_elapsed >= m_seconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
