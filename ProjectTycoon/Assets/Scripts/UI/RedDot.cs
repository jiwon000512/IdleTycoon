using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 57 레드닷: 단추 오른쪽 위 빨간 점 하나 = 「지금 눌러서 할 수 있는 일이 있다」(2026-10-12 사용자, 제각각이던 「!」 · 숫자 배지를 합침).
    // 0이면 숨김, 1이면 점만(red_dot 28), 2 이상이면 수 든 점(red_dot_count 56, 굵은 11 글자 44). 그림은 아트방 A 「납작」
    public sealed class RedDot : MonoBehaviour
    {
        // 점 지름(점만 · 수 든 점), 둘 다 한 칸 4px의 배수
        private const float k_Dot = 28f;
        private const float k_Counted = 56f;

        [SerializeField] private Image m_image;
        [SerializeField] private Sprite m_dotSprite;
        [SerializeField] private Sprite m_countSprite;
        [SerializeField] private TMP_Text m_count;

        public void Set(bool on)
        {
            Set(on ? 1 : 0);
        }

        public void Set(int count)
        {
            gameObject.SetActive(count > 0);
            m_count.gameObject.SetActive(count > 1);
            m_count.text = count > 1 ? count.ToString() : string.Empty;
            ((RectTransform)transform).sizeDelta = Vector2.one * (count > 1 ? k_Counted : k_Dot);
            Sprite sprite = count > 1 ? m_countSprite : m_dotSprite;

            if (sprite != null)
            {
                m_image.sprite = sprite;
            }
        }
    }
}
