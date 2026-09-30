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
    // 설계 26 창고(인벤토리): 점원 버튼 아래 창고 버튼(편집 모드에서 숨음) → 팝업(점원 팝업과 같은 틀)은 재료 칸 격자 4열만.
    // 칸을 누르면 그 위에 정보 창(점원 팝업의 「고용할까」 창처럼)이 떠서 이름 · 개수 · 설명 · 획득처 · 사용처를 보인다. 표시와 입력만, 규칙은 InventoryPresenter
    public sealed class InventoryView : UIView
    {
        // 칸 하나. IconPath가 null이면 빈 칸(자리만 보인다)
        public sealed class SlotData
        {
            public string IconPath;
            public string Count;
            public string Name;
            public bool Dim;
        }

        // 획득처 한 줄: 곳 · 걸리는 초 · 한 번에 얻는 개수(재료 아이콘 + 개수)
        public sealed class SourceData
        {
            public string Place;
            public string Seconds;
            public string IconPath;
            public string Yield;
        }

        // 사용처 하나: 쓰는 것(빵) 그림 · 이름 · 한 번에 드는 개수
        public sealed class UseData
        {
            public string IconPath;
            public string Name;
            public string Count;
        }

        // 월드 그림(한 칸 2px)을 정수 배로(UI 규칙 8): 칸 아이콘은 한 칸 = 캔버스 10px(칸을 채우게, QA 2026-09-30), 정보 아이콘 8px, 줄 아이콘 4px
        private const float k_SlotIconScale = 5f;
        private const float k_BigIconScale = 4f;
        private const float k_LineIconScale = 2f;
        private const float k_EmptyAlpha = 0.35f;
        private const float k_DimAlpha = 0.5f;

        [SerializeField] private Button m_openButton;
        [SerializeField] private GameObject m_root;
        [Tooltip("등장 · 퇴장: 팝업 전체가 나타나고 패널이 올라온다(UiFx)")]
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Button m_slotTemplate;
        [Tooltip("정보 창: 창고 패널 위 딤 + 상자. 상자 = 아이콘 · 이름 · 개수 · 닫기 / 설명 / 획득처 틀 / 사용처 틀")]
        [SerializeField] private GameObject m_info;
        [SerializeField] private CanvasGroup m_infoGroup;
        [SerializeField] private RectTransform m_infoBox;
        [SerializeField] private Button m_infoDim;
        [SerializeField] private Button m_infoClose;
        [SerializeField] private Image m_infoIcon;
        [SerializeField] private TextMeshProUGUI m_infoName;
        [SerializeField] private TextMeshProUGUI m_infoCount;
        [SerializeField] private TextMeshProUGUI m_infoDesc;
        [SerializeField] private TextMeshProUGUI m_sourceLabel;
        [SerializeField] private RectTransform m_sourceTemplate;
        [SerializeField] private TextMeshProUGUI m_sourceNone;
        [SerializeField] private TextMeshProUGUI m_usesLabel;
        [SerializeField] private RectTransform m_useTemplate;
        [SerializeField] private TextMeshProUGUI m_usesNone;

        private readonly List<Button> m_slots = new List<Button>();
        // 칸마다 마지막에 보인 개수 글(열린 채 바뀌면 그 칸이 부푼다)
        private readonly List<string> m_counts = new List<string>();
        private readonly List<RectTransform> m_sources = new List<RectTransform>();
        private readonly List<RectTransform> m_uses = new List<RectTransform>();
        private readonly Dictionary<string, Sprite> m_sprites = new Dictionary<string, Sprite>();
        private Vector2 m_panelRest;
        private Vector2 m_infoRest;
        private Coroutine m_fx;
        private Coroutine m_infoFx;

        public event Action OpenClicked;
        public event Action CloseRequested;
        public event Action<int> SlotClicked;
        public event Action InfoCloseRequested;

        public bool IsOpen => m_root.activeSelf;
        public bool IsInfoOpen => m_info.activeSelf;

        private void Awake()
        {
            m_openButton.onClick.AddListener(() => OpenClicked?.Invoke());
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_infoDim.onClick.AddListener(() => InfoCloseRequested?.Invoke());
            m_infoClose.onClick.AddListener(() => InfoCloseRequested?.Invoke());
            m_slotTemplate.gameObject.SetActive(false);
            m_sourceTemplate.gameObject.SetActive(false);
            m_useTemplate.gameObject.SetActive(false);
            m_panelRest = m_panel.anchoredPosition;
            m_infoRest = m_infoBox.anchoredPosition;
            m_info.SetActive(false);
            m_root.SetActive(false);
        }

        public void SetButtonVisible(bool visible)
        {
            m_openButton.gameObject.SetActive(visible);
        }

        public void SetLabels(string title, string source, string uses, string none)
        {
            m_title.text = title;
            m_sourceLabel.text = source;
            m_usesLabel.text = uses;
            m_sourceNone.text = none;
            m_usesNone.text = none;
        }

        public void Open()
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_info.SetActive(false);
            m_root.SetActive(true);
            Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 칸: 아이콘(5배) + 오른쪽 아래 개수 배지(빈 칸은 숨김) + 이름. 열린 채 개수가 바뀐 칸은 한 번 부푼다
        public void SetSlots(IReadOnlyList<SlotData> slots)
        {
            while (m_slots.Count < slots.Count)
            {
                int index = m_slots.Count;
                Button slot = Instantiate(m_slotTemplate, m_slotTemplate.transform.parent);
                slot.onClick.AddListener(() => SlotClicked?.Invoke(index));
                m_slots.Add(slot);
                m_counts.Add(null);
            }

            for (int i = 0; i < m_slots.Count; i++)
            {
                Button slot = m_slots[i];
                slot.gameObject.SetActive(i < slots.Count);

                if (i >= slots.Count)
                {
                    continue;
                }

                SlotData data = slots[i];
                bool empty = data.IconPath == null;
                slot.interactable = !empty;
                slot.GetComponent<CanvasGroup>().alpha = empty ? k_EmptyAlpha : data.Dim ? k_DimAlpha : 1f;
                SetIcon(slot.transform.Find("Icon").GetComponent<Image>(), data.IconPath, k_SlotIconScale);
                slot.transform.Find("CountBadge").gameObject.SetActive(!empty);
                slot.transform.Find("CountBadge/Count").GetComponent<TMP_Text>().text = data.Count;
                slot.transform.Find("Name").GetComponent<TMP_Text>().text = data.Name;

                if (!empty && m_root.activeSelf && m_counts[i] != null && m_counts[i] != data.Count)
                {
                    StartCoroutine(UiFx.Pulse((RectTransform)slot.transform));
                }

                m_counts[i] = empty ? null : data.Count;
            }
        }

        // 정보 창 내용. 열려 있지 않으면 연다
        public void ShowInfo(string iconPath, string name, string count, string desc, IReadOnlyList<SourceData> sources, IReadOnlyList<UseData> uses)
        {
            SetIcon(m_infoIcon, iconPath, k_BigIconScale);
            m_infoName.text = name;
            m_infoCount.text = count;
            m_infoDesc.text = desc;

            Fill(m_sources, m_sourceTemplate, sources.Count);
            m_sourceNone.gameObject.SetActive(sources.Count == 0);

            for (int i = 0; i < sources.Count; i++)
            {
                Transform line = m_sources[i];
                line.Find("Place").GetComponent<TMP_Text>().text = sources[i].Place;
                line.Find("Seconds").GetComponent<TMP_Text>().text = sources[i].Seconds;
                SetIcon(line.Find("Icon").GetComponent<Image>(), sources[i].IconPath, k_LineIconScale);
                line.Find("Yield").GetComponent<TMP_Text>().text = sources[i].Yield;
            }

            Fill(m_uses, m_useTemplate, uses.Count);
            m_usesNone.gameObject.SetActive(uses.Count == 0);

            for (int i = 0; i < uses.Count; i++)
            {
                Transform use = m_uses[i];
                SetIcon(use.Find("Icon").GetComponent<Image>(), uses[i].IconPath, k_LineIconScale);
                use.Find("Name").GetComponent<TMP_Text>().text = uses[i].Name;
                use.Find("Count").GetComponent<TMP_Text>().text = uses[i].Count;
            }

            if (!m_info.activeSelf)
            {
                m_info.SetActive(true);
                Run(ref m_infoFx, UiFx.Appear(m_infoGroup, m_infoBox, m_infoRest, 0f));
                SoundManager.Instance.Play(SoundTable.k_UiOpen);
            }
        }

        public void HideInfo()
        {
            if (m_info.activeSelf)
            {
                Run(ref m_infoFx, UiFx.Vanish(m_infoGroup, m_info));
                SoundManager.Instance.Play(SoundTable.k_UiClose);
            }
        }

        private static void Fill(List<RectTransform> pool, RectTransform template, int count)
        {
            while (pool.Count < count)
            {
                pool.Add(Instantiate(template, template.parent));
            }

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(i < count);
            }
        }

        // 레이아웃이 크기를 정하므로 LayoutElement에도 준다
        private void SetIcon(Image icon, string path, float scale)
        {
            Sprite sprite = path == null ? null : Load(path);
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            if (sprite == null)
            {
                return;
            }

            Vector2 size = sprite.rect.size * scale;
            icon.rectTransform.sizeDelta = size;

            if (icon.TryGetComponent(out LayoutElement layout))
            {
                layout.preferredWidth = size.x;
                layout.preferredHeight = size.y;
            }
        }

        private Sprite Load(string path)
        {
            if (!m_sprites.TryGetValue(path, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                m_sprites[path] = sprite;
            }

            return sprite;
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
