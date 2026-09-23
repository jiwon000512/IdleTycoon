using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 오븐 하나. 굽기 타이머·다 구워 기다리는 개수.
    // 오븐 연출(2026-09-23, Source~/make_oven_fx.py): 굽는 중 = 아궁이 불빛 + 굴뚝 회색 연기, 다 구움 = 불 끔 + 흰 연기, 빈 오븐 = 둘 다 끔. 빵 아이콘은 없다
    // 설계 10: 타이머 자리에 빈 오븐 = 오르내리는 아래 화살표(웜뱃의 대상이면 끔), 다 구움 = 빈 원판 + 식빵(Source~/make_oven_idle.py)
    public sealed class OvenView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        // 굽기 타이머: 진행 0~1을 미리 그린 프레임 중 하나로(Source~/make_bar.py B, 2026-09-23)
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private TextMeshPro m_readyText;
        [SerializeField] private SpriteRenderer m_emptyMark;
        [SerializeField] private SpriteRenderer m_readyMark;
        [Tooltip("빈 오븐 화살표: 오르내리는 높이(유닛, 2칸)와 한 번 바뀌는 간격(초)")]
        [SerializeField] private float m_markBob = 0.05f;
        [SerializeField] private float m_markStepSeconds = 0.4f;
        [SerializeField] private Sprite m_baseBody;
        [SerializeField] private Sprite m_upgradedBody;
        [Tooltip("아궁이 불빛(몸통과 같은 크기·피벗). 흙/벽돌 오븐 세트")]
        [SerializeField] private SpriteRenderer m_fire;
        [SerializeField] private SpriteAnimator m_fireAnimator;
        [SerializeField] private Sprite[] m_baseFire;
        [SerializeField] private Sprite[] m_upgradedFire;
        [Tooltip("굴뚝 연기(피벗 = 굴뚝 입구). 굽는 중 회색, 다 구우면 흰색")]
        [SerializeField] private SpriteRenderer m_smoke;
        [SerializeField] private SpriteAnimator m_smokeAnimator;
        [SerializeField] private Sprite[] m_graySmoke;
        [SerializeField] private Sprite[] m_whiteSmoke;
        [Tooltip("굴뚝 입구 높이(유닛): 흙 오븐, 벽돌 오븐")]
        [SerializeField] private float m_baseChimney = 1.25f;
        [SerializeField] private float m_upgradedChimney = 1.425f;
        [Tooltip("불빛·연기 초당 프레임")]
        [SerializeField] private float m_fxFrameRate = 8f;

        private bool m_upgraded;
        // 흙 오븐 기준 타이머·개수 글자 높이. 벽돌 오븐은 굴뚝이 높은 만큼 올린다
        private float m_timerY;
        private Vector3 m_readyPosition;
        // 벽돌 오븐이 흙 오븐보다 높은 만큼
        private float m_lift;
        private bool m_empty;
        private bool m_markHidden;
        private Sprite[] m_firePlaying;
        private Sprite[] m_smokePlaying;

        private Sprite[] FireFrames => m_upgraded ? m_upgradedFire : m_baseFire;

        private void Awake()
        {
            m_timerY = m_timer.transform.localPosition.y;
            m_readyPosition = m_readyText.transform.localPosition;
        }

        private void Update()
        {
            if (m_emptyMark.enabled)
            {
                bool up = Mathf.Repeat(Time.time, m_markStepSeconds * 2f) < m_markStepSeconds;
                m_emptyMark.transform.localPosition = new Vector3(0f, m_timerY + m_lift + (up ? m_markBob : 0f), 0f);
            }
        }

        public void SetLook(bool upgraded)
        {
            m_upgraded = upgraded;
            m_body.sprite = upgraded ? m_upgradedBody : m_baseBody;
            float chimney = upgraded ? m_upgradedChimney : m_baseChimney;
            m_smoke.transform.localPosition = new Vector3(0f, chimney, 0f);
            m_lift = chimney - m_baseChimney;
            m_timer.transform.localPosition = new Vector3(0f, m_timerY + m_lift, 0f);
            m_readyMark.transform.localPosition = m_timer.transform.localPosition;
            m_readyText.transform.localPosition = m_readyPosition + Vector3.up * m_lift;

            if (m_firePlaying != null)
            {
                Play(m_fire, m_fireAnimator, FireFrames, ref m_firePlaying);
            }
        }

        // 빵을 꺼낼 때 날아가기 시작하는 자리(몸통 가운데)
        public Vector3 BreadPosition => m_body.bounds.center;

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        // 웜뱃이 이 오븐 앞에 있으면 상호작용 버튼이 같은 뜻을 전하니 화살표를 끈다
        public void SetMarkHidden(bool hidden)
        {
            m_markHidden = hidden;
            m_emptyMark.enabled = m_empty && !hidden;
        }

        public void ShowBaking(float progress, int ready)
        {
            bool done = ready > 0;
            m_empty = false;
            m_emptyMark.enabled = false;
            m_timer.enabled = !done;
            m_readyMark.enabled = done;
            SetProgress(progress);
            m_readyText.enabled = done;
            m_readyText.text = $"x{ready}";
            Play(m_fire, m_fireAnimator, done ? null : FireFrames, ref m_firePlaying);
            Play(m_smoke, m_smokeAnimator, done ? m_whiteSmoke : m_graySmoke, ref m_smokePlaying);
        }

        // 손님 동선 설계 v0.2: 진행 표시만 매 프레임(오븐 사건은 초가 바뀔 때만 온다)
        public void SetProgress(float progress)
        {
            m_timer.sprite = m_timerFrames[Mathf.RoundToInt(Mathf.Clamp01(progress) * (m_timerFrames.Length - 1))];
        }

        public void ShowEmpty()
        {
            m_timer.enabled = false;
            m_readyText.enabled = false;
            m_readyMark.enabled = false;
            m_empty = true;
            m_emptyMark.enabled = !m_markHidden;
            Play(m_fire, m_fireAnimator, null, ref m_firePlaying);
            Play(m_smoke, m_smokeAnimator, null, ref m_smokePlaying);
        }

        // 같은 프레임 배열이면 다시 시작하지 않는다(오븐 사건은 1초마다 온다). null이면 끈다
        private void Play(SpriteRenderer renderer, SpriteAnimator animator, Sprite[] frames, ref Sprite[] playing)
        {
            if (frames == playing)
            {
                return;
            }

            playing = frames;
            renderer.enabled = frames != null;

            if (frames != null)
            {
                animator.Play(frames, m_fxFrameRate);
            }
        }
    }
}
