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
    // 설계 31 · 32 → 설계 36 뽑기 메인: 위 글 · 카드 뒷면 한 장 · 아래 글(가진 반짝돌 · 모자람 · 다 모음) · 뽑기 버튼.
    // 뽑은 결과는 이 팝업 위에 따로 뜨는 RelicDrawResultView. 규칙은 Core Relics · RelicCartPresenter
    public sealed class RelicCartView : UIView
    {
        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Sprite m_cardBack;
        [SerializeField] private TextMeshProUGUI m_label;
        [SerializeField] private RelicCard m_card;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private Button m_drawButton;
        [SerializeField] private TextMeshProUGUI m_drawLabel;
        [SerializeField] private TextMeshProUGUI m_drawCost;

        private Vector2 m_panelRest;
        private Coroutine m_fx;

        public event Action CloseRequested;
        public event Action DrawClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_drawButton.onClick.AddListener(() => DrawClicked?.Invoke());
            m_card.Button.interactable = false;
            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
        }

        public void SetLabels(string title, string draw)
        {
            m_title.text = title;
            m_drawLabel.text = draw;
        }

        // delay: 행상이 인사를 말하는 동안 기다렸다 뜬다(설계 34)
        public void Open(float delay)
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            Run(UiFx.Appear(m_rootGroup, m_panel, m_panelRest, delay));
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

        public void ShowReady(string label, string hint, int cost, bool canDraw)
        {
            m_card.SetBack(m_cardBack);
            m_label.text = label;
            m_hint.text = hint;
            m_drawCost.text = cost.ToString();
            m_drawButton.interactable = canDraw;
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
