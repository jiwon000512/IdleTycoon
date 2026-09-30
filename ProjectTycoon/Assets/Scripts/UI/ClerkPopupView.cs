using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 21 점원 화면: 편집 버튼 아래 점원 버튼(늘 보임) → 팝업. 팝업은 세 상태를 한 프리팹에 둔다:
    // 목록(가게 탭 + 줄: 자리 목록 또는 후보 목록 + 바닥 글/버튼) · 목록 위 「고용할까」 작은 창 · 협상(위 장면 = 웜뱃·후보 옆모습 + 말풍선, 아래 = 타이밍 바).
    // 표시와 입력 이벤트만, 규칙은 ClerkPresenter. 그림 경로(Resources)는 여기서 읽는다
    public sealed class ClerkPopupView : UIView
    {
        // 줄 하나(시안 D1): 초상(경로, null = 빈 자리 점선 틀) · 배지 · 이름 · 자리 알약(null = 없음) · 두 칸 표(Skill < 0 = 없음) · 버튼
        public sealed class RowData
        {
            public string IconPath;
            public ClerkBadge Badge;
            public string BadgeText;
            public string Name;
            public string Slot;
            public string SkillHeader;
            public string WageHeader;
            public int Skill = -1;
            public string Wage;
            public string Button;
            public bool Enabled;
        }

        private const float k_ToastSeconds = 2.5f;
        // 줄 목록은 이 수까지 보이고 그 아래는 스크롤, 패널 높이는 보이는 줄 수에 맞춘다(2026-09-30 사용자). 요약 줄(점원 수 · 월급 합)은 바닥에 고정
        private const int k_VisibleRows = 3;
        // 협상 장면의 웜뱃·후보 그림: 스프라이트 1px = 캔버스 2px(둘 다 같은 배율, 시트 칸 여백 포함)
        private const float k_ScenePixel = 2f;
        // 협상 결과: 이긴 쪽이 두 번 폴짝(캔버스 px, 4의 배수로 움직인다) · 맞은 구간이 깜빡
        private const float k_HopSeconds = 0.5f;
        private const float k_HopHeight = 24f;
        private const float k_BlinkSeconds = 0.07f;

        // 점원 버튼 배지: 딴짓 수는 주황, 월급 모자람은 빨강(팔레트 「부족」)
        private static readonly Color k_BadgeIdling = new Color32(0xD8, 0x78, 0x48, 255);
        private static readonly Color k_BadgeShort = new Color32(0xA6, 0x4B, 0x3C, 255);

        [SerializeField] private Button m_openButton;
        [Tooltip("점원 버튼 오른쪽 위 배지")]
        [SerializeField] private Image m_buttonBadge;
        [SerializeField] private TextMeshProUGUI m_buttonBadgeText;
        [SerializeField] private GameObject m_root;
        [Tooltip("등장·퇴장: 팝업 전체가 나타나고 패널이 올라온다(UiFx)")]
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private CanvasGroup m_askGroup;
        [SerializeField] private RectTransform m_askBox;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("목록 상태: 가게 탭 줄 · 요약 · 줄 목록 · 바닥(글 또는 버튼)")]
        [SerializeField] private GameObject m_list;
        [SerializeField] private GameObject m_tabRow;
        [SerializeField] private TextMeshProUGUI m_tabLabel;
        [SerializeField] private TextMeshProUGUI m_summaryLeft;
        [SerializeField] private TextMeshProUGUI m_summaryRight;
        [Tooltip("요약 오른쪽: 글(m_summaryRight) + 코인 + 값을 담은 묶음과 그 값")]
        [SerializeField] private GameObject m_summaryValueGroup;
        [SerializeField] private TextMeshProUGUI m_summaryValue;
        [SerializeField] private RectTransform m_summaryRow;
        [Tooltip("요약 줄 가운데: 월급날 글 + 차오르는 게이지(안쪽 폭은 캔버스 px)")]
        [SerializeField] private GameObject m_payday;
        [SerializeField] private TextMeshProUGUI m_paydayLabel;
        [SerializeField] private RectTransform m_paydayFill;
        [SerializeField] private float m_paydayWidth;
        [Tooltip("월급이 모자랄 때의 게이지 채움")]
        [SerializeField] private Sprite m_paydayShortFill;
        [SerializeField] private RectTransform m_rows;
        [SerializeField] private ClerkRowView m_rowTemplate;
        [Tooltip("후보 줄: 상태 배지 없이 초상 틀이 크고 카드 높이 가운데")]
        [SerializeField] private ClerkRowView m_candidateRowTemplate;
        [SerializeField] private TextMeshProUGUI m_foot;
        [SerializeField] private Button m_footButton;
        [SerializeField] private TextMeshProUGUI m_footButtonLabel;
        [SerializeField] private TextMeshProUGUI m_footButtonCost;
        [Tooltip("고용할까 작은 창")]
        [SerializeField] private GameObject m_ask;
        [SerializeField] private Image m_askIcon;
        [SerializeField] private TextMeshProUGUI m_askTitle;
        [SerializeField] private ClerkStatView m_askStat;
        [SerializeField] private Button m_askPlain;
        [SerializeField] private TextMeshProUGUI m_askPlainLabel;
        [SerializeField] private Button m_askNegotiate;
        [SerializeField] private TextMeshProUGUI m_askNegotiateLabel;
        [SerializeField] private Button m_askCancel;
        [Tooltip("고용할까 창(명함 카드): 이름 · 자리 이름표 · 협상 안내")]
        [SerializeField] private TextMeshProUGUI m_askName;
        [SerializeField] private TextMeshProUGUI m_askSlot;
        [SerializeField] private TextMeshProUGUI m_askHint;
        [Tooltip("협상 상태")]
        [SerializeField] private GameObject m_nego;
        [SerializeField] private Image m_negoWombat;
        [SerializeField] private Image m_negoCandidate;
        [Tooltip("후보 말풍선. 웜뱃은 말하지 않아 말풍선이 없다")]
        [SerializeField] private TextMeshProUGUI m_negoRight;
        [SerializeField] private TextMeshProUGUI m_negoHint;
        [SerializeField] private RectTransform m_marker;
        [Tooltip("바 구간 5개(빨강·흰·초록·흰·빨강). 폭은 SetZones가 정한다")]
        [SerializeField] private RectTransform[] m_zones;
        [SerializeField] private Button m_tapArea;
        [Tooltip("탭 버튼(「지금!」): 화면 탭과 같다")]
        [SerializeField] private Button m_nowButton;
        [SerializeField] private TextMeshProUGUI m_nowLabel;
        [Tooltip("해고 토스트")]
        [SerializeField] private GameObject m_toast;
        [SerializeField] private TextMeshProUGUI m_toastText;
        [SerializeField] private TextMeshProUGUI m_toastReason;

        private readonly List<ClerkRowView> m_rowViews = new List<ClerkRowView>();
        private readonly List<ClerkRowView> m_candidateViews = new List<ClerkRowView>();
        private readonly Dictionary<string, Sprite> m_sprites = new Dictionary<string, Sprite>();

        private Image m_paydayFillImage;
        private Sprite m_paydayFillSprite;
        private Vector2 m_panelRest;
        private Vector2 m_askRest;
        // 프리팹 값: 패널 높이(협상 상태) · 줄 창 위쪽 높이(제목 · 탭) · 줄 창 아래 높이(요약 줄 또는 바닥 버튼) · 줄 높이 · 줄 사이
        private float m_panelFull;
        private float m_chrome;
        private float m_viewportBottom;
        private float m_rowHeight;
        private float m_rowGap;
        private bool m_open;
        private bool m_askOpen;
        private Coroutine m_rootFx;
        private Coroutine m_askFx;
        private Coroutine m_toastFx;

        public event Action OpenClicked;
        public event Action CloseClicked;
        public event Action<int> RowButtonClicked;
        public event Action FootClicked;
        public event Action AskPlainClicked;
        public event Action AskNegotiateClicked;
        public event Action AskCancelClicked;
        public event Action Tapped;
        // 매 프레임(초). 팝업이 닫혀 있어도 온다(버튼 배지)
        public event Action<float> Ticked;

        public bool IsVisible => m_open;

        private void Awake()
        {
            m_openButton.onClick.AddListener(() => OpenClicked?.Invoke());
            m_dim.onClick.AddListener(() => CloseClicked?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseClicked?.Invoke());
            m_footButton.onClick.AddListener(() => FootClicked?.Invoke());
            m_askPlain.onClick.AddListener(() => AskPlainClicked?.Invoke());
            m_askNegotiate.onClick.AddListener(() => AskNegotiateClicked?.Invoke());
            m_askCancel.onClick.AddListener(() => AskCancelClicked?.Invoke());
            m_tapArea.onClick.AddListener(() => Tapped?.Invoke());
            m_nowButton.onClick.AddListener(() => Tapped?.Invoke());
            m_rowTemplate.gameObject.SetActive(false);
            m_candidateRowTemplate.gameObject.SetActive(false);
            m_panelRest = m_panel.anchoredPosition;
            m_askRest = m_askBox.anchoredPosition;
            RectTransform viewport = (RectTransform)m_rows.parent;
            RectTransform body = (RectTransform)m_list.transform.parent;
            m_panelFull = m_panel.sizeDelta.y;
            m_viewportBottom = viewport.offsetMin.y;
            m_chrome = -body.sizeDelta.y - viewport.offsetMax.y;
            m_rowHeight = m_rowTemplate.GetComponent<LayoutElement>().preferredHeight;
            m_rowGap = m_rows.GetComponent<VerticalLayoutGroup>().spacing;
            m_paydayFillImage = m_paydayFill.GetComponent<Image>();
            m_paydayFillSprite = m_paydayFillImage.sprite;
            m_root.SetActive(false);
            m_toast.SetActive(false);
            m_buttonBadge.gameObject.SetActive(false);
        }

        private void Update()
        {
            Ticked?.Invoke(Time.deltaTime);
        }

        // text가 null이면 숨긴다. alert = 월급 모자람(빨강), 아니면 딴짓 수(주황)
        public void SetButtonBadge(string text, bool alert)
        {
            m_buttonBadge.gameObject.SetActive(text != null);
            m_buttonBadge.color = alert ? k_BadgeShort : k_BadgeIdling;
            m_buttonBadgeText.text = text ?? string.Empty;
        }

        // 자리 목록의 줄 하나가 옅어진다(해고). 이어 오는 ShowList의 내용은 옅어진 뒤에 그려진다
        public void PlayRowOut(int index)
        {
            m_rowViews[index].Leave();
        }

        // 편집 모드에서는 점원 버튼을 숨긴다
        public void SetButtonVisible(bool visible)
        {
            m_openButton.gameObject.SetActive(visible);
        }

        public void Open()
        {
            if (m_open)
            {
                return;
            }

            m_open = true;
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
            m_root.SetActive(true);
            Run(ref m_rootFx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
        }

        public void Close()
        {
            if (!m_open)
            {
                return;
            }

            m_open = false;
            SoundManager.Instance.Play(SoundTable.k_UiClose);
            Run(ref m_rootFx, UiFx.Vanish(m_rootGroup, m_root));
        }

        private void Run(ref Coroutine slot, IEnumerator routine)
        {
            if (slot != null)
            {
                StopCoroutine(slot);
            }

            slot = StartCoroutine(routine);
        }

        // 목록이 바뀐 순간(열기 · 자리 ↔ 후보 · 새 후보)에만 부른다. 값만 고쳐 그릴 때는 부르지 않는다
        public void PlayRows()
        {
            int shown = 0;

            foreach (ClerkRowView row in m_rows.GetComponentsInChildren<ClerkRowView>())
            {
                row.Appear(shown++ * UiFx.k_RowGap);
            }
        }

        // 목록 상태(고용할까 창은 건드리지 않는다). tab이 null이면 후보 목록: 탭 줄을 숨기고 후보 줄 템플릿을 쓴다. 요약은 왼쪽 글 · 오른쪽 글 + 코인 값. footButton이 null이면 바닥은 글, 아니면 버튼(값 칸 포함)
        public void ShowList(string title, string tab, string summaryLeft, string summaryRight, string summaryValue, IReadOnlyList<RowData> rows, string foot, string footButton, string footCost, bool footEnabled)
        {
            m_title.text = title;
            m_list.SetActive(true);
            m_nego.SetActive(false);
            m_tabRow.SetActive(tab != null);
            m_tabLabel.text = tab ?? string.Empty;
            m_summaryLeft.gameObject.SetActive(summaryLeft != null);
            m_summaryLeft.text = summaryLeft ?? string.Empty;
            m_summaryValueGroup.SetActive(summaryRight != null);
            m_summaryRight.text = summaryRight ?? string.Empty;
            m_summaryValue.text = summaryValue ?? string.Empty;
            m_summaryRow.gameObject.SetActive(summaryLeft != null || summaryRight != null);
            m_foot.gameObject.SetActive(footButton == null);
            m_foot.text = foot ?? string.Empty;
            m_footButton.gameObject.SetActive(footButton != null);
            m_footButtonLabel.text = footButton ?? string.Empty;
            m_footButtonCost.text = footCost ?? string.Empty;
            m_footButton.interactable = footEnabled;

            bool candidates = tab == null;
            List<ClerkRowView> views = candidates ? m_candidateViews : m_rowViews;
            foreach (ClerkRowView other in candidates ? m_rowViews : m_candidateViews)
            {
                other.gameObject.SetActive(false);
            }

            while (views.Count < rows.Count)
            {
                ClerkRowView row = Instantiate(candidates ? m_candidateRowTemplate : m_rowTemplate, m_rows);
                row.ButtonClicked += Row_ButtonClicked;
                views.Add(row);
            }

            for (int i = 0; i < views.Count; i++)
            {
                if (i >= rows.Count)
                {
                    views[i].gameObject.SetActive(false);
                    continue;
                }

                views[i].Show(rows[i], Load(rows[i].IconPath));
            }

            SetListHeight(rows.Count);
        }

        // 줄 창은 k_VisibleRows줄까지 보이고 그 아래는 스크롤. 바닥(요약 줄 또는 후보 목록의 버튼)은 프리팹 높이 그대로
        private void SetListHeight(int rows)
        {
            int shown = Mathf.Clamp(rows, 1, k_VisibleRows);
            float list = shown * m_rowHeight + (shown - 1) * m_rowGap;
            m_panel.sizeDelta = new Vector2(m_panel.sizeDelta.x, m_chrome + list + m_viewportBottom);
            m_rows.anchoredPosition = Vector2.zero;
        }

        // 월급날 게이지. label이 null이면 숨긴다(점원이 없다 · 후보 목록). 모자라면(low) 채움이 빨강
        public void SetPayday(string label, float progress, bool low)
        {
            m_paydayFillImage.sprite = low ? m_paydayShortFill : m_paydayFillSprite;
            m_payday.SetActive(label != null);
            m_paydayLabel.text = label ?? string.Empty;
            m_paydayFill.sizeDelta = new Vector2(Mathf.Round(m_paydayWidth * Mathf.Clamp01(progress)), m_paydayFill.sizeDelta.y);
        }

        // 고용할까 · 해고할까 창. 취소는 오른쪽 위 닫기 버튼(AskCancelClicked). 이미 떠 있으면 글만 고친다
        public void ShowAsk(string iconPath, string title, string name, string slot, string skillHeader, string wageHeader, int skill, string wage, string plain, string negotiate, string hint)
        {
            if (!m_askOpen)
            {
                m_askOpen = true;
                m_ask.SetActive(true);
                Run(ref m_askFx, UiFx.Appear(m_askGroup, m_askBox, m_askRest, 0f));
            }

            m_askIcon.sprite = Load(iconPath);
            m_askTitle.text = title;
            m_askName.text = name;
            m_askSlot.text = slot;
            m_askStat.Show(skillHeader, wageHeader, skill, wage);
            m_askPlainLabel.text = plain;
            m_askNegotiateLabel.text = negotiate;
            m_askHint.text = hint;
        }

        public void HideAsk()
        {
            if (m_askOpen)
            {
                m_askOpen = false;
                Run(ref m_askFx, UiFx.Vanish(m_askGroup, m_ask));
            }
        }

        // 협상 상태. candidatePath = 후보 옆모습 시트(첫 칸을 좌우 뒤집어 왼쪽을 본다)
        public void ShowNegotiation(string title, string hint, string now, string candidatePath, string bubble)
        {
            m_title.text = title;
            m_list.SetActive(false);
            HideAsk();
            m_nego.SetActive(true);
            m_panel.sizeDelta = new Vector2(m_panel.sizeDelta.x, m_panelFull);
            m_negoHint.text = hint;
            m_nowLabel.text = now;
            m_negoCandidate.sprite = Load(candidatePath);
            NativeSize(m_negoWombat);
            NativeSize(m_negoCandidate);
            SetBubble(bubble);
            SetMarker(0f);
        }

        private static void NativeSize(Image image)
        {
            image.SetNativeSize();
            image.rectTransform.sizeDelta *= k_ScenePixel;
        }

        // 협상 결과: 월급이 그대로거나 내려가면 good(웜뱃이 폴짝), 오르면 후보가 폴짝. 리본 글이 결과로 바뀌고 표시가 선 구간이 깜빡인다
        public void PlayResult(bool good, string ribbon)
        {
            SoundManager.Instance.Play(good ? SoundTable.k_NegoGood : SoundTable.k_NegoBad);
            m_negoHint.text = ribbon;
            StartCoroutine(HopRoutine((good ? m_negoWombat : m_negoCandidate).rectTransform));
            float at = m_marker.anchorMin.x;

            foreach (RectTransform zone in m_zones)
            {
                if (at >= zone.anchorMin.x && at <= zone.anchorMax.x)
                {
                    StartCoroutine(BlinkRoutine(zone.GetComponent<Image>()));
                    break;
                }
            }
        }

        private static IEnumerator HopRoutine(RectTransform target)
        {
            Vector2 rest = target.anchoredPosition;

            for (float t = 0f; t < k_HopSeconds; t += Time.unscaledDeltaTime)
            {
                float height = Mathf.Abs(Mathf.Sin(t / k_HopSeconds * 2f * Mathf.PI)) * k_HopHeight;
                target.anchoredPosition = rest + Vector2.up * (Mathf.Round(height / 4f) * 4f);
                yield return null;
            }

            target.anchoredPosition = rest;
        }

        private static IEnumerator BlinkRoutine(Image zone)
        {
            for (int i = 0; i < 4; i++)
            {
                zone.enabled = i % 2 == 1;
                yield return new WaitForSecondsRealtime(k_BlinkSeconds);
            }

            zone.enabled = true;
        }

        public void SetBubble(string text)
        {
            m_negoRight.text = text;
        }

        // 0~1
        public void SetMarker(float t)
        {
            m_marker.anchorMin = new Vector2(t, 0f);
            m_marker.anchorMax = new Vector2(t, 1f);
            m_marker.anchoredPosition = Vector2.zero;
        }

        // 바 구간: 가운데에서 초록 반폭 g, 흰 반폭 w(바 폭 = 1)
        public void SetZones(float green, float white)
        {
            float[] edges = { 0f, 0.5f - white, 0.5f - green, 0.5f + green, 0.5f + white, 1f };

            for (int i = 0; i < m_zones.Length; i++)
            {
                m_zones[i].anchorMin = new Vector2(edges[i], 0f);
                m_zones[i].anchorMax = new Vector2(edges[i + 1], 1f);
                m_zones[i].offsetMin = Vector2.zero;
                m_zones[i].offsetMax = Vector2.zero;
            }
        }

        // 두 줄: 무슨 일(「진흙 그만둠」) · 까닭(「월급을 못 냈다」). 한 글에 정보 하나
        public void ShowToast(string text, string reason)
        {
            m_toastText.text = text;
            m_toastReason.text = reason;
            Run(ref m_toastFx, ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            m_toast.SetActive(true);
            yield return new WaitForSecondsRealtime(k_ToastSeconds);
            m_toast.SetActive(false);
        }

        // Resources 경로. 시트면 첫 칸
        private Sprite Load(string path)
        {
            if (path == null)
            {
                return null;
            }

            if (!m_sprites.TryGetValue(path, out Sprite sprite))
            {
                Sprite[] frames = Resources.LoadAll<Sprite>(path);
                Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                sprite = frames.Length > 0 ? frames[0] : null;
                m_sprites[path] = sprite;
            }

            return sprite;
        }

        private void Row_ButtonClicked(ClerkRowView row)
        {
            int index = m_rowViews.IndexOf(row);
            RowButtonClicked?.Invoke(index >= 0 ? index : m_candidateViews.IndexOf(row));
        }
    }
}
