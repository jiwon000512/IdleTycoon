using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 계산대 구역. 줄 머리 자리에서 위로 줄이 이어진다
    public sealed class CounterView : ShopSection
    {
        [SerializeField] private Transform m_queueHead;

        public Vector2 QueueHead => m_queueHead.position;
    }
}
