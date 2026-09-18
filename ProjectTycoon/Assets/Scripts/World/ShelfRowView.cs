using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 진열 층 하나 = 빵 두 칸(왼쪽·오른쪽). 가운데는 줄이 선다
    public sealed class ShelfRowView : ShopSection
    {
        [SerializeField] private ShelfView m_left;
        [SerializeField] private ShelfView m_right;

        public ShelfView Get(int side)
        {
            return side == 0 ? m_left : m_right;
        }
    }
}
