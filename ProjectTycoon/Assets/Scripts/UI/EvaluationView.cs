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
    // 설계 40 평가 팝업(석상 팝업과 같은 틀): 평가판 버튼으로 열리는 「빵집 평가」(별 · 별마다 능력 · 다음 평가 조건 · 보상 · 부르기)와
    // 평가가 끝나면 뜨는 「굴 소식」(통과: 별 · 평가단장 한마디 · 얻은 것, 실패: 모자란 것)을 같은 틀에 그린다. 버튼 글 · 켜짐은 Presenter가 준 읽기 함수로 매 프레임(쉬는 시간)
    public sealed class EvaluationView : UIView
    {
        public sealed class PageData
        {
            public string Title;
            // 카드 별 그림의 별 수(0 = 빈 별, 5개마다 색이 오른다)
            public int Stars;
            public string Name;
            public string Effect;
            public string Time;
            public string Hint;
            public Func<string> Button;
            public Func<bool> ButtonEnabled;
        }

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("별 카드: 별 수 · 별마다 능력 둘(소식지는 제목 · 평가단장 한마디)")]
        [SerializeField] private RectTransform m_card;
        [SerializeField] private Image m_icon;
        [Tooltip("별 그림: 빈 별 · 갈색(★1~5) · 은(6~10) · 금(11~15) · 무지개(16~)")]
        [SerializeField] private Sprite[] m_stars;
        [SerializeField] private TextMeshProUGUI m_name;
        [SerializeField] private TextMeshProUGUI m_effect;
        [SerializeField] private TextMeshProUGUI m_time;
        [Tooltip("다음 평가 조건 · 보상(소식지는 얻은 것 · 모자란 것), 줄마다 하나")]
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private PageData m_page;

        public event Action CloseRequested;
        public event Action ButtonClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_button.onClick.AddListener(() => ButtonClicked?.Invoke());
            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
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

        public void Show(PageData page)
        {
            m_page = page;
            m_title.text = page.Title;
            m_icon.sprite = StarSprite(m_stars, page.Stars);
            m_name.text = page.Name;
            m_effect.text = page.Effect;
            m_time.text = page.Time;
            m_hint.text = page.Hint;
            Refresh();
        }

        // 소식지가 뜨는 순간: 통과면 카드가 부풀고 통과 소리(별 색이 오르면 그 소리도), 실패면 실패 소리
        public void PlayResult(bool passed, bool tierUp)
        {
            StartCoroutine(UiFx.Pulse(m_card));
            SoundManager.Instance.Play(passed ? SoundTable.k_EvalPass : SoundTable.k_EvalFail);

            if (tierUp)
            {
                SoundManager.Instance.Play(SoundTable.k_StarTier);
            }
        }

        // 별 수 → 그림(0 = 빈 별, 1~5 갈색, 6~10 은, … 마지막 그림에서 멈춘다). 상단 평가 알약도 쓴다
        public static Sprite StarSprite(Sprite[] stars, int count)
        {
            return count <= 0 ? stars[0] : stars[Mathf.Min(1 + (count - 1) / 5, stars.Length - 1)];
        }

        private void Update()
        {
            if (m_root.activeSelf && m_page != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            m_buttonLabel.text = m_page.Button();
            m_button.interactable = m_page.ButtonEnabled();
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
