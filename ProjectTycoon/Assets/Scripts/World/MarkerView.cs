using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 굴 격자 설계 v0.5: 칸 위 파기 비용 태그. 글자 하나와 튀는 몸체(m_body)만 갖는다(빈 자리 점선은 2026-09-23부터 글자 없이 BakeryView가 직접)
    public sealed class MarkerView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private TextMeshPro m_text;

        private float m_width;

        // 글이 길면 알약이 옆으로 늘어난다(구운 폭보다 줄지는 않는다)
        public void Show(string text)
        {
            if (m_text.text != text)
            {
                m_width = m_width > 0f ? m_width : m_body.size.x;
                m_text.text = text;
                m_body.size = new Vector2(Mathf.Max(m_width, m_text.GetPreferredValues(text).x + m_body.size.y), m_body.size.y);
            }

            gameObject.SetActive(true);
        }

        // 숨은 태그(값 알약이 꺼진 물길 끝 등)는 튈 것이 없다
        public void Bounce()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StartCoroutine(Fx.Bounce(m_body.transform));
        }
    }
}
