using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 계산대 구역. 줄 머리 자리에서 위로 줄이 이어진다.
    // 웜뱃은 기다릴 때 화면(정면)을 보고, 줄에 손님이 있으면 뒤돌아 손님 쪽(위)을 본다
    public sealed class CounterView : ShopSection
    {
        [SerializeField] private Transform m_queueHead;
        [SerializeField] private SpriteRenderer m_wombat;
        [SerializeField] private Sprite m_wombatFront;
        [SerializeField] private Sprite m_wombatBack;

        public Vector2 QueueHead => m_queueHead.position;

        public void SetServing(bool serving)
        {
            m_wombat.sprite = serving ? m_wombatBack : m_wombatFront;
        }
    }
}
