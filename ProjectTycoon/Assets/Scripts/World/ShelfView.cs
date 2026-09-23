using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 진열대 하나. 빵 아이콘 + 재고/용량. 굴 격자 설계 v0.5: 잠긴 칸은 없다(빈 자리는 MarkerView)
    public sealed class ShelfView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;
        [SerializeField] private TextMeshPro m_stockText;

        // 연출 1차: 재고 0이면 숫자가 깜빡인다
        private const float k_BlinkSeconds = 0.6f;
        private bool m_empty;

        // 빵 그림 자리(월드). 설계 10: 웜뱃이 채울 때 빵이 날아가는 곳
        public Vector3 IconPosition => m_icon.transform.position;

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        public void Show(Sprite icon, int stock, int capacity)
        {
            m_body.color = Color.white;
            m_icon.enabled = true;
            m_icon.sprite = icon;
            m_stockText.enabled = true;
            m_stockText.text = $"{stock}/{capacity}";
            m_stockText.color = stock == 0 ? new Color(0.75f, 0.2f, 0.15f) : new Color(0.23f, 0.14f, 0.09f);
            m_empty = stock == 0;
        }

        private void Update()
        {
            if (!m_empty || !m_stockText.enabled)
            {
                return;
            }

            Color color = m_stockText.color;
            color.a = Mathf.Repeat(Time.time, k_BlinkSeconds) < k_BlinkSeconds * 0.5f ? 1f : 0.35f;
            m_stockText.color = color;
        }
    }
}
