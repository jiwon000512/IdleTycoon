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
        // 그림 한 칸(2px ÷ PPU 80)
        private const float k_Cell = 0.025f;

        [SerializeField] private Transform m_modelRoot;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private CoinPopup m_coinPrefab;
        [Tooltip("설계 22 이모지 말풍선(BubbleTable 칸 번호 = bubble_sheet 칸). Core Bubble 상태를 매 프레임 읽는다")]
        [SerializeField] private SpriteRenderer m_bubble;
        [SerializeField] private Sprite[] m_bubbleFrames;
        [Tooltip("설계 41 평가단 수첩 표식(머리 위). 말풍선 · 글자 말풍선 · 머리에 인 빵과 같은 자리라 그동안 숨긴다")]
        [SerializeField] private SpriteRenderer m_mark;
        [Tooltip("표식을 머리 꼭대기에서 띄우는 높이(3px)")]
        [SerializeField] private float m_markLift = 0.0375f;
        [Tooltip("집은 빵. 계산할 때까지 앞발(또는 머리 위)에")]
        [SerializeField] private SpriteRenderer m_carry;
        [Tooltip("대화 글자 말풍선(9-slice 상자 + 글)")]
        [SerializeField] private SpriteRenderer m_say;
        [SerializeField] private SpriteRenderer m_sayTail;
        [SerializeField] private TextMeshPro m_sayText;
        [SerializeField] private float m_sayPadding = 0.3f;
        [Tooltip("톡 뛸 때 솟는 높이(유닛)")]
        [SerializeField] private float m_hopHeight = 0.15f;
        [Tooltip("빵이 진열대에서 드는 자리로 날아오는 시간(초)")]
        [SerializeField] private float m_carryFlySeconds = 0.3f;
        [Tooltip("carryAt hand: 집은 빵 가운데 높이 = 키 × 이 비율 + 빵 반(손님 앞발이 키의 약 1/3)")]
        [SerializeField] private float m_breadHalf = 0.18f;
        [Tooltip("옆모습일 때 빵을 보는 쪽으로 내미는 거리(유닛)")]
        [SerializeField] private float m_handReach = 0.2f;
        [Tooltip("코인 팝업을 머리 옆으로 비키는 거리(유닛)")]
        [SerializeField] private float m_coinSideOffset = 0.7f;
        [Tooltip("숨쉬기 · 걷기 프레임 시간(초). 모든 손님 공통(Shop/Source~/make_anim.py 순서, 웜뱃과 같은 값)")]
        [SerializeField] private float[] m_idleSeconds = { 0.5f, 0.16f, 0.52f, 0.16f };
        [SerializeField] private float[] m_walkSeconds = { 0.08f, 0.065f, 0.065f, 0.065f, 0.08f, 0.065f, 0.065f, 0.065f };
        [Tooltip("걷기 프레임마다 몸이 뜬 칸(+ 위). 든 빵도 같이 오르내린다")]
        [SerializeField] private int[] m_walkBob = { 0, -1, 1, 0, 0, -1, 1, 0 };
        [Tooltip("숨쉬기 프레임마다 몸이 뜬 칸(+ 위). 든 빵도 같이 오르내린다")]
        [SerializeField] private int[] m_idleBob = { 0, -1, -1, 0 };
        [Tooltip("눈 깜빡임: 서 있을 때 이 사이 무작위 초마다 · 감는 시간(초)")]
        [SerializeField] private Vector2 m_blinkEvery = new Vector2(3f, 6f);
        [SerializeField] private float m_blinkSeconds = 0.12f;
        [Tooltip("딴짓(VisitorTable fidgetSheet, 종 · 방향마다 한 벌): 서 있으면 이 사이 무작위 초마다 한 번 · 한 칸 시간(초)")]
        [SerializeField] private Vector2 m_fidgetEvery = new Vector2(4f, 9f);
        [SerializeField] private float m_fidgetFrameSeconds = 0.08f;

        private Visitor m_walker;
        private Transform m_origin;
        private Sprite[] m_frontIdle;
        private Sprite[] m_frontMove;
        private Sprite[] m_backIdle;
        private Sprite[] m_backMove;
        private Sprite[] m_sideIdle;
        private Sprite[] m_sideMove;
        private Sprite[] m_frontBlink;
        private Sprite[] m_sideBlink;
        private float m_blinkIn;
        private Sprite[] m_frontFidget;
        private Sprite[] m_backFidget;
        private Sprite[] m_sideFidget;
        private float m_fidgetIn;
        private Sprite[] m_playing;
        private float m_height;
        private float m_handRatio;
        private float m_shadowAlpha;
        private bool m_carrying;
        private bool m_flying;
        private Facing m_facing = Facing.Down;
        private bool m_carryOnHead;
        private Coroutine m_saying;
        private System.Func<bool> m_marked;
        private System.Func<string> m_note;
        private string m_noteShown;

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
            m_frontBlink = look.BlinkSheet != null ? frames.Get(look.BlinkSheet) : null;
            m_sideBlink = look.SideBlinkSheet != null ? frames.Get(look.SideBlinkSheet) : null;
            m_blinkIn = Random.Range(m_blinkEvery.x, m_blinkEvery.y);
            m_frontFidget = look.FidgetSheet != null ? frames.Get(look.FidgetSheet) : null;
            m_backFidget = look.BackFidgetSheet != null ? frames.Get(look.BackFidgetSheet) : null;
            m_sideFidget = look.SideFidgetSheet != null ? frames.Get(look.SideFidgetSheet) : null;
            m_fidgetIn = Random.Range(m_fidgetEvery.x, m_fidgetEvery.y);
            m_modelRoot.localScale = Vector3.one * (float)look.Scale;
            m_height = m_frontIdle[0].bounds.size.y * (float)look.Scale;
            m_carryOnHead = look.CarryAt == CarryAt.Head;
            m_handRatio = (float)look.HandRatio;
            m_bubble.transform.localPosition = HeadOffset;
            m_say.transform.localPosition = HeadOffset + Vector3.up * Bubbles.SayLift;
            m_bubble.enabled = false;
            m_mark.transform.localPosition = HeadOffset + Vector3.up * m_markLift;
            m_mark.enabled = false;
            m_carry.enabled = false;
            Bubbles.HideSay(m_say, m_sayTail, m_sayText);
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
            Bubbles.ShowSay(m_say, m_sayTail, m_sayText, text, m_sayPadding);
            yield return new WaitForSeconds(seconds);
            Bubbles.HideSay(m_say, m_sayTail, m_sayText);
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
            PopCoin(amount);
        }

        // 설계 41: marked가 참인 동안 머리 위에 평가단 표식(빵집 손님만)
        public void MarkWhile(System.Func<bool> marked)
        {
            m_marked = marked;
        }

        // note가 글을 주는 동안 머리 위 글자 말풍선(점원이 기다리는 재료). 대사(Say)가 먼저, 그동안 이모지 말풍선은 숨긴다
        public void SayWhile(System.Func<string> note)
        {
            m_note = note;
        }

        // 머리 위 코인 + 금액(손님 결제 · 점원 월급)
        public void PopCoin(string amount)
        {
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
            string note = m_saying == null && alpha > 0f ? m_note?.Invoke() : null;

            // 대사가 끝나면(HideSay) 다시 띄우도록 대사 동안은 띄운 글을 잊는다
            if (m_saying != null)
            {
                m_noteShown = null;
            }
            else if (note != m_noteShown)
            {
                if (note != null)
                {
                    Bubbles.ShowSay(m_say, m_sayTail, m_sayText, note, m_sayPadding);
                }
                else
                {
                    Bubbles.HideSay(m_say, m_sayTail, m_sayText);
                }

                m_noteShown = note;
            }

            Bubbles.Show(m_bubble, m_bubbleFrames, m_walker.Bubble, m_saying == null && note == null && alpha > 0f);
            m_mark.enabled = m_marked != null && m_marked() && !m_bubble.enabled && m_saying == null && note == null && !(m_carrying && m_carryOnHead) && alpha > 0f;
            m_mark.color = new Color(1f, 1f, 1f, alpha);
            Show(facing, m_walker.Moving && !m_walker.Paused);
            m_facing = facing;
            m_carry.sortingOrder = m_carryOnHead ? k_CarryFrontOrder : Fx.CarryOrder(facing, k_CarryFrontOrder, k_CarryBackOrder);

            if (!m_flying)
            {
                // 든 빵도 몸과 같이 오르내린다(걷기 · 숨쉬기 프레임만, 딴짓은 몸 높이가 그대로)
                int[] bobs = m_walker.Moving && !m_walker.Paused ? m_walkBob : m_idleBob;
                bool bobbing = m_playing != null && m_playing.Length == bobs.Length && !m_animator.Interjecting;
                float bob = bobbing ? bobs[m_animator.Index] * k_Cell * m_modelRoot.localScale.y : 0f;
                m_carry.transform.localPosition = CarryOffset + Vector3.up * bob;
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

            // 같은 프레임 배열이면 다시 시작하지 않는다(숨쉬기·걷기가 끊기지 않게). 시작 박자는 무작위(손님 여럿이 같은 박자로 움직이지 않게)
            if (m_playing != frames)
            {
                m_playing = frames;
                m_animator.Play(frames, moving ? m_walkSeconds : m_idleSeconds, Random.value);
            }

            // 서 있을 때만 가끔 눈을 감는다(뒷모습은 눈이 없다)
            m_blinkIn -= Time.deltaTime;

            if (!moving && m_blinkIn <= 0f)
            {
                m_blinkIn = Random.Range(m_blinkEvery.x, m_blinkEvery.y);
                m_animator.Overlay(facing == Facing.Down ? m_frontBlink : facing == Facing.Up ? null : m_sideBlink, m_blinkSeconds);
            }

            // 서 있으면 가끔 딴짓을 한 번(멈춘 뒤 적어도 m_fidgetEvery.x초 뒤, 숨쉬기 한 바퀴가 끝날 때 시작)
            m_fidgetIn = moving ? Mathf.Max(m_fidgetIn, m_fidgetEvery.x) : m_fidgetIn - Time.deltaTime;

            if (!moving && m_fidgetIn <= 0f)
            {
                m_fidgetIn = Random.Range(m_fidgetEvery.x, m_fidgetEvery.y);
                m_animator.Interject(facing == Facing.Down ? m_frontFidget : facing == Facing.Up ? m_backFidget : m_sideFidget, m_fidgetFrameSeconds);
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
