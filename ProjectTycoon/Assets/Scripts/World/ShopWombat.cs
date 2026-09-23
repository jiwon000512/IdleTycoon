using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.6 · 손님 동선 설계 v0.2 · 설계 09: 가게 웜뱃. 위치·보는 방향은 Core(ShopSim.WombatPosition)를 매 프레임 읽는다.
    // 걸으면 그 방향의 앞·뒤·옆 걷기, 서면 숨쉬기(마지막으로 걸은 방향. 계산대 자리에서 줄 머리가 서 있으면 계산 중 뒷모습). 든 빵은 머리 위에 쌓는다
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
        [Tooltip("머리 위 빵 층(아래부터). 보이는 층 수의 상한")]
        [SerializeField] private SpriteRenderer[] m_carry;

        private ShopSim m_shop;
        private ShopView m_view;
        private FrameCache m_frames;
        private Sprite[] m_playing;
        private int m_shownCarry;

        public void Bind(ShopSim shop, ShopView view, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_frames = frames;
            m_shop.CarryChanged += Shop_CarryChanged;
            Shop_CarryChanged();
            Update();
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.CarryChanged -= Shop_CarryChanged;
            }
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

            if (!moving && m_shop.WombatAtCounter && Serving)
            {
                facing = Facing.Up;
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

        // 연출 강도(설계 09 7장): 꺼내서 층이 늘면 맨 위 층만 톡
        private void Shop_CarryChanged()
        {
            int shown = Mathf.Min(m_shop.CarriedCount, m_carry.Length);
            Sprite icon = m_shop.Carried != null ? m_frames.Get(m_shop.Carried.Sprite)[0] : null;

            for (int i = 0; i < m_carry.Length; i++)
            {
                m_carry[i].sprite = icon;
                m_carry[i].enabled = i < shown;
            }

            if (shown > m_shownCarry)
            {
                StartCoroutine(Fx.Bounce(m_carry[shown - 1].transform));
            }

            m_shownCarry = shown;
        }
    }
}
