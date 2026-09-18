using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 오븐 줄 하나 = 오븐 두 대
    public sealed class OvenRowView : ShopSection
    {
        [SerializeField] private OvenView m_left;
        [SerializeField] private OvenView m_right;

        public OvenView Get(int side)
        {
            return side == 0 ? m_left : m_right;
        }
    }
}
