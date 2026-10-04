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
    // 설계 40 · 41 평가 팝업. 두 판을 번갈아 보인다.
    // 평가판(panel 틀): 별 카드(별 · 능력 알약 둘) · 「★1 평가」 이름표 · 시간 · 손님 배수 · 조건 칸 · 「★1이 되면」 얻는 것 칸 · 부르기.
    // 굴 소식(아트방 소식지 틀 B3): 제호 · 제목 · 별 다섯 · 평가단장 한마디 · 칸 상자(통과: 얻은 것, 실패: 모자란 것) · 버튼.
    // 칸은 InfoTile, 컨테이너의 첫 자식을 본으로 복제한다. 버튼 글 · 켜짐은 Presenter가 준 읽기 함수로 매 프레임(쉬는 시간)
    public sealed class EvaluationView : UIView
    {
        private const int k_StarRow = 5;

        public sealed class BoardData
        {
            public string Title;
            // 카드 별 그림의 별 수(0 = 빈 별, 5개마다 색이 오른다)
            public int Stars;
            public string Name;
            // 별 아래 한 줄(없으면 숨김)
            public string Sub;
            public string Price;
            public string Visitors;
            public string Tag;
            public string Time;
            public string Rush;
            public IReadOnlyList<InfoTile.Data> Goals;
            public string UnlockTag;
            public IReadOnlyList<InfoTile.Data> Rewards;
            // 칸으로 못 그리는 해금(팁 · 다음 가게), 없으면 숨김
            public string Extra;
            public Func<string> Button;
            public Func<bool> ButtonEnabled;
        }

        public sealed class NewsData
        {
            public string Masthead;
            public string Title;
            // 별 줄(0 = 숨김)
            public int Stars;
            public string Line;
            public string BoxLabel;
            // 통과는 얻은 것(작은 칸), 실패는 조건(큰 칸). 쓰지 않는 쪽은 null
            public IReadOnlyList<InfoTile.Data> Rewards;
            public IReadOnlyList<InfoTile.Data> Goals;
            public string Extra;
            public string Button;
        }

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private Button m_dim;
        [Tooltip("별 그림: 빈 별 · 갈색(★1~5) · 은(6~10) · 금(11~15) · 무지개(16~)")]
        [SerializeField] private Sprite[] m_stars;
        [Tooltip("이름으로 찾는 칸 아이콘(말풍선 ♥ · !!, 사물, 업그레이드). 그 밖은 Resources 경로")]
        [SerializeField] private List<InfoTile.IconRef> m_icons = new List<InfoTile.IconRef>();

        [Header("평가판")]
        [SerializeField] private RectTransform m_board;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Image m_starIcon;
        [SerializeField] private TextMeshProUGUI m_name;
        [SerializeField] private TextMeshProUGUI m_sub;
        [SerializeField] private TextMeshProUGUI m_price;
        [SerializeField] private TextMeshProUGUI m_visitors;
        [SerializeField] private TextMeshProUGUI m_tag;
        [SerializeField] private TextMeshProUGUI m_time;
        [SerializeField] private TextMeshProUGUI m_rush;
        [SerializeField] private RectTransform m_goals;
        [SerializeField] private TextMeshProUGUI m_unlockTag;
        [SerializeField] private RectTransform m_rewards;
        [SerializeField] private TextMeshProUGUI m_extra;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;

        [Header("굴 소식")]
        [SerializeField] private RectTransform m_news;
        [SerializeField] private TextMeshProUGUI m_masthead;
        [SerializeField] private TextMeshProUGUI m_newsTitle;
        [SerializeField] private RectTransform m_newsStarRow;
        [SerializeField] private Image[] m_newsStars;
        [SerializeField] private TextMeshProUGUI m_newsLine;
        [SerializeField] private TextMeshProUGUI m_boxLabel;
        [SerializeField] private RectTransform m_newsRewards;
        [SerializeField] private RectTransform m_newsGoals;
        [SerializeField] private TextMeshProUGUI m_newsExtra;
        [SerializeField] private Button m_newsButton;
        [SerializeField] private TextMeshProUGUI m_newsButtonLabel;

        private Vector2 m_boardRest;
        private Vector2 m_newsRest;
        private Coroutine m_fx;
        private BoardData m_page;

        public event Action CloseRequested;
        public event Action ButtonClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_button.onClick.AddListener(() => ButtonClicked?.Invoke());
            m_newsButton.onClick.AddListener(() => ButtonClicked?.Invoke());
            m_boardRest = m_board.anchoredPosition;
            m_newsRest = m_news.anchoredPosition;
            m_root.SetActive(false);
        }

        public void Open()
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            bool news = m_news.gameObject.activeSelf;
            Run(UiFx.Appear(m_rootGroup, news ? m_news : m_board, news ? m_newsRest : m_boardRest, 0f));
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

        public void ShowBoard(BoardData page)
        {
            m_page = page;
            m_board.gameObject.SetActive(true);
            m_news.gameObject.SetActive(false);
            m_title.text = page.Title;
            m_starIcon.sprite = StarSprite(m_stars, page.Stars);
            m_name.text = page.Name;
            SetLine(m_sub, page.Sub);
            m_price.text = page.Price;
            m_visitors.text = page.Visitors;
            m_tag.text = page.Tag;
            m_time.text = page.Time;
            m_rush.text = page.Rush;
            InfoTile.Fill(m_goals, page.Goals, m_icons);
            m_unlockTag.text = page.UnlockTag;
            InfoTile.Fill(m_rewards, page.Rewards, m_icons);
            SetLine(m_extra, page.Extra);
            Refresh();
        }

        public void ShowNews(NewsData page)
        {
            m_page = null;
            m_board.gameObject.SetActive(false);
            m_news.gameObject.SetActive(true);
            m_masthead.text = page.Masthead;
            m_newsTitle.text = page.Title;
            m_newsStarRow.gameObject.SetActive(page.Stars > 0);

            // 지금 색의 다섯 칸 중 채운 별(★7 = 은별 둘)
            int filled = page.Stars > 0 ? (page.Stars - 1) % k_StarRow + 1 : 0;

            for (int i = 0; i < m_newsStars.Length; i++)
            {
                m_newsStars[i].sprite = i < filled ? StarSprite(m_stars, page.Stars) : m_stars[0];
            }

            m_newsLine.text = page.Line;
            m_boxLabel.text = page.BoxLabel;
            m_newsRewards.gameObject.SetActive(page.Rewards != null);
            m_newsGoals.gameObject.SetActive(page.Goals != null);
            InfoTile.Fill(m_newsRewards, page.Rewards, m_icons);
            InfoTile.Fill(m_newsGoals, page.Goals, m_icons);
            SetLine(m_newsExtra, page.Extra);
            m_newsButtonLabel.text = page.Button;
        }

        // 소식지가 뜨는 순간: 통과면 별 줄이 부풀고 통과 소리(별 색이 오르면 그 소리도), 실패면 실패 소리
        public void PlayResult(bool passed, bool tierUp)
        {
            StartCoroutine(UiFx.Pulse(passed ? m_newsStarRow : (RectTransform)m_newsTitle.transform));
            SoundManager.Instance.Play(passed ? SoundTable.k_EvalPass : SoundTable.k_EvalFail);

            if (tierUp)
            {
                SoundManager.Instance.Play(SoundTable.k_StarTier);
            }
        }

        // 별 수 → 그림(0 = 빈 별, 1~5 갈색, 6~10 은, … 마지막 그림에서 멈춘다). 상단 평가 카드도 쓴다
        public static Sprite StarSprite(Sprite[] stars, int count)
        {
            return count <= 0 ? stars[0] : stars[Mathf.Min(1 + (count - 1) / k_StarRow, stars.Length - 1)];
        }

        private static void SetLine(TMP_Text text, string value)
        {
            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
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
