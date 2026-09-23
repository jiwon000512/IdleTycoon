using System.Collections;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.6 · 손님 동선 설계 v0.2 · 설계 09: 가게 웜뱃. 위치·보는 방향은 Core(ShopSim.WombatPosition)를 매 프레임 읽는다.
    // 걸으면 그 방향의 앞·뒤·옆 걷기, 서면 숨쉬기(마지막으로 걸은 방향. 계산대 자리에서 줄 머리가 서 있으면 계산 중 뒷모습)
    // 설계 10: 꺼내면 빵 하나가 오븐 → 웜뱃으로 날아온 뒤 층이 늘고, 채우면 맨 위 층 → 진열대로 빵 하나가 날아간다
    // 2026-09-23: 든 빵은 머리 위가 아니라 앞발에서 위로 쌓는다(뒷모습이면 몸 뒤에)
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
        [Tooltip("든 빵 층(아래부터). 보이는 층 수의 상한")]
        [SerializeField] private SpriteRenderer[] m_carry;
        [Tooltip("맨 아래 빵 가운데 높이(유닛, 발끝 기준) = 앞발 0.21 + 빵 반쯤")]
        [SerializeField] private float m_handHeight = 0.35f;
        [Tooltip("옆모습일 때 빵을 보는 쪽으로 내미는 거리(유닛)")]
        [SerializeField] private float m_handReach = 0.2f;
        [Tooltip("나르는 빵이 날아가는 시간(초)과 포물선 높이(유닛)")]
        [SerializeField] private float m_flySeconds = 0.3f;
        [SerializeField] private float m_flyArc = 0.4f;

        // 나는 빵은 가게의 모든 그림 위에
        private const int k_FlyOrder = 1000;
        // 든 빵 층 순서: 몸(0) 앞 51~, 뒷모습이면 몸 뒤 -10~
        private const int k_CarryFrontOrder = 51;
        private const int k_CarryBackOrder = -10;

        private ShopSim m_shop;
        private ShopView m_view;
        private FrameCache m_frames;
        private Sprite[] m_playing;
        private int m_shownCarry;
        private int m_lastCount;
        private BreadRecord m_lastCarried;
        // 꺼내기는 오븐 사건 바로 뒤에 들기 사건이 온다(ShopSim.TakeOut). 꺼내기가 auto라 대상이 아닌 오븐에서도 꺼낸다
        private int m_lastOven;

        public void Bind(ShopSim shop, ShopView view, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_frames = frames;
            m_shop.CarryChanged += Shop_CarryChanged;
            m_shop.OvenChanged += Shop_OvenChanged;
            ShowCarry();
            Update();
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_shop.CarryChanged -= Shop_CarryChanged;
                m_shop.OvenChanged -= Shop_OvenChanged;
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
            m_carry[0].transform.parent.localPosition = Fx.HandOffset(facing, m_handHeight, m_handReach);

            for (int i = 0; i < m_carry.Length; i++)
            {
                m_carry[i].sortingOrder = Fx.CarryOrder(facing, k_CarryFrontOrder, k_CarryBackOrder) + i;
            }

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

        private void Shop_CarryChanged()
        {
            int count = m_shop.CarriedCount;
            BreadRecord bread = count > m_lastCount ? m_shop.Carried : m_lastCarried;

            if (count > m_lastCount)
            {
                StartCoroutine(FlyRoutine(Icon(bread), m_view.OvenBreadPosition(m_lastOven), () => m_carry[Mathf.Min(count, m_carry.Length) - 1].transform.position, ShowCarry));
            }
            else
            {
                if (count < m_lastCount)
                {
                    Vector3 shelf = m_view.ShelfIconPosition(bread.Id);
                    StartCoroutine(FlyRoutine(Icon(bread), m_carry[Mathf.Min(m_lastCount, m_carry.Length) - 1].transform.position, () => shelf, null));
                }

                ShowCarry();
            }

            m_lastCount = count;
            m_lastCarried = m_shop.Carried;
        }

        private void Shop_OvenChanged(int index)
        {
            m_lastOven = index;
        }

        private Sprite Icon(BreadRecord bread)
        {
            return m_frames.Get(bread.Sprite)[0];
        }

        // 연출 강도(설계 09 7장): 꺼내서 층이 늘면 맨 위 층만 톡
        private void ShowCarry()
        {
            int shown = Mathf.Min(m_shop.CarriedCount, m_carry.Length);
            Sprite icon = m_shop.Carried != null ? Icon(m_shop.Carried) : null;

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

        // 빵 그림 하나가 from에서 to(움직이는 자리일 수 있다)로 포물선을 그리며 날아가고 끝나면 arrived
        private IEnumerator FlyRoutine(Sprite icon, Vector3 from, System.Func<Vector3> to, System.Action arrived)
        {
            GameObject go = new GameObject("FlyingBread");
            go.transform.SetParent(m_view.transform, false);
            go.transform.localScale = m_carry[0].transform.lossyScale;
            SpriteRenderer flying = go.AddComponent<SpriteRenderer>();
            flying.sprite = icon;
            flying.sortingOrder = k_FlyOrder;

            for (float t = 0f; t < m_flySeconds; t += Time.deltaTime)
            {
                float k = t / m_flySeconds;
                go.transform.position = Vector3.Lerp(from, to(), k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * m_flyArc);
                yield return null;
            }

            Destroy(go);
            arrived?.Invoke();
        }
    }
}
