using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08: 계산대 구역. 줄 머리 자리에서 위로 줄이 이어진다. 웜뱃은 이 구역의 자식이고 심부름 때 구역 밖으로 걸어갔다 온다(v0.6)
    public sealed class CounterView : ShopSection
    {
        [SerializeField] private Transform m_queueHead;
        [SerializeField] private ShopWombat m_wombat;

        public Vector2 QueueHead => m_queueHead.position;
        public ShopWombat Wombat => m_wombat;

        public void SetServing(bool serving)
        {
            m_wombat.SetServing(serving);
        }
    }
}
