using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 진열대 재고 표지판(칠판 입간판, 2026-09-24 사용자 선택): 진열대 벽 쪽 앞 모서리에 서서 분필 글자로 재고 수를 보인다.
    // 손님이 표지판 앞뒤로 설 수 있게 진열대와 따로 정렬되는 개체(진열대 SortingGroup 밖)
    public sealed class ShelfSignView : MonoBehaviour
    {
        // 연출 1차: 재고 0이면 숫자가 깜빡인다
        private const float k_BlinkSeconds = 0.6f;
        private static readonly Color k_Chalk = new Color32(0xF4, 0xEE, 0xDC, 0xFF);
        private static readonly Color k_EmptyChalk = new Color32(0xF0, 0xA0, 0x70, 0xFF);

        [SerializeField] private TextMeshPro m_stockText;

        private bool m_empty;

        public void Show(int stock)
        {
            m_stockText.text = stock.ToString();
            m_stockText.color = stock == 0 ? k_EmptyChalk : k_Chalk;
            m_empty = stock == 0;
        }

        private void Update()
        {
            if (!m_empty)
            {
                return;
            }

            Color color = m_stockText.color;
            color.a = Mathf.Repeat(Time.time, k_BlinkSeconds) < k_BlinkSeconds * 0.5f ? 1f : 0.35f;
            m_stockText.color = color;
        }
    }
}
