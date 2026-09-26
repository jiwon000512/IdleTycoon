using System.Collections;
using TMPro;
using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 손님 동선 설계 v0.2: Core 손님(Visitor: 위치·보는 방향·상태)을 매 프레임 그대로 그린다. 걷기·판단은 Core가, 여기서는 그림 고르기와 연출만.
    // 계층: 루트(발끝) → ModelRoot(크기) → Sprite + Shadow. 머리 위: 기다림 말풍선(빈 진열대 앞), 하트. 집은 빵은 VisitorTable.carryAt 자리(앞발 또는 머리 위)
    public sealed class VisitorView : MonoBehaviour
    {
        // 집은 빵 순서: 몸(0) 앞, 뒷모습이면 몸 뒤
        private const int k_CarryFrontOrder = 51;
        private const int k_CarryBackOrder = -1;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private CoinPopup m_coinPrefab;
        [Tooltip("설계 22 이모지 말풍선(BubbleTable 칸 번호 = bubble_sheet 칸). Core Bubble 상태를 매 프레임 읽는다")]
        [SerializeField] private SpriteRenderer m_bubble;
        [SerializeField] private Sprite[] m_bubbleFrames;
        [Tooltip("집은 빵. 계산할 때까지 앞발(또는 머리 위)에")]
        [SerializeField] private SpriteRenderer m_carry;
        [Tooltip("대화 글자 말풍선(9-slice 상자 + 글)")]
        [SerializeField] private SpriteRenderer m_say;
        [SerializeField] private TextMeshPro m_sayText;
        [SerializeField] private float m_sayPadding = 0.3f;
        [Tooltip("톡 뛸 때 솟는 높이(유닛)")]
        [SerializeField] private float m_hopHeight = 0.15f;
        [Tooltip("빵이 진열대에서 드는 자리로 날아오는 시간(초)")]
        [SerializeField] private float m_carryFlySeconds = 0.3f;
        [Tooltip("carryAt hand: 집은 빵 가운데 높이 = 키 × 이 비율 + 빵 반(손님 앞발이 키의 약 1/3)")]
        [SerializeField] private float m_handRatio = 0.33f;
        [SerializeField] private float m_breadHalf = 0.18f;
        [Tooltip("옆모습일 때 빵을 보는 쪽으로 내미는 거리(유닛)")]
        [SerializeField] private float m_handReach = 0.2f;
        [Tooltip("코인 팝업을 머리 옆으로 비키는 거리(유닛)")]
        [SerializeField] private float m_coinSideOffset = 0.7f;

        private Visitor m_walker;
        private Transform m_origin;
        private Sprite[] m_frontIdle;
        private Sprite[] m_frontMove;
        private Sprite[] m_backIdle;
        private Sprite[] m_backMove;
        private Sprite[] m_sideIdle;
        private Sprite[] m_sideMove;
        private float m_idleFrameRate;
        private float m_moveFrameRate;
        private Sprite[] m_playing;
        private float m_height;
        private float m_shadowAlpha;
        private bool m_carrying;
        private bool m_flying;
        private Facing m_facing = Facing.Down;
        private bool m_carryOnHead;
        private Coroutine m_saying;

        private Vector3 HeadOffset => new Vector3(0f, m_height, 0f);
        // 2026-09-23: 집은 빵은 visitors.carryAt 자리에 든다. 앞발이면 옆모습에서 보는 쪽으로 내민다
        private Vector3 CarryOffset => m_carryOnHead
            ? HeadOffset + Vector3.up * 0.1f
            : Fx.HandOffset(m_facing, m_height * m_handRatio + m_breadHalf, m_handReach);

        // 설계 11: 빵집 손님과 광장 손님이 같이 쓴다. origin = 그 곳(빵집·광장)의 원점
        public void Initialize(Visitor walker, FrameCache frames, Transform origin)
        {
            VisitorTable look = walker.Look;
            m_walker = walker;
            m_origin = origin;
            m_shadowAlpha = m_shadowRenderer.color.a;
            m_frontIdle = frames.Get(look.IdleSheet ?? look.Sprite);
            m_frontMove = frames.Get(look.MoveSheet ?? look.Sprite);
            m_backIdle = look.BackIdleSheet != null ? frames.Get(look.BackIdleSheet) : m_frontIdle;
            m_backMove = look.BackMoveSheet != null ? frames.Get(look.BackMoveSheet) : m_frontMove;
            m_sideIdle = look.SideIdleSheet != null ? frames.Get(look.SideIdleSheet) : m_frontIdle;
            m_sideMove = look.SideMoveSheet != null ? frames.Get(look.SideMoveSheet) : m_frontMove;
            m_idleFrameRate = (float)look.IdleFrameRate;
            m_moveFrameRate = (float)look.MoveFrameRate;
            m_modelRoot.localScale = Vector3.one * (float)look.Scale;
            m_height = m_frontIdle[0].bounds.size.y * (float)look.Scale;
            m_carryOnHead = look.CarryAt == CarryAt.Head;
            m_bubble.transform.localPosition = HeadOffset;
            m_say.transform.localPosition = HeadOffset;
            m_bubble.enabled = false;
            m_carry.enabled = false;
            m_say.enabled = false;
            m_sayText.enabled = false;
            Update();
        }

        // 설계 22: 머리 위 글자 말풍선을 seconds 동안. 이모지 말풍선은 그동안 숨긴다
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

        // 집기: 빵 그림이 진열대(from, 월드)에서 드는 자리로 날아와 계산할 때까지 든다
        public void Pick(Sprite bread, Vector3 from)
        {
            m_carry.sprite = bread;
            m_carrying = true;
            StartCoroutine(FlyRoutine(from));
        }

        // 설계 21 점원: 든 빵을 바로 보이거나 감춘다(null = 빈손)
        public void ShowCarry(Sprite bread)
        {
            m_carry.sprite = bread;
            m_carrying = bread != null;
        }

        // 결제 코인 팝업(♥ 말풍선은 Core Bubble이 띄운다)
        public void Pay(string amount)
        {
            m_carrying = false;
            CoinPopup popup = Instantiate(m_coinPrefab, transform.position + HeadOffset + Vector3.right * m_coinSideOffset, Quaternion.identity, transform.parent);
            popup.Show(amount);
        }

        private void Update()
        {
            System.Numerics.Vector2 p = m_walker.Position + m_walker.Sidestep;
            Vector3 position = m_origin.position + new Vector3(p.X, p.Y, 0f);
            float alpha = 1f;
            VisitorPhase phase = m_walker.Phase;

            // 톡 뛰기: 솟았다 내려앉으며 나올 때 선명해지고 들어갈 때 흐려진다
            if (phase == VisitorPhase.Entering || phase == VisitorPhase.Exiting)
            {
                float t = (float)m_walker.HopProgress;
                position.y += Mathf.Sin(t * Mathf.PI) * m_hopHeight;
                alpha = phase == VisitorPhase.Entering ? t : 1f - t;
            }

            transform.position = position;
            SetAlpha(alpha);
            Facing facing = m_walker.Facing;
            Bubbles.Show(m_bubble, m_bubbleFrames, m_walker.Bubble, m_saying == null && alpha > 0f);
            Show(facing, m_walker.Moving);
            m_facing = facing;
            m_carry.sortingOrder = m_carryOnHead ? k_CarryFrontOrder : Fx.CarryOrder(facing, k_CarryFrontOrder, k_CarryBackOrder);

            if (!m_flying)
            {
                m_carry.transform.localPosition = CarryOffset;
            }

            m_carry.enabled = m_carrying;
        }

        private void Show(Facing facing, bool moving)
        {
            Sprite[] frames;

            switch (facing)
            {
                case Facing.Up:
                    frames = moving ? m_backMove : m_backIdle;
                    break;
                case Facing.Down:
                    frames = moving ? m_frontMove : m_frontIdle;
                    break;
                default:
                    frames = moving ? m_sideMove : m_sideIdle;
                    break;
            }

            m_spriteRenderer.flipX = facing == Facing.Left;

            // 같은 프레임 배열이면 다시 시작하지 않는다(숨쉬기·걷기가 끊기지 않게)
            if (m_playing != frames)
            {
                m_playing = frames;
                m_animator.Play(frames, moving ? m_moveFrameRate : m_idleFrameRate);
            }
        }

        private void SetAlpha(float alpha)
        {
            Color color = m_spriteRenderer.color;
            color.a = alpha;
            m_spriteRenderer.color = color;
            Color shadow = m_shadowRenderer.color;
            shadow.a = m_shadowAlpha * alpha;
            m_shadowRenderer.color = shadow;
            Color carry = m_carry.color;
            carry.a = alpha;
            m_carry.color = carry;
        }

        private IEnumerator FlyRoutine(Vector3 from)
        {
            m_flying = true;

            for (float t = 0f; t < m_carryFlySeconds; t += Time.deltaTime)
            {
                float k = t / m_carryFlySeconds;
                Vector3 to = transform.position + CarryOffset;
                m_carry.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.4f);
                yield return null;
            }

            m_flying = false;
        }

    }
}
