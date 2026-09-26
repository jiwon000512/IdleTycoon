using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 21 점원 화면: 편집 버튼 아래 점원 버튼(늘 보임) → 팝업. 팝업은 세 상태를 한 프리팹에 둔다:
    // 목록(가게 탭 + 줄: 자리 목록 또는 후보 목록 + 바닥 글/버튼) · 목록 위 「고용할까」 작은 창 · 협상(위 장면 = 웜뱃·후보 옆모습 + 말풍선, 아래 = 타이밍 바).
    // 표시와 입력 이벤트만, 규칙은 ClerkPresenter. 그림 경로(Resources)는 여기서 읽는다
    public sealed class ClerkPopupView : UIView
    {
        public sealed class RowData
        {
            public string IconPath;
            public Sprite Icon;
            public string Name;
            public string Sub;
            public string Button;
            public bool Enabled;
        }

        private const float k_ToastSeconds = 2.5f;
        // 협상 장면의 웜뱃·후보 그림: 스프라이트 1px = 캔버스 2px(둘 다 같은 배율, 시트 칸 여백 포함)
        private const float k_ScenePixel = 2f;

        [SerializeField] private Button m_openButton;
        [SerializeField] private GameObject m_root;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("목록 상태: 가게 탭 줄 · 요약 · 줄 목록 · 바닥(글 또는 버튼)")]
        [SerializeField] private GameObject m_list;
        [SerializeField] private GameObject m_tabRow;
        [SerializeField] private TextMeshProUGUI m_tabLabel;
        [SerializeField] private TextMeshProUGUI m_summary;
        [SerializeField] private RectTransform m_rows;
        [SerializeField] private ClerkRowView m_rowTemplate;
        [SerializeField] private TextMeshProUGUI m_foot;
        [SerializeField] private Button m_footButton;
        [SerializeField] private TextMeshProUGUI m_footButtonLabel;
        [Tooltip("고용할까 작은 창")]
        [SerializeField] private GameObject m_ask;
        [SerializeField] private Image m_askIcon;
        [SerializeField] private TextMeshProUGUI m_askTitle;
        [SerializeField] private TextMeshProUGUI m_askSub;
        [SerializeField] private Button m_askPlain;
        [SerializeField] private TextMeshProUGUI m_askPlainLabel;
        [SerializeField] private Button m_askNegotiate;
        [SerializeField] private TextMeshProUGUI m_askNegotiateLabel;
        [SerializeField] private Button m_askCancel;
        [SerializeField] private TextMeshProUGUI m_askCancelLabel;
        [Tooltip("협상 상태")]
        [SerializeField] private GameObject m_nego;
        [SerializeField] private Image m_negoWombat;
        [SerializeField] private Image m_negoCandidate;
        [SerializeField] private TextMeshProUGUI m_negoLeft;
        [SerializeField] private TextMeshProUGUI m_negoRight;
        [SerializeField] private TextMeshProUGUI m_negoHint;
        [SerializeField] private RectTransform m_marker;
        [Tooltip("바 구간 5개(빨강·흰·초록·흰·빨강). 폭은 SetZones가 정한다")]
        [SerializeField] private RectTransform[] m_zones;
        [SerializeField] private Button m_tapArea;
        [Tooltip("해고 토스트")]
        [SerializeField] private GameObject m_toast;
        [SerializeField] private TextMeshProUGUI m_toastText;

        private readonly List<ClerkRowView> m_rowViews = new List<ClerkRowView>();
        private readonly Dictionary<string, Sprite> m_sprites = new Dictionary<string, Sprite>();

        public event Action OpenClicked;
        public event Action CloseClicked;
        public event Action<int> RowButtonClicked;
        public event Action FootClicked;
        public event Action AskPlainClicked;
        public event Action AskNegotiateClicked;
        public event Action AskCancelClicked;
        public event Action Tapped;
        // 협상 화면이 보이는 동안 매 프레임(초)
        public event Action<float> Ticked;

        public bool IsVisible => m_root.activeSelf;

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
            m_rowTemplate.gameObject.SetActive(false);
            m_root.SetActive(false);
            m_toast.SetActive(false);
        }

        private void Update()
        {
            if (m_root.activeSelf && m_nego.activeSelf)
            {
                Ticked?.Invoke(Time.deltaTime);
            }
        }

        // 편집 모드에서는 점원 버튼을 숨긴다
        public void SetButtonVisible(bool visible)
        {
            m_openButton.gameObject.SetActive(visible);
        }

        public void Open()
        {
            m_root.SetActive(true);
        }

        public void Close()
        {
            m_root.SetActive(false);
            m_ask.SetActive(false);
        }

        // 목록 상태. tab이 null이면 탭 줄을 숨긴다(후보 목록). footButton이 null이면 바닥은 글
        public void ShowList(string title, string tab, string summary, IReadOnlyList<RowData> rows, string foot, string footButton, bool footEnabled)
        {
            m_title.text = title;
            m_list.SetActive(true);
            m_nego.SetActive(false);
            m_ask.SetActive(false);
            m_tabRow.SetActive(tab != null);
            m_tabLabel.text = tab ?? string.Empty;
            m_summary.gameObject.SetActive(summary != null);
            m_summary.text = summary ?? string.Empty;
            m_foot.gameObject.SetActive(footButton == null);
            m_foot.text = foot ?? string.Empty;
            m_footButton.gameObject.SetActive(footButton != null);
            m_footButtonLabel.text = footButton ?? string.Empty;
            m_footButton.interactable = footEnabled;

            while (m_rowViews.Count < rows.Count)
            {
                ClerkRowView row = Instantiate(m_rowTemplate, m_rows);
                row.ButtonClicked += Row_ButtonClicked;
                m_rowViews.Add(row);
            }

            for (int i = 0; i < m_rowViews.Count; i++)
            {
                if (i >= rows.Count)
                {
                    m_rowViews[i].gameObject.SetActive(false);
                    continue;
                }

                RowData data = rows[i];
                m_rowViews[i].Show(data.Icon != null ? data.Icon : Load(data.IconPath), data.Name, data.Sub, data.Button, data.Enabled);
            }
        }

        public void ShowAsk(string iconPath, string title, string sub, string plain, string negotiate, string cancel)
        {
            m_ask.SetActive(true);
            m_askIcon.sprite = Load(iconPath);
            m_askTitle.text = title;
            m_askSub.text = sub;
            m_askPlainLabel.text = plain;
            m_askNegotiateLabel.text = negotiate;
            m_askCancelLabel.text = cancel;
        }

        public void HideAsk()
        {
            m_ask.SetActive(false);
        }

        // 협상 상태. candidatePath = 후보 옆모습 시트(첫 칸을 좌우 뒤집어 왼쪽을 본다)
        public void ShowNegotiation(string title, string hint, string candidatePath, string left, string right)
        {
            m_title.text = title;
            m_list.SetActive(false);
            m_ask.SetActive(false);
            m_nego.SetActive(true);
            m_negoHint.text = hint;
            m_negoCandidate.sprite = Load(candidatePath);
            NativeSize(m_negoWombat);
            NativeSize(m_negoCandidate);
            SetBubbles(left, right);
            SetMarker(0f);
        }

        private static void NativeSize(Image image)
        {
            image.SetNativeSize();
            image.rectTransform.sizeDelta *= k_ScenePixel;
        }

        public void SetBubbles(string left, string right)
        {
            m_negoLeft.text = left;
            m_negoRight.text = right;
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

        public void ShowToast(string text)
        {
            m_toastText.text = text;
            StopAllCoroutines();
            StartCoroutine(ToastRoutine());
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
            RowButtonClicked?.Invoke(m_rowViews.IndexOf(row));
        }
    }
}
