using System.Collections;
using TMPro;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 08 v0.6 · 손님 동선 설계 v0.2 · 설계 09: 웜뱃 그림. 위치·보는 방향은 Core(Wombat.Mover)를 매 프레임 읽는다.
    // 걸으면 그 방향의 앞·뒤·옆 걷기, 서면 숨쉬기(마지막으로 걸은 방향. 계산대 자리에서 줄 머리가 서 있으면 계산 중 뒷모습)
    // 설계 10: 꺼내면 빵 하나가 오븐 → 웜뱃으로 날아온 뒤 층이 늘고, 채우면 맨 위 층 → 진열대로 빵 하나가 날아간다
    // 2026-09-23: 든 빵은 머리 위가 아니라 앞발에서 위로 쌓는다(뒷모습이면 몸 뒤에)
    // 설계 11: 광장 웜뱃도 같은 그림(WombatArea만 읽고 든 빵은 그리지 않는다). 웜뱃이 다른 곳에 있으면 숨는다
    public sealed class WombatView : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private SpriteRenderer m_renderer;
        [SerializeField] private SpriteRenderer m_shadow;
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
        [Tooltip("설계 22: 머리 위 이모지 말풍선과 대화 글자 말풍선")]
        [SerializeField] private SpriteRenderer m_bubble;
        [SerializeField] private Sprite[] m_bubbleFrames;
        [SerializeField] private SpriteRenderer m_say;
        [SerializeField] private TextMeshPro m_sayText;
        [SerializeField] private float m_sayPadding = 0.3f;

        // 나는 빵은 가게의 모든 그림 위에
        private const int k_FlyOrder = 1000;
        // 든 빵 층 순서: 몸(0) 앞 51~, 뒷모습이면 몸 뒤 -10~
        private const int k_CarryFrontOrder = 51;
        private const int k_CarryBackOrder = -10;

        private WombatArea m_area;
        private Transform m_origin;
        private BakeryArea m_shop;
        private Hands m_hands;
        private BakeryView m_view;
        private FrameCache m_frames;
        private Sprite[] m_playing;
        private int m_shownCarry;
        private int m_lastCount;
        private Coroutine m_saying;

        // 굽기 때 켜진 채 저장된 말풍선·글 상자는 실행 시작에 끈다(Say·Bubble이 필요할 때만 켠다)
        private void Awake()
        {
            m_bubble.enabled = false;
            m_say.enabled = false;
            m_sayText.enabled = false;
        }

        // 설계 22: 대화 글자 말풍선
        public void Say(string text, float seconds)
        {
            if (m_saying != null)
            {
                StopCoroutine(m_saying);
            }

            m_saying = StartCoroutine(SayRoutine(text, seconds));
        }

        private IEnumerator SayRoutine(string text, float seconds)
        {
            Bubbles.ShowSay(m_say, m_sayText, text, m_sayPadding);
            yield return new WaitForSeconds(seconds);
            m_say.enabled = false;
            m_sayText.enabled = false;
            m_saying = null;
        }

        public void Bind(BakeryArea shop, BakeryView view, FrameCache frames)
        {
            m_shop = shop;
            m_view = view;
            m_area = shop;
            m_hands = shop.Wombat.Worker.Hands;
            m_origin = view.transform;
            m_frames = frames;
            m_hands.Changed += Hands_Changed;
            ShowCarry();
            Update();
        }

        // 설계 11: 광장. 든 빵 층은 쓰지 않는다
        public void Bind(WombatArea area, Transform origin, FrameCache frames)
        {
            m_area = area;
            m_origin = origin;
            m_frames = frames;

            foreach (SpriteRenderer layer in m_carry)
            {
                layer.enabled = false;
            }

            Update();
        }

        private void OnDestroy()
        {
            if (m_shop != null)
            {
                m_hands.Changed -= Hands_Changed;
            }
        }

        private bool ServingAtCounter()
        {
            foreach (CounterInteractable counter in m_shop.Counters)
            {
                if (m_shop.IsInRange(counter) && counter.HeadWaiting)
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (m_area == null)
            {
                return;
            }

            bool present = m_area.WombatPresent;
            m_renderer.enabled = present;
            m_shadow.enabled = present;
            m_carry[0].transform.parent.gameObject.SetActive(present);

            if (!present)
            {
                m_bubble.enabled = false;
                return;
            }

            Bubbles.Show(m_bubble, m_bubbleFrames, m_area.Wombat.Bubble, m_saying == null);

            System.Numerics.Vector2 p = m_area.Wombat.Mover.Position;
            transform.position = m_origin.position + new Vector3(p.X, p.Y, 0f);
            bool moving = m_area.Wombat.Moving;
            Facing facing = m_area.Wombat.Mover.Facing;

            // 계산대 뒤(위)에서 줄 머리가 서 있으면 계산 중 앞모습(손님은 아래)
            if (!moving && m_shop != null && ServingAtCounter())
            {
                facing = Facing.Down;
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

        // 꺼내면(오븐에서) 빵 하나가 오븐 → 손으로 날아온 뒤 층이 늘고, 채우면(진열대에) 맨 위 층 → 진열대로 날아간다
        private void Hands_Changed(Interactable at)
        {
            int count = m_hands.Count;

            if (at is OvenInteractable oven && count > m_lastCount)
            {
                StartCoroutine(FlyRoutine(Icon(oven.Bread ?? m_hands.Bread), m_view.OvenBreadPosition(oven), () => m_carry[Mathf.Min(count, m_carry.Length) - 1].transform.position, ShowCarry));
            }
            else
            {
                if (at is ShelfInteractable shelf && count < m_lastCount)
                {
                    Vector3 to = m_view.ShelfIconPosition(shelf);
                    StartCoroutine(FlyRoutine(Icon(shelf.Bread), m_carry[Mathf.Min(m_lastCount, m_carry.Length) - 1].transform.position, () => to, null));
                }

                ShowCarry();
            }

            m_lastCount = count;
        }

        private Sprite Icon(BreadTable bread)
        {
            return m_frames.Get(bread.Sprite)[0];
        }

        // 연출 강도(설계 09 7장): 꺼내서 층이 늘면 맨 위 층만 톡
        private void ShowCarry()
        {
            int shown = Mathf.Min(m_hands.Count, m_carry.Length);
            Sprite icon = m_hands.Bread != null ? Icon(m_hands.Bread) : null;

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
