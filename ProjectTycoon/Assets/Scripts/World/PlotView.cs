using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 25: 밭 하나. 흙 틀 + 작물 그림(CropTable sprite_단계). 자라는 동안 오븐과 같은 타이머, 빈 밭은 오븐과 같은 오르내리는 화살표(웜뱃의 대상이면 끔)
    public sealed class PlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_crop;
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private SpriteRenderer m_emptyMark;
        [Tooltip("빈 밭 화살표: 오르내리는 높이(유닛, 2칸)와 한 번 바뀌는 간격(초)")]
        [SerializeField] private float m_markBob = 0.05f;
        [SerializeField] private float m_markStepSeconds = 0.4f;

        private float m_markY;
        private bool m_empty;
        private bool m_markHidden;

        // 편집 모드 외곽선을 붙일 몸체
        public SpriteRenderer Body => m_body;

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
            StartCoroutine(Fx.Bounce(m_crop.transform));
        }

        // crop: 지금 단계 그림(빈 밭은 null). growing: 타이머를 보인다
        public void Show(Sprite crop, bool growing)
        {
            m_crop.sprite = crop;
            m_crop.enabled = crop != null;
            m_timer.enabled = growing;
            m_empty = crop == null;
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
