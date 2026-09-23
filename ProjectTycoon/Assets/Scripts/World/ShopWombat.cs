using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.6 · 손님 동선 설계 v0.2: 가게 웜뱃. 위치·보는 방향은 Core(ShopSim.WombatPosition)를 매 프레임 읽는다.
    // 계산대 제자리에서는 숨쉬기(줄 머리가 서 있으면 계산 중 뒷모습, 아니면 정면), 심부름 중에는 걷는 방향의 앞·뒤·옆 걷기
    public sealed class ShopWombat : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private SpriteRenderer m_renderer;
        [SerializeField] private Sprite[] m_frontIdle;
        [SerializeField] private Sprite[] m_backIdle;
        [SerializeField] private Sprite[] m_sideIdle;
        [SerializeField] private Sprite[] m_frontWalk;
        [SerializeField] private Sprite[] m_backWalk;
        [SerializeField] private Sprite[] m_sideWalk;
        [Tooltip("숨쉬기 초당 프레임. 4프레임 ÷ 숨 한 번 1.6초")]
        [SerializeField] private float m_idleFrameRate = 2.5f;
        [Tooltip("걷기 초당 프레임")]
        [SerializeField] private float m_walkFrameRate = 8f;

        private ShopSim m_shop;
        private ShopView m_view;
        private Sprite[] m_playing;

        public void Bind(ShopSim shop, ShopView view)
        {
            m_shop = shop;
            m_view = view;
            Update();
        }

        private void Update()
        {
            if (m_shop == null)
            {
                return;
            }

            transform.position = m_view.ToWorld(m_shop.WombatPosition);
            bool moving = m_shop.WombatMoving;
            Facing facing = m_shop.WombatFacing;

            if (!moving && m_shop.WombatAtCounter)
            {
                facing = Serving ? Facing.Up : Facing.Down;
            }

            Sprite[] frames;

            switch (facing)
            {
                case Facing.Up:
                    frames = moving ? m_backWalk : m_backIdle;
                    break;
                case Facing.Down:
                    frames = moving ? m_frontWalk : m_frontIdle;
                    break;
                default:
                    frames = moving ? m_sideWalk : m_sideIdle;
                    break;
            }

            m_renderer.flipX = facing == Facing.Left;

            // 같은 프레임 배열이면 다시 시작하지 않는다(숨쉬기가 끊기지 않게)
            if (m_playing != frames)
            {
                m_playing = frames;
                m_animator.Play(frames, moving ? m_walkFrameRate : m_idleFrameRate);
            }
        }

        // 줄 머리가 머리 자리에 서 있으면 계산 중
        private bool Serving
        {
            get
            {
                if (m_shop.Queue.Count == 0)
                {
                    return false;
                }

                Customer head = m_shop.Queue[0];
                return head.Phase == CustomerPhase.Queued && !head.Moving;
            }
        }
    }
}
