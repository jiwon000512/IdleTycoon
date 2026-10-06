using System.Collections;
using TMPro;
using UnityEngine;
using GameKit.Audio;
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
        [Tooltip("눈 감은 숨쉬기(앞·옆, 숨쉬기와 같은 순서). 뒷모습은 없다")]
        [SerializeField] private Sprite[] m_frontBlink;
        [SerializeField] private Sprite[] m_sideBlink;
        [Tooltip("숨쉬기 프레임 시간(초): 기본 · 내려앉기 · 내려앉은 채 머묾 · 돌아옴(Source~/make_anim.py)")]
        [SerializeField] private float[] m_idleSeconds = { 0.5f, 0.16f, 0.52f, 0.16f };
        [Tooltip("숨쉬기 프레임마다 몸이 뜬 칸(+ 위). 든 빵도 같이 오르내린다")]
        [SerializeField] private int[] m_idleBob = { 0, -1, -1, 0 };
        [Tooltip("걷기 프레임 시간(초): 딛기(0·4)는 조금 길게")]
        [SerializeField] private float[] m_walkSeconds = { 0.08f, 0.065f, 0.065f, 0.065f, 0.08f, 0.065f, 0.065f, 0.065f };
        [Tooltip("걷기 프레임마다 몸이 뜬 칸(+ 위). 든 빵도 같이 오르내린다(Source~/make_anim.py의 body_y)")]
        [SerializeField] private int[] m_walkBob = { 0, -1, 1, 0, 0, -1, 1, 0 };
        [Tooltip("눈 깜빡임: 서 있을 때 이 사이 무작위 초마다 · 감는 시간(초)")]
        [SerializeField] private Vector2 m_blinkEvery = new Vector2(3f, 6f);
        [SerializeField] private float m_blinkSeconds = 0.12f;
        [Tooltip("딴짓(두리번 · 앞발 비비기 · 발 구르기 · 엉덩이 흔들기, 방향마다 한 벌): 서 있으면 이 사이 무작위 초마다 한 번 · 한 칸 시간(초). 늘 서 있는 주인공이라 손님(4~9초)보다 드물게")]
        [SerializeField] private Sprite[] m_frontFidget;
        [SerializeField] private Sprite[] m_backFidget;
        [SerializeField] private Sprite[] m_sideFidget;
        [SerializeField] private Vector2 m_fidgetEvery = new Vector2(6f, 12f);
        [SerializeField] private float m_fidgetFrameSeconds = 0.08f;
        [Tooltip("굴 파기 「웅크려 퍼 던지기」(방향마다 시트 한 장, Shop/Source~/make_dig.py) · 흙을 던지는 칸 번호(흙덩이가 이때 튄다). 한 칸 시간은 ConfigTable wombatDigSeconds ÷ 칸 수")]
        [SerializeField] private Sprite[] m_frontDig;
        [SerializeField] private Sprite[] m_backDig;
        [SerializeField] private Sprite[] m_sideDig;
        [Tooltip("설계 44 아트방 A 「통통 폴짝 · 곰인형」: 엉덩이 쿵 3칸(웅크림 · 폴짝 · 쿵) · 털썩 앉기 2칸(앉는 중 · 앉음), 방향마다 시트 한 장(Source~/make_thump.py)")]
        [SerializeField] private Sprite[] m_frontThump;
        [SerializeField] private Sprite[] m_backThump;
        [SerializeField] private Sprite[] m_sideThump;
        [SerializeField] private Sprite[] m_frontSit;
        [SerializeField] private Sprite[] m_backSit;
        [SerializeField] private Sprite[] m_sideSit;
        [SerializeField] private int m_digThrowFrame = 8;
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
        [SerializeField] private SpriteRenderer m_sayTail;
        [SerializeField] private TextMeshPro m_sayText;
        [SerializeField] private float m_sayPadding = 0.3f;

        // 그림 한 칸(2px ÷ PPU 80)
        private const float k_Cell = 0.025f;
        // 나는 빵은 가게의 모든 그림 위에
        private const int k_FlyOrder = 1000;
        // 든 빵 층 순서: 몸(0) 앞 51~, 뒷모습이면 몸 뒤 -10~
        private const int k_CarryFrontOrder = 51;
        private const int k_CarryBackOrder = -10;
        // 걷는 동안 발밑에서 이는 먼지(k_DustGap초마다 알갱이 k_DustCount개가 천천히 떠오른다)
        private static readonly Color k_Dust = new Color32(0xC8, 0xA8, 0x8C, 255);
        private const float k_DustGap = 0.2f;
        private const int k_DustCount = 3;
        private const int k_DustCells = 6;
        private const float k_DustSpread = 0.3f;
        private const float k_DustLift = 0.5f;
        private const float k_DustUp = 0.05f;
        private const float k_DustSeconds = 0.4f;

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
        private float m_blinkIn;
        private float m_fidgetIn;
        private Sprite m_square;
        private float m_dustTimer;
        // 설계 44 쿵 · 털썩 칸 순서(아트방): 쿵 = 웅크림 둘 · 폴짝 셋 · 쿵 셋, 털썩 = 앉는 중 둘 · 앉음. 한 칸 0.0625초(쿵 0.5초 ÷ 8)
        private static readonly int[] k_ThumpOrder = { 0, 0, 1, 1, 1, 2, 2, 2 };
        private static readonly int[] k_SitDown = { 0, 0, 1 };
        private const float k_ActFrameSeconds = 0.0625f;
        // 발소리(2026-09-30 사용자 요청): 걷기 프레임 가운데 몸이 내려앉는 칸(m_walkBob −1)에 한 번씩
        private static readonly int[] k_StepFrames = { 1, 5 };
        private int m_lastFrame = -1;

        // 굽기 때 켜진 채 저장된 말풍선·글 상자는 실행 시작에 끈다(Say·Bubble이 필요할 때만 켠다)
        private void Awake()
        {
            m_bubble.enabled = false;
            Bubbles.HideSay(m_say, m_sayTail, m_sayText);
            m_fidgetIn = m_fidgetEvery.x;
        }

        // 설계 22: 대화 글자 말풍선
        // 이 곳에 웜뱃이 있나(곳에 매인 소리는 이때만)
        public bool Present => m_area != null && m_area.WombatPresent;

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
            m_dustTimer = moving ? m_dustTimer - Time.deltaTime : 0f;

            if (moving && m_dustTimer <= 0f)
            {
                m_dustTimer = k_DustGap;
                m_square = m_square != null ? m_square : Fx.NewSquare();
                // 발끝보다 조금 위(몸 뒤로 정렬)
                StartCoroutine(Fx.Burst(m_origin, m_square, transform.position + Vector3.up * k_DustUp, k_DustCount, k_DustSpread, k_DustLift, 0f, k_DustSeconds, k_DustCells, k_Dust, 0));
            }

            // 계산대 뒤(위)에서 줄 머리가 서 있으면 계산 중 앞모습(손님은 아래). 굴을 파는 동안은 판 칸 쪽 그대로
            if (!moving && m_shop != null && !m_area.Wombat.Busy && ServingAtCounter())
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
            // 든 빵도 몸과 같이 오르내린다(걷기 · 숨쉬기 프레임만, 딴짓은 몸 높이가 그대로)
            int[] bobs = moving ? m_walkBob : m_idleBob;
            float bob = m_playing == frames && !m_animator.Interjecting ? bobs[m_animator.Index % bobs.Length] * k_Cell : 0f;
            m_carry[0].transform.parent.localPosition = Fx.HandOffset(facing, m_handHeight + bob, m_handReach);

            for (int i = 0; i < m_carry.Length; i++)
            {
                m_carry[i].sortingOrder = Fx.CarryOrder(facing, k_CarryFrontOrder, k_CarryBackOrder) + i;
            }

            // 같은 프레임 배열이면 다시 시작하지 않는다(숨쉬기가 끊기지 않게)
            if (m_playing != frames)
            {
                m_playing = frames;
                m_animator.Play(frames, moving ? m_walkSeconds : m_idleSeconds);
            }

            // 발소리는 이 곳에 웜뱃이 있을 때만 이 뷰가 도니 곳 밖에서는 나지 않는다
            int frame = m_animator.Index;

            if (moving && frame != m_lastFrame && System.Array.IndexOf(k_StepFrames, frame) >= 0)
            {
                SoundManager.Instance.Play(SoundTable.k_Step);
            }

            m_lastFrame = moving ? frame : -1;

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

        // 굴을 팠다(빵집 · 농장 칸): 지금 보는 방향으로 퍼 던지기를 바로 한 번, 웜뱃이 서 있는 시간(Wombat.DigSeconds)에 맞춰. 흙을 던지기까지 남은 초를 돌려준다
        // 웜뱃은 판 칸 쪽으로 돌아서므로 그 방향 숨쉬기를 먼저 깔아 둔다(Update가 방향이 바뀌었다고 다시 Play해 끼워 넣기를 지우지 않게, 끝나면 그 숨쉬기로)
        public float Dig()
        {
            Sprite[] frames = DigSheet();
            float frameSeconds = (float)m_area.Wombat.DigSeconds / frames.Length;
            PlayNow(frames, frameSeconds);
            return m_digThrowFrame * frameSeconds;
        }

        // 설계 44 엉덩이 쿵: 웅크림 · 폴짝 · 쿵을 seconds 동안 바로(아트방 순서 k_ThumpOrder). 소리는 쿵 칸(2)이 처음 나올 때. 그때까지 초를 돌려준다(물결 고리)
        public float Thump(float seconds)
        {
            float frameSeconds = seconds / k_ThumpOrder.Length;
            float impact = frameSeconds * System.Array.IndexOf(k_ThumpOrder, 2);
            PlayNow(Sequence(ThumpSheet(), k_ThumpOrder, frameSeconds), frameSeconds);
            StartCoroutine(PlayLater(SoundTable.k_Thump, impact));
            return impact;
        }

        // 설계 44 월척 털썩: 털썩 앉아(앉는 중 → 앉음) seconds가 다 될 때까지 앉음 칸을 유지한 뒤 일어선다
        public void Haul(float seconds)
        {
            PlayNow(Sequence(SitSheet(), k_SitDown, seconds), k_ActFrameSeconds);
            SoundManager.Instance.Play(SoundTable.k_Haul);
        }

        private static IEnumerator PlayLater(string id, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SoundManager.Instance.Play(id);
        }

        // order대로 고른 칸. seconds가 order보다 길면 마지막 칸을 이어 붙인다(한 칸 k_ActFrameSeconds)
        private static Sprite[] Sequence(Sprite[] sheet, int[] order, float seconds)
        {
            int count = Mathf.Max(order.Length, Mathf.RoundToInt(seconds / k_ActFrameSeconds));
            Sprite[] frames = new Sprite[count];

            for (int i = 0; i < count; i++)
            {
                frames[i] = sheet[order[Mathf.Min(i, order.Length - 1)]];
            }

            return frames;
        }

        private Sprite[] ThumpSheet()
        {
            Facing facing = m_area.Wombat.Mover.Facing;
            return facing == Facing.Down ? m_frontThump : facing == Facing.Up ? m_backThump : m_sideThump;
        }

        private Sprite[] SitSheet()
        {
            Facing facing = m_area.Wombat.Mover.Facing;
            return facing == Facing.Down ? m_frontSit : facing == Facing.Up ? m_backSit : m_sideSit;
        }

        private Sprite[] DigSheet()
        {
            Facing facing = m_area.Wombat.Mover.Facing;
            return facing == Facing.Down ? m_frontDig : facing == Facing.Up ? m_backDig : m_sideDig;
        }

        private void PlayNow(Sprite[] frames, float frameSeconds)
        {
            Facing facing = m_area.Wombat.Mover.Facing;
            m_playing = facing == Facing.Down ? m_frontIdle : facing == Facing.Up ? m_backIdle : m_sideIdle;
            m_animator.Play(m_playing, m_idleSeconds);
            m_renderer.flipX = facing == Facing.Left;
            m_animator.Interject(frames, frameSeconds, true);
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
