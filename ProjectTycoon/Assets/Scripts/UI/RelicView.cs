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
    // 설계 32 유물 화면: HUD 오른쪽 버튼 줄(창고 아래)의 유물 버튼으로 어디서나 연다.
    // 끼운 유물 칸 셋(아이콘 · 별 · 효과) + 모은 유물 격자(못 모은 칸은 그림자) + 고른 유물 정보 줄(끼우기/빼기) + 안내. 규칙은 Core Relics · RelicPresenter
    public sealed class RelicView : UIView
    {
        [SerializeField] private Button m_openButton;
        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Sprite m_chip;
        [SerializeField] private Sprite m_chipSelected;
        [SerializeField] private TextMeshProUGUI m_slotsLabel;
        [SerializeField] private RelicCard[] m_slots;
        [SerializeField] private TextMeshProUGUI m_collectionLabel;
        [SerializeField] private RelicCard[] m_cells;
        [SerializeField] private GameObject m_info;
        [SerializeField] private RelicCard m_infoCard;
        [SerializeField] private Button m_infoButton;
        [SerializeField] private TextMeshProUGUI m_infoButtonLabel;
        [SerializeField] private TextMeshProUGUI m_hint;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private string m_emptySlot;
        private Color m_effectColor;

        public event Action OpenClicked;
        public event Action CloseRequested;
        public event Action InfoClicked;
        public event Action<int> SlotClicked;
        public event Action<int> CellClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_openButton.onClick.AddListener(() => OpenClicked?.Invoke());
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_infoButton.onClick.AddListener(() => InfoClicked?.Invoke());
            Listen(m_slots, i => SlotClicked?.Invoke(i));
            Listen(m_cells, i => CellClicked?.Invoke(i));
            m_panelRest = m_panel.anchoredPosition;
            m_effectColor = m_slots[0].Effect.color;
            m_root.SetActive(false);
        }

        public void SetButtonVisible(bool visible)
        {
            m_openButton.gameObject.SetActive(visible);
        }

        public void SetLabels(string title, string slots, string emptySlot)
        {
            m_title.text = title;
            m_slotsLabel.text = slots;
            m_emptySlot = emptySlot;
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

        // 칸(null이면 빈 칸) · 격자 · 「모은 유물 n/m」 · 고른 유물(null이면 정보 줄을 숨김)과 버튼 글 · 안내
        public void Show(IReadOnlyList<RelicCard.Data> slots, IReadOnlyList<RelicCard.Data> cells, string collection,
            RelicCard.Data info, string infoButton, bool infoButtonOn, string hint)
        {
            for (int i = 0; i < m_slots.Length; i++)
            {
                m_slots[i].Set(slots[i], m_chip, m_chipSelected);
                // 빈 칸은 안내 글 색으로 「빈 칸」
                m_slots[i].Effect.text = slots[i] != null ? slots[i].Effect : m_emptySlot;
                m_slots[i].Effect.color = slots[i] != null ? m_effectColor : m_hint.color;
            }

            for (int i = 0; i < m_cells.Length; i++)
            {
                m_cells[i].Set(cells[i], m_chip, m_chipSelected);
            }

            m_collectionLabel.text = collection;
            m_info.SetActive(info != null);

            if (info != null)
            {
                m_infoCard.Set(info, m_chip, m_chipSelected);
                m_infoButtonLabel.text = infoButton;
                m_infoButton.interactable = infoButtonOn;
            }

            m_hint.text = hint;
        }

        // 칸 · 격자 하나가 한 번 부푼다(끼우거나 뺀 자리)
        public void PulseSlot(int index)
        {
            StartCoroutine(UiFx.Pulse(m_slots[index].Root));
        }

        private static void Listen(RelicCard[] cards, Action<int> clicked)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].Button.onClick.AddListener(() => clicked(index));
            }
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
