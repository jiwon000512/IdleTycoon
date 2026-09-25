using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 진열대 재고 표지판(칠판 입간판, 2026-09-24 사용자 선택): 진열대 벽 쪽 앞 모서리에 서서 분필 글자로 재고 수를 보인다.
    // 손님이 표지판 앞뒤로 설 수 있게 진열대와 따로 정렬되는 개체(진열대 SortingGroup 밖). 재고 0이면 주황 분필(깜빡임은 2026-09-25 사용자 요청으로 뺌)
    public sealed class ShelfSignView : MonoBehaviour
    {
        private static readonly Color k_Chalk = new Color32(0xF4, 0xEE, 0xDC, 0xFF);
        private static readonly Color k_EmptyChalk = new Color32(0xF0, 0xA0, 0x70, 0xFF);

        [SerializeField] private TextMeshPro m_stockText;

        public void Show(int stock)
        {
            m_stockText.text = stock.ToString();
            m_stockText.color = stock == 0 ? k_EmptyChalk : k_Chalk;
        }
    }
}
