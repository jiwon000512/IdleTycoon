using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 25 → 설계 27: 밭 칸 하나. 갈아 놓은 흙판(칸 크기, 흙 칸이면 숨김) + 작물 포기 여럿(CropTable sprite_단계, 저마다 발끝으로 깊이 정렬)
    // + 오븐과 같은 타이머·다 익음 표시(작물 위)·빈 밭 화살표(칸 가운데, 웜뱃의 대상이면 끔)
    public sealed class PlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [Tooltip("설계 28: 거름 준 밭의 알갱이(흙판 바로 위)")]
        [SerializeField] private SpriteRenderer m_manure;
        [Tooltip("작물 줄(뒷줄부터). 줄마다 그림이 다르다")]
        [SerializeField] private SpriteRenderer[] m_crops;
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private SpriteRenderer m_readyMark;
        [SerializeField] private SpriteRenderer m_emptyMark;
        [Tooltip("빈 밭 화살표: 오르내리는 높이(유닛, 2칸)와 한 번 바뀌는 간격(초)")]
        [SerializeField] private float m_markBob = 0.05f;
        [SerializeField] private float m_markStepSeconds = 0.4f;

        private float m_markY;
        private bool m_empty;
        private bool m_markHidden;

        // 작물 줄 수(곳 화면이 줄마다 그림을 준다)
        public int Rows => m_crops.Length;

        private void Awake()
        {
            m_markY = m_emptyMark.transform.localPosition.y;
        }

        private void Update()
        {
            if (m_emptyMark.enabled)
            {
                bool up = Mathf.Repeat(Time.time, m_markStepSeconds * 2f) < m_markStepSeconds;
                m_emptyMark.transform.localPosition = new Vector3(0f, m_markY + (up ? m_markBob : 0f), 0f);
            }
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));

            foreach (SpriteRenderer crop in m_crops)
            {
                StartCoroutine(Fx.Bounce(crop.transform));
            }
        }

        // tilled: 밭 칸인가(흙 칸이면 흙판·작물·표식 전부 숨김). crops: 줄마다 지금 단계 그림(빈 밭은 null). growing: 타이머, ripe: 다 익음 표시, fertilized: 거름 알갱이
        public void Show(bool tilled, Sprite[] crops, bool growing, bool ripe, bool fertilized)
        {
            m_body.enabled = tilled;
            m_manure.enabled = tilled && fertilized;

            for (int i = 0; i < m_crops.Length; i++)
            {
                m_crops[i].sprite = crops == null ? null : crops[i];
                m_crops[i].enabled = tilled && crops != null;
            }

            m_timer.enabled = tilled && growing;
            m_readyMark.enabled = tilled && ripe;
            m_empty = tilled && crops == null;
            m_emptyMark.enabled = m_empty && !m_markHidden;
        }

        public void SetProgress(float progress)
        {
            m_timer.sprite = m_timerFrames[Mathf.RoundToInt(Mathf.Clamp01(progress) * (m_timerFrames.Length - 1))];
        }

        public void SetMarkHidden(bool hidden)
        {
            m_markHidden = hidden;
            m_emptyMark.enabled = m_empty && !hidden;
        }
    }
}
