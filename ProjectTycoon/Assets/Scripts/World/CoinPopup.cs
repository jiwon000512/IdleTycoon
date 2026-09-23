using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 06 P8: 머리 위에서 떠올라 사라지는 코인 연출. 연출 1차: 금액 글자("+10")를 함께 띄운다
    public sealed class CoinPopup : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_renderer;
        [SerializeField] private TextMeshPro m_amountText;
        [SerializeField] private float m_riseHeight = 0.8f;
        [SerializeField] private float m_seconds = 0.7f;

        private float m_elapsed;

        public void Show(string amount)
        {
            m_amountText.text = amount;
        }

        private void Update()
        {
            m_elapsed += Time.deltaTime;
            transform.position += Vector3.up * (m_riseHeight * Time.deltaTime / m_seconds);

            float alpha = 1f - m_elapsed / m_seconds;
            Color color = m_renderer.color;
            color.a = alpha;
            m_renderer.color = color;
            Color textColor = m_amountText.color;
            textColor.a = alpha;
            m_amountText.color = textColor;

            if (m_elapsed >= m_seconds)
            {
                Destroy(gameObject);
            }
        }
    }
}
