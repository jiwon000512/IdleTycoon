using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 오븐 하나. 굽기 타이머·다 구워 기다리는 개수.
    // 오븐 연출(2026-09-23, Source~/make_oven_fx.py): 굽는 중 = 아궁이 불빛 + 굴뚝 회색 연기, 다 구움 = 불 끔 + 흰 연기, 빈 오븐 = 둘 다 끔. 빵 아이콘은 없다
    public sealed class OvenView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        // 굽기 타이머: 진행 0~1을 미리 그린 프레임 중 하나로(Source~/make_bar.py B, 2026-09-23)
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private TextMeshPro m_readyText;
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
        private float m_readyY;
        private Sprite[] m_firePlaying;
        private Sprite[] m_smokePlaying;

        private Sprite[] FireFrames => m_upgraded ? m_upgradedFire : m_baseFire;

        private void Awake()
        {
            m_timerY = m_timer.transform.localPosition.y;
            m_readyY = m_readyText.transform.localPosition.y;
        }

        public void SetLook(bool upgraded)
        {
            m_upgraded = upgraded;
            m_body.sprite = upgraded ? m_upgradedBody : m_baseBody;
            float chimney = upgraded ? m_upgradedChimney : m_baseChimney;
            m_smoke.transform.localPosition = new Vector3(0f, chimney, 0f);
            m_timer.transform.localPosition = new Vector3(0f, m_timerY + chimney - m_baseChimney, 0f);
            m_readyText.transform.localPosition = new Vector3(0f, m_readyY + chimney - m_baseChimney, 0f);

            if (m_firePlaying != null)
            {
                Play(m_fire, m_fireAnimator, FireFrames, ref m_firePlaying);
            }
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        public void ShowBaking(float progress, int ready)
        {
            bool done = ready > 0;
            m_timer.enabled = !done;
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
