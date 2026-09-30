using System.Collections;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 25 → 설계 27: 밭 칸 하나. 갈아 놓은 흙판(칸 크기, 흙 칸이면 숨김) + 작물 포기 여럿(CropTable sprite_단계, 저마다 발끝으로 깊이 정렬)
    // + 진행 게이지·다 익음 표시(작물 위)·빈 밭 화살표(칸 가운데, 웜뱃의 대상이면 끔).
    // 무럭무럭(2026-09-30 사용자 선택 C): 단계가 바뀌면 뒷줄부터 한 박자씩 쑥 늘어났다 자리 잡고 잎 알갱이가 튄다(익을 땐 금빛 + 익음 표시가 한 번 부풂).
    // 자라는 동안 줄마다 바람이 지나가듯 한 칸씩 기울고, 익어 있는 동안 이삭 위에 반짝임이 뜬다
    public sealed class PlotView : MonoBehaviour
    {
        // 늘어남: 밑변 기준, 한 칸(0.08초)씩 끊어 가는 세로 · 가로 배율. 칸 배수로 맞춘다
        private static readonly float[] k_GrowY = { 0.55f, 1.18f, 0.92f };
        private static readonly float[] k_GrowX = { 1f, 0.96f, 1.02f };
        private static readonly float[] k_Pulse = { 1f, 1.18f, 1f };
        private const float k_GrowStep = 0.08f;
        // 1 칸 = 2px(PPU 80이라 스프라이트 픽셀 2개)
        private const float k_CellPixels = 2f;
        // 잎 · 금빛 알갱이(거두기 낟알과 같은 네모): 줄마다 두 빛깔 × 4
        private static readonly Color k_Leaf = new Color32(144, 156, 96, 255);
        private static readonly Color k_LeafDark = new Color32(108, 120, 72, 255);
        private static readonly Color k_Gold = new Color32(240, 200, 96, 255);
        private static readonly Color k_GoldLight = new Color32(252, 236, 180, 255);
        private const int k_BitCount = 4;
        private const float k_BitSpread = 1.2f;
        private const float k_BitLift = 2.6f;
        private const float k_BitGravity = 10f;
        private const float k_BitSeconds = 0.45f;
        private const int k_BitCells = 4;
        private const float k_BitRise = 0.3f;
        private const int k_BitOrder = 1000;
        // 바람: 줄마다 sin((시각 × 속도 − 줄 × 어긋남) × π)가 문턱을 넘으면 오른쪽 · 왼쪽으로 기운 그림
        private const float k_WindSpeed = 2.2f;
        private const float k_WindRowLag = 0.9f;
        private const float k_WindThreshold = 0.55f;
        // 반짝임: 익어 있는 동안 이 간격마다 무작위 줄 · 가로 자리(밭 가운데 ±)의 이삭 위에, 큰 + → 작은 + 한 장씩
        private const float k_SparkleEvery = 0.35f;
        private const float k_SparkleFrameSeconds = 0.2f;
        private const float k_SparkleSpan = 1.25f;
        private const float k_SparkleRise = 0.55f;

        [SerializeField] private SpriteRenderer m_body;
        [Tooltip("설계 28: 거름 준 밭의 알갱이(흙판 바로 위)")]
        [SerializeField] private SpriteRenderer m_manure;
        [Tooltip("작물 줄(뒷줄부터). 줄마다 그림이 다르다")]
        [SerializeField] private SpriteRenderer[] m_crops;
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private SpriteRenderer m_readyMark;
        [SerializeField] private SpriteRenderer m_emptyMark;
        [Tooltip("반짝임 두 개(번갈아 쓴다)와 그림 두 장(큰 + · 작은 +)")]
        [SerializeField] private SpriteRenderer[] m_sparkles;
        [SerializeField] private Sprite[] m_sparkleFrames;
        [Tooltip("빈 밭 화살표: 오르내리는 높이(유닛, 2칸)와 한 번 바뀌는 간격(초)")]
        [SerializeField] private float m_markBob = 0.05f;
        [SerializeField] private float m_markStepSeconds = 0.4f;

        private float m_markY;
        private bool m_empty;
        private bool m_markHidden;
        private bool m_ripe;
        // 줄마다 지금 그림 {기본, 왼쪽으로 기운, 오른쪽으로 기운} 또는 {기본}(흔들리지 않음)과 늘어나는 중인가
        private Sprite[][] m_rows;
        private Coroutine[] m_growing;
        private float m_sparkleLeft;
        private int m_nextSparkle;
        private Sprite m_square;

        // 작물 줄 수(곳 화면이 줄마다 그림을 준다)
        public int Rows => m_crops.Length;

        private void Awake()
        {
            m_markY = m_emptyMark.transform.localPosition.y;
            m_growing = new Coroutine[m_crops.Length];
        }

        private void Update()
        {
            if (m_emptyMark.enabled)
            {
                bool up = Mathf.Repeat(Time.time, m_markStepSeconds * 2f) < m_markStepSeconds;
                m_emptyMark.transform.localPosition = new Vector3(0f, m_markY + (up ? m_markBob : 0f), 0f);
            }

            Sway();

            if (m_ripe && (m_sparkleLeft -= Time.deltaTime) <= 0f)
            {
                m_sparkleLeft = k_SparkleEvery;
                StartCoroutine(Sparkle(m_sparkles[m_nextSparkle++ % m_sparkles.Length]));
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

        // tilled: 밭 칸인가(흙 칸이면 흙판·작물·표식 전부 숨김). crops: 줄마다 지금 단계 그림 {기본, 왼쪽, 오른쪽} 또는 {기본}(빈 밭은 null).
        // growing: 게이지, ripe: 다 익음 표시, fertilized: 거름 알갱이. grew: 방금 단계가 올랐다(뒷줄부터 늘어나며 새 그림으로)
        public void Show(bool tilled, Sprite[][] crops, bool growing, bool ripe, bool fertilized, bool grew)
        {
            m_body.enabled = tilled;
            m_manure.enabled = tilled && fertilized;
            m_rows = tilled ? crops : null;

            for (int i = 0; i < m_crops.Length; i++)
            {
                if (m_growing[i] != null)
                {
                    StopCoroutine(m_growing[i]);
                    m_growing[i] = null;
                    m_crops[i].transform.localScale = Vector3.one;
                }

                if (grew && m_rows != null)
                {
                    m_growing[i] = StartCoroutine(Grow(i, ripe));
                    continue;
                }

                m_crops[i].sprite = m_rows == null ? null : m_rows[i][0];
                m_crops[i].enabled = m_rows != null;
            }

            m_timer.enabled = tilled && growing;
            m_readyMark.enabled = tilled && ripe;
            m_ripe = tilled && ripe;

            foreach (SpriteRenderer sparkle in m_sparkles)
            {
                sparkle.enabled &= m_ripe;
            }

            if (grew && m_ripe)
            {
                StartCoroutine(Stretch(m_readyMark.transform, m_readyMark.sprite, k_Pulse, k_Pulse));
            }

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

        // 줄 하나: 제 차례(뒷줄부터 한 박자씩)가 되면 새 그림으로 바뀌며 알갱이가 튀고 쑥 늘어났다 자리 잡는다. 그동안은 옛 그림 그대로, 흔들리지 않는다
        private IEnumerator Grow(int row, bool ripe)
        {
            yield return new WaitForSeconds(row * k_GrowStep);
            SpriteRenderer crop = m_crops[row];
            crop.sprite = m_rows[row][0];
            crop.enabled = true;
            m_square = m_square != null ? m_square : Fx.NewSquare();
            Vector3 at = crop.transform.position + Vector3.up * k_BitRise;
            StartCoroutine(Fx.Burst(transform, m_square, at, k_BitCount, k_BitSpread, k_BitLift, k_BitGravity, k_BitSeconds, k_BitCells, ripe ? k_Gold : k_Leaf, k_BitOrder));
            StartCoroutine(Fx.Burst(transform, m_square, at, k_BitCount, k_BitSpread, k_BitLift, k_BitGravity, k_BitSeconds, k_BitCells, ripe ? k_GoldLight : k_LeafDark, k_BitOrder));
            yield return Stretch(crop.transform, crop.sprite, k_GrowY, k_GrowX);
            m_growing[row] = null;
        }

        // 배율을 한 칸(k_GrowStep)씩 끊어 바꾼 뒤 1로. 그림이 칸 배수 크기로 보이게 배율을 칸 수에 맞춘다
        private static IEnumerator Stretch(Transform target, Sprite sprite, float[] ys, float[] xs)
        {
            float width = sprite.rect.width / k_CellPixels;
            float height = sprite.rect.height / k_CellPixels;

            for (int i = 0; i < ys.Length; i++)
            {
                target.localScale = new Vector3(Mathf.Round(width * xs[i]) / width, Mathf.Round(height * ys[i]) / height, 1f);
                yield return new WaitForSeconds(k_GrowStep);
            }

            target.localScale = Vector3.one;
        }

        // 바람: 뒷줄부터 차례로 오른쪽 · 왼쪽으로 한 칸 기운다(흔들림 그림이 있는 줄만, 늘어나는 동안은 쉰다)
        private void Sway()
        {
            if (m_rows == null)
            {
                return;
            }

            for (int i = 0; i < m_crops.Length; i++)
            {
                if (m_growing[i] != null || m_rows[i].Length < 3)
                {
                    continue;
                }

                float wind = Mathf.Sin((Time.time * k_WindSpeed - i * k_WindRowLag) * Mathf.PI);
                m_crops[i].sprite = m_rows[i][wind > k_WindThreshold ? 2 : wind < -k_WindThreshold ? 1 : 0];
            }
        }

        private IEnumerator Sparkle(SpriteRenderer sparkle)
        {
            Transform row = m_crops[Random.Range(0, m_crops.Length)].transform;
            sparkle.transform.position = row.position + new Vector3(Random.Range(-k_SparkleSpan, k_SparkleSpan), k_SparkleRise, 0f);
            sparkle.enabled = true;

            foreach (Sprite frame in m_sparkleFrames)
            {
                sparkle.sprite = frame;
                yield return new WaitForSeconds(k_SparkleFrameSeconds);
            }

            sparkle.enabled = false;
        }
    }
}
