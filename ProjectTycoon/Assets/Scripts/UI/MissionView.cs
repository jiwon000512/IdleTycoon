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
    // 설계 54 오늘의 일 판: HUD 오른쪽 메뉴 줄 다섯째(유물 아래) 버튼으로 연다(받을 것이 있으면 배지 「!」).
    // 점수 막대 + 그 위 상자 칸 셋(점수 자리에, 열 수 있으면 톡, 연 상자는 체크) + 미션 줄 다섯(아이콘 · 이름 · 진행 · 점수 또는 받기 · 받은 줄은 흐림 + 체크) + 새 일까지. 규칙은 Core DailyBoard · MissionPresenter
    public sealed class MissionView : UIView
    {
        [Serializable]
        public sealed class RowSlot
        {
            public GameObject Root;
            public CanvasGroup Group;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Progress;
            public TMP_Text Points;
            public Button Claim;
            public GameObject Check;
        }

        [Serializable]
        public sealed class ChestSlot
        {
            public RectTransform Root;
            public Button Button;
            public TMP_Text Value;
            public TMP_Text Points;
            public GameObject Check;
        }

        public readonly struct RowData
        {
            public readonly Sprite Icon;
            public readonly string Name;
            public readonly string Progress;
            public readonly string Points;
            public readonly bool Done;
            public readonly bool Claimed;

            public RowData(Sprite icon, string name, string progress, string points, bool done, bool claimed)
            {
                Icon = icon;
                Name = name;
                Progress = progress;
                Points = points;
                Done = done;
                Claimed = claimed;
            }
        }

        public readonly struct ChestData
        {
            // 막대 위 자리(0~1)
            public readonly float At;
            public readonly string Value;
            public readonly string Points;
            public readonly bool Ready;
            public readonly bool Opened;

            public ChestData(float at, string value, string points, bool ready, bool opened)
            {
                At = at;
                Value = value;
                Points = points;
                Ready = ready;
                Opened = opened;
            }
        }

        [SerializeField] private Button m_openButton;
        [Tooltip("받을 것이 있으면(다 한 미션 · 열 수 있는 상자) 버튼 오른쪽 위 배지")]
        [SerializeField] private GameObject m_badge;
        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("점수 막대 채움(Filled 가로)")]
        [SerializeField] private Image m_bar;
        [SerializeField] private TextMeshProUGUI m_points;
        [SerializeField] private ChestSlot[] m_chests;
        [SerializeField] private RowSlot[] m_rows;
        [SerializeField] private TextMeshProUGUI m_empty;
        [SerializeField] private TextMeshProUGUI m_reset;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Func<string> m_resetText;
        private bool[] m_ready = new bool[0];

        public event Action OpenClicked;
        public event Action CloseRequested;
        public event Action<int> ClaimClicked;
        public event Action<int> ChestClicked;

        public bool IsOpen => m_root.activeSelf;
        // 설계 55: 오른쪽 메뉴 줄의 오늘의 일 단추
        public RectTransform MenuButton => (RectTransform)m_openButton.transform;

        private void Awake()
        {
            m_openButton.onClick.AddListener(() => OpenClicked?.Invoke());
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

            for (int i = 0; i < m_rows.Length; i++)
            {
                int index = i;
                m_rows[i].Claim.onClick.AddListener(() => ClaimClicked?.Invoke(index));
            }

            for (int i = 0; i < m_chests.Length; i++)
            {
                int index = i;
                m_chests[i].Button.onClick.AddListener(() => ChestClicked?.Invoke(index));
            }

            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
            m_badge.SetActive(false);
        }

        private void Update()
        {
            if (m_root.activeSelf && m_resetText != null)
            {
                m_reset.text = m_resetText();
            }
        }

        public void SetTitle(string title, string empty, Func<string> resetText)
        {
            m_title.text = title;
            m_empty.text = empty;
            m_resetText = resetText;
        }

        public void SetButtonVisible(bool visible)
        {
            m_openButton.gameObject.SetActive(visible);
        }

        public void SetBadge(bool on)
        {
            m_badge.SetActive(on);
        }

        // 설계 54: 할 일 알약의 「가기」가 이 메뉴 버튼을 가리킨다(세 번 톡톡)
        public void PointButton()
        {
            StartCoroutine(UiFx.Point((RectTransform)m_openButton.transform));
        }

        public void Open()
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            Run(UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            Run(UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 미션 줄(남는 줄은 숨김) · 상자 칸 · 막대 채움 0~1 · 지금 점수
        public void Show(IReadOnlyList<RowData> rows, IReadOnlyList<ChestData> chests, float fill, string points)
        {
            for (int i = 0; i < m_rows.Length; i++)
            {
                RowSlot slot = m_rows[i];
                bool shown = i < rows.Count;
                slot.Root.SetActive(shown);

                if (!shown)
                {
                    continue;
                }

                RowData row = rows[i];
                slot.Icon.sprite = row.Icon;
                slot.Icon.enabled = row.Icon != null;
                slot.Name.text = row.Name;
                slot.Progress.text = row.Progress;
                slot.Points.text = row.Points;
                slot.Points.gameObject.SetActive(!row.Done);
                slot.Claim.gameObject.SetActive(row.Done && !row.Claimed);
                slot.Check.SetActive(row.Claimed);
                slot.Group.alpha = row.Claimed ? 0.55f : 1f;
            }

            m_empty.gameObject.SetActive(rows.Count == 0);
            bool[] ready = new bool[m_chests.Length];

            for (int i = 0; i < m_chests.Length; i++)
            {
                ChestSlot slot = m_chests[i];
                bool shown = i < chests.Count;
                slot.Root.gameObject.SetActive(shown);

                if (!shown)
                {
                    continue;
                }

                ChestData chest = chests[i];
                slot.Root.anchorMin = new Vector2(chest.At, slot.Root.anchorMin.y);
                slot.Root.anchorMax = new Vector2(chest.At, slot.Root.anchorMax.y);
                slot.Value.text = chest.Value;
                slot.Points.text = chest.Points;
                slot.Check.SetActive(chest.Opened);
                slot.Button.interactable = chest.Ready && !chest.Opened;
                ready[i] = chest.Ready && !chest.Opened;

                // 막 열 수 있게 된 상자는 한 번 톡
                if (ready[i] && (i >= m_ready.Length || !m_ready[i]) && m_root.activeSelf)
                {
                    StartCoroutine(UiFx.Pulse(slot.Root));
                }
            }

            m_ready = ready;
            m_bar.fillAmount = Mathf.Clamp01(fill);
            m_points.text = points;
        }

        private void Run(IEnumerator routine)
        {
            if (m_fx != null)
            {
                StopCoroutine(m_fx);
            }

            m_fx = StartCoroutine(routine);
        }
    }
}
