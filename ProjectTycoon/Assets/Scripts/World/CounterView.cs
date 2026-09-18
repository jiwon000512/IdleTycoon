using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 계산대 구역. 줄 머리 자리에서 위로 줄이 이어진다.
    // 웜뱃은 기다릴 때 화면(정면)을 보고, 줄에 손님이 있으면 뒤돌아 손님 쪽(위)을 본다. 어느 쪽이든 숨쉬기 프레임을 돈다(몸 1칸 오르내림, 귀가 한 박자 늦게 따라옴)
    public sealed class CounterView : ShopSection
    {
        [SerializeField] private Transform m_queueHead;
        [SerializeField] private SpriteAnimator m_wombat;
        [Tooltip("정면 숨쉬기 프레임(재생 순서대로)")]
        [SerializeField] private Sprite[] m_frontFrames;
        [Tooltip("뒷모습 숨쉬기 프레임(재생 순서대로)")]
        [SerializeField] private Sprite[] m_backFrames;
        [Tooltip("초당 프레임. 4프레임 ÷ 숨 한 번 1.6초")]
        [SerializeField] private float m_frameRate = 2.5f;

        private bool? m_serving;

        public Vector2 QueueHead => m_queueHead.position;

        public void SetServing(bool serving)
        {
            if (m_serving == serving)
            {
                return;
            }

            m_serving = serving;
            m_wombat.Play(serving ? m_backFrames : m_frontFrames, m_frameRate);
        }
    }
}
