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
        [Tooltip("설계 55: 가방 탭(여는 단추는 창고 화면의 가방 단추 하나)")]
        [SerializeField] private BagTabs m_bagTabs;
        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Sprite m_chip;
        [SerializeField] private Sprite m_chipSelected;
        [Tooltip("유물 화면 B(아트방): 소제목 줄 = 아이콘 · 이름 · 개수")]
        [SerializeField] private TextMeshProUGUI m_slotsLabel;
        [SerializeField] private TextMeshProUGUI m_slotsCount;
        [SerializeField] private RelicCard[] m_slots;
        [Tooltip("빈 칸 가운데 「+」(칸마다)")]
        [SerializeField] private GameObject[] m_slotPlus;
        [SerializeField] private TextMeshProUGUI m_collectionLabel;
        [SerializeField] private TextMeshProUGUI m_collectionCount;
        [SerializeField] private RelicCard[] m_cells;
        [SerializeField] private GameObject m_info;
        [SerializeField] private RelicCard m_infoCard;
        [SerializeField] private Button m_infoButton;
        [SerializeField] private TextMeshProUGUI m_infoButtonLabel;
        [SerializeField] private TextMeshProUGUI m_hint;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        // 닫히는 0.1초 동안도 켜져 있다. 그사이 다시 열면 닫기를 멈추고 다시 띄운다(가방 탭, 2026-10-09 리뷰)
        private bool m_closing;
        private Color m_effectColor;

        public event Action CloseRequested;
        public event Action InfoClicked;
        public event Action<int> SlotClicked;
        public event Action<int> CellClicked;

        public bool IsOpen => m_root.activeSelf;
        public BagTabs BagTabs => m_bagTabs;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_infoButton.onClick.AddListener(() => InfoClicked?.Invoke());
            Listen(m_slots, i => SlotClicked?.Invoke(i));
            Listen(m_cells, i => CellClicked?.Invoke(i));
            m_panelRest = m_panel.anchoredPosition;
            m_effectColor = m_slots[0].Effect.color;
            m_root.SetActive(false);
        }

        public void SetLabels(string title, string slots, string collection)
        {
            m_title.text = title;
            m_slotsLabel.text = slots;
            m_collectionLabel.text = collection;
        }

        public void Open()
        {
            if (m_root.activeSelf && !m_closing)
            {
                return;
            }

            m_closing = false;
            m_root.SetActive(true);
            Run(UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf || m_closing)
            {
                return;
            }

            m_closing = true;

            Run(UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 칸(null이면 빈 칸) · 격자 · 끼운 수 · 모은 수(「n/m」) · 고른 유물(null이면 정보 줄을 숨김)과 버튼 글 · 안내
        public void Show(IReadOnlyList<RelicCard.Data> slots, IReadOnlyList<RelicCard.Data> cells, string slotsCount, string collectionCount,
            RelicCard.Data info, string infoButton, bool infoButtonOn, string hint)
        {
            for (int i = 0; i < m_slots.Length; i++)
            {
                m_slots[i].Set(slots[i], m_chip, m_chipSelected);
                // 빈 칸은 흐린 틀 가운데 「+」만
                bool empty = slots[i] == null;
                m_slots[i].Frame.color = new Color(1f, 1f, 1f, empty ? 0.6f : 1f);
                m_slots[i].Effect.color = m_effectColor;
                m_slotPlus[i].SetActive(empty);
            }

            m_slotsCount.text = slotsCount;

            for (int i = 0; i < m_cells.Length; i++)
            {
                m_cells[i].Set(cells[i], m_chip, m_chipSelected);
            }

            m_collectionCount.text = collectionCount;
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
