using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 36 뽑기 결과: 뽑기 메인 위에 따로 뜬다. 공개는 돌 깨기(2026-10-01 사용자 「산산조각」, 아트방 시간표):
    // 반짝돌 원석에 금이 세 번 가고(톡 · 톡 · 쩍) 일곱 조각으로 흩어지면 유물이 빛살과 함께 솟고, 카드 틀 없이 이름표 · 이름 · 별 · 효과 →
    // 새 별 → 결과 글과 「유물 보기」 · 「한 번 더」 · 「확인」. 빛살 · 반짝임은 열린 동안 계속. 모든 자리는 프리팹, 여기는 시간만
    public sealed class RelicDrawResultView : UIView
    {
        private struct Cue
        {
            public float At;
            public string Sound;

            public Cue(float at, string sound)
            {
                At = at;
                Sound = sound;
            }
        }

        private const float k_Px = 4f;
        private const float k_MaxStep = 1f / 30f;
        private static readonly float[] k_Cracks = { 0.15f, 0.45f, 0.75f };
        private const float k_ShakeSeconds = 0.1f;
        private const float k_ShakeStep = 0.033f;
        private const float k_GlowSpeed = 18f;
        private const float k_Break = 1f;
        private const float k_PieceSeconds = 0.75f;
        private const float k_PieceSpeed = 520f;
        private const float k_PieceLift = 160f;
        private const float k_Gravity = 700f;
        private const float k_FlashSeconds = 0.3f;
        private const float k_RaysIn = 0.25f;
        private const float k_RaysAlpha = 0.85f;
        private const float k_RaysPeriod = 0.18f;
        private const float k_RaysPeriodTop = 0.12f;
        private const float k_Rise = 1.05f;
        private const float k_RiseSeconds = 0.4f;
        private const float k_Sparks = 1.4f;
        private const float k_SparkCycle = 0.36f;
        private const float k_SparkPhase = 0.37f;
        private const float k_Info = 1.5f;
        private const float k_FadeSeconds = 0.2f;
        private const float k_NewStar = 1.9f;
        private const float k_IconPulse = 2.15f;
        private const float k_PulseSeconds = 0.18f;
        private const float k_PulsePeak = 0.12f;
        private const float k_Foot = 2.3f;
        // 별 단계(1부터)마다 반짝임 수
        private static readonly int[] k_SparkCounts = { 4, 2, 6 };
        private static readonly int[] k_SparkFrames = { 0, 1, 2, 1 };

        private static readonly Cue[] k_Cues =
        {
            new Cue(0.15f, SoundTable.k_DrawTap0),
            new Cue(0.45f, SoundTable.k_DrawTap1),
            new Cue(0.75f, SoundTable.k_DrawCrack),
            new Cue(k_Break, SoundTable.k_DrawBreak),
            new Cue(k_Rise, SoundTable.k_DrawReveal),
            new Cue(k_NewStar, SoundTable.k_StatueBlessed),
        };

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private RectTransform m_stone;
        [SerializeField] private Image m_crack;
        [SerializeField] private Sprite[] m_cracks;
        [SerializeField] private Image m_glow;
        [SerializeField] private Sprite m_glowGold;
        [SerializeField] private Sprite m_glowMint;
        [SerializeField] private Image[] m_pieces;
        // 조각이 날아가는 방향(UI칸, 조각 무게중심 − 금 시작점, 아래가 +)
        [SerializeField] private Vector2[] m_pieceDirs;
        [SerializeField] private Image m_flash;
        [SerializeField] private Image m_rays;
        [SerializeField] private Sprite[] m_raysGold;
        [SerializeField] private Sprite[] m_raysMint;
        [SerializeField] private Image[] m_sparks;
        [SerializeField] private Sprite[] m_sparksGold;
        [SerializeField] private Sprite[] m_sparksMint;
        [SerializeField] private RelicCard m_card;
        [SerializeField] private CanvasGroup m_info;
        [SerializeField] private CanvasGroup[] m_after;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private Button m_viewButton;
        [SerializeField] private TextMeshProUGUI m_viewLabel;
        [SerializeField] private Button m_againButton;
        [SerializeField] private TextMeshProUGUI m_againLabel;
        [SerializeField] private TextMeshProUGUI m_againCost;
        [SerializeField] private Button m_okButton;
        [SerializeField] private TextMeshProUGUI m_okLabel;

        private Vector2 m_panelRest;
        private Vector2 m_stoneRest;
        private Vector2 m_iconRest;
        private Coroutine m_fx;
        private Coroutine m_reveal;

        public event Action CloseRequested;
        public event Action ViewClicked;
        public event Action AgainClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_okButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_viewButton.onClick.AddListener(() => ViewClicked?.Invoke());
            m_againButton.onClick.AddListener(() => AgainClicked?.Invoke());
            m_panelRest = m_panel.anchoredPosition;
            m_stoneRest = m_stone.anchoredPosition;
            m_iconRest = m_card.Icon.rectTransform.anchoredPosition;
            m_root.SetActive(false);
        }

        public void SetLabels(string view, string again, string ok)
        {
            m_viewLabel.text = view;
            m_againLabel.text = again;
            m_okLabel.text = ok;
        }

        // face: 뽑은 유물(새 별까지 든 앞면), stars: 새 별 번호(1부터). 열려 있으면(한 번 더) 그 자리에서 새 돌부터 다시
        public void Show(RelicCard.Data face, int stars, string hint, int cost, bool canDraw)
        {
            if (!m_root.activeSelf)
            {
                m_root.SetActive(true);
                Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
                SoundManager.Instance.Play(SoundTable.k_UiOpen);
            }

            m_hint.text = hint;
            m_againCost.text = cost.ToString();
            m_againButton.interactable = canDraw;
            Run(ref m_reveal, Reveal(face, stars));
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            if (m_reveal != null)
            {
                StopCoroutine(m_reveal);
                m_reveal = null;
            }

            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        private IEnumerator Reveal(RelicCard.Data face, int stars)
        {
            int tier = Mathf.Clamp(stars, 1, m_card.Stars.Length);
            bool mint = tier == 2;
            m_card.Set(face, null, null);
            m_glow.sprite = mint ? m_glowMint : m_glowGold;

            foreach (Image star in m_card.Stars)
            {
                star.rectTransform.localScale = Vector3.one;
            }

            int cue = 0;

            // 여는 프레임이 끊겨도 앞부분(금)을 건너뛰지 않게 한 프레임은 k_MaxStep까지만
            for (float t = 0f; ; t += Mathf.Min(Time.unscaledDeltaTime, k_MaxStep))
            {
                for (; cue < k_Cues.Length && t >= k_Cues[cue].At; cue++)
                {
                    SoundManager.Instance.Play(k_Cues[cue].Sound);
                }

                PoseStone(t);
                PosePieces(t);
                PoseRelic(t, tier, mint ? m_raysMint : m_raysGold, mint ? m_sparksMint : m_sparksGold);
                PoseInfo(t, m_card.Stars[tier - 1]);
                yield return null;
            }
        }

        // 깨지기 전: 금 세 번(그때마다 한 칸 흔들림), 마지막 금부터 빛이 샌다
        private void PoseStone(float t)
        {
            m_stone.gameObject.SetActive(t < k_Break);
            int crack = -1;
            float shake = 0f;

            for (int i = 0; i < k_Cracks.Length; i++)
            {
                if (t < k_Cracks[i])
                {
                    break;
                }

                crack = i;

                if (t < k_Cracks[i] + k_ShakeSeconds)
                {
                    shake = (int)((t - k_Cracks[i]) / k_ShakeStep) % 2 == 1 ? k_Px : -k_Px;
                }
            }

            m_stone.anchoredPosition = m_stoneRest + new Vector2(shake, 0f);
            m_crack.enabled = crack >= 0;
            m_crack.sprite = m_cracks[Mathf.Max(crack, 0)];
            float last = k_Cracks[k_Cracks.Length - 1];
            m_glow.enabled = t >= last;
            m_glow.color = new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin((t - last) * k_GlowSpeed)));
        }

        // 깨짐: 조각마다 제 방향으로 튀고(위로 조금 더) 중력에 떨어지며 사라진다. 번쩍은 금 시작점에서
        private void PosePieces(float t)
        {
            float dt = t - k_Break;
            bool flying = dt >= 0f && dt < k_PieceSeconds;

            for (int i = 0; i < m_pieces.Length; i++)
            {
                m_pieces[i].enabled = flying;

                if (!flying)
                {
                    continue;
                }

                Vector2 v = m_pieceDirs[i].normalized * k_PieceSpeed - new Vector2(0f, k_PieceLift);
                Vector2 down = v * dt + new Vector2(0f, k_Gravity * dt * dt);
                m_pieces[i].rectTransform.anchoredPosition = m_stoneRest + Snap(new Vector2(down.x, -down.y));
                m_pieces[i].color = new Color(1f, 1f, 1f, 1f - dt / k_PieceSeconds);
            }

            m_flash.enabled = dt >= 0f && dt < k_FlashSeconds;
            m_flash.color = new Color(1f, 1f, 1f, 1f - dt / k_FlashSeconds);
        }

        // 유물이 금 시작점에서 제자리로 솟고(뒤에 빛살), 둘레에 반짝임. 별 단계: ★2 민트 · 최고 단계는 빛살이 빠르다
        private void PoseRelic(float t, int tier, Sprite[] rays, Sprite[] sparks)
        {
            RectTransform icon = m_card.Icon.rectTransform;
            icon.gameObject.SetActive(t >= k_Rise);
            float k = Mathf.Clamp01((t - k_Rise) / k_RiseSeconds);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            Vector2 from = m_flash.rectTransform.anchoredPosition;
            icon.anchoredPosition = m_iconRest + Snap(new Vector2(0f, (from.y - m_iconRest.y) * (1f - e)));
            icon.localScale = Vector3.one * Pulse(t - k_IconPulse);

            m_rays.enabled = t >= k_Break;
            float period = tier == m_card.Stars.Length ? k_RaysPeriodTop : k_RaysPeriod;
            m_rays.sprite = rays[(int)(t / period) % rays.Length];
            m_rays.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - k_Break) / k_RaysIn) * k_RaysAlpha);
            m_rays.rectTransform.anchoredPosition = icon.anchoredPosition;

            for (int i = 0; i < m_sparks.Length; i++)
            {
                bool on = t >= k_Sparks && i < k_SparkCounts[tier - 1];
                m_sparks[i].enabled = on;

                if (on)
                {
                    float cycle = (t - k_Sparks) / k_SparkCycle + i * k_SparkPhase;
                    m_sparks[i].sprite = sparks[k_SparkFrames[(int)(cycle * k_SparkFrames.Length) % k_SparkFrames.Length]];
                    m_sparks[i].SetNativeSize();
                }
            }
        }

        // 이름표 · 이름 · 별 · 효과가 나타나고, 새 별이 켜지며 부푼 뒤 결과 글 · 버튼
        private void PoseInfo(float t, Image newStar)
        {
            m_info.alpha = Mathf.Clamp01((t - k_Info) / k_FadeSeconds);
            newStar.color = t >= k_NewStar ? Color.white : RelicCard.k_Shadow;
            newStar.rectTransform.localScale = Vector3.one * Pulse(t - k_NewStar);
            float after = Mathf.Clamp01((t - k_Foot) / k_FadeSeconds);

            foreach (CanvasGroup group in m_after)
            {
                group.alpha = after;
                group.blocksRaycasts = t >= k_Foot;
            }
        }

        private static float Pulse(float t)
        {
            return t > 0f && t < k_PulseSeconds ? 1f + k_PulsePeak * Mathf.Sin(t / k_PulseSeconds * Mathf.PI) : 1f;
        }

        // 움직임은 4px(UI 한 칸) 단위
        private static Vector2 Snap(Vector2 v)
        {
            return new Vector2(Mathf.Round(v.x / k_Px) * k_Px, Mathf.Round(v.y / k_Px) * k_Px);
        }

        private void Run(ref Coroutine slot, IEnumerator routine)
        {
            if (slot != null)
            {
                StopCoroutine(slot);
            }

            slot = StartCoroutine(routine);
        }
    }
}
