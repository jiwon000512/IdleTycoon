using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;
using GameKit.Audio;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 연출 1차: 상단 바는 코인만. 값이 바뀌면 0.25초 동안 숫자가 오른다(첫 표시는 즉시)
    // 숫자는 공용 코인 캡슐(Prefabs/UI/CoinPill) 안의 금색 글자.
    // 설계 30: 석상 축복이 걸린 동안 코인 아래 축복 알약(아이콘 · 효과 · 남은 시간, 마지막 10초는 깜빡임). 그동안 쓴 코인 표시는 그 아래로 내려간다.
    // 설계 40 · 41: 별 평가 중에는 화면 위 가운데에 평가 카드 하나(별 · 「★7 평가」 · 남은 시간과 줄어드는 막대 · 조건 칸: 아이콘 + 진행/목표, 채우면 초록 체크 · 실망이 상한에 닿으면 빨강). 왼쪽 알약 줄과 따로다.
    // 설계 31 · 32: 떠돌이 행상 알약은 늘 보인다(행상이 있으면 「행상 2:59」, 없으면 회색 얼굴 + 시간만 「13:57」, 2026-10-01 사용자 B). 알약은 코인 아래로 위에서부터 쌓인다(축복 → 행상 → 물때)
    // 설계 49: 낚시터에 있는 동안 물때 알약(「물때 7/10」, 대물 물때에는 「대물!」만, 2026-10-06 사용자)
    // 설계 54: 할 일 알약은 위 가운데(아이콘 · 글 · 진행 · 가기/받기 칩). 위 가운데는 「지금 목표」 자리라 평가 카드가 뜨면 알약은 숨는다(2026-10-08 사용자)
    public sealed class TopBarView : UIView
    {
        private const float k_CountSeconds = 0.25f;
        private const float k_SpentSeconds = 0.8f;
        private const float k_SpentFade = 0.3f;
        private const double k_BlinkSeconds = 10d;
        private const float k_BlinkSpeed = 4f;
        private const float k_BlinkMin = 0.45f;
        // 왼쪽 알약끼리 · 평가 카드 밑과 첫 알약 사이(코인 알약과 첫 알약 사이와 같다)
        private const float k_Gap = 8f;
        // 행상이 없을 때 얼굴 아이콘 색
        private static readonly Color k_AwayFace = new Color(0.45f, 0.45f, 0.45f);
        // 설계 41 조건 칸 하나: 아이콘(아이콘 목록 id 또는 Resources 경로) · 값(진행/목표) · 상태. 매 프레임 읽는다
        public sealed class GoalData
        {
            public string Icon;
            public Func<string> Value;
            public Func<InfoTile.State> State;
        }

        [SerializeField] private TMP_Text m_coinsText;
        [Tooltip("코인을 쓴 순간 캡슐 아래에 잠깐 뜨는 「-24」(작은 코인 캡슐)")]
        [SerializeField] private CanvasGroup m_spent;
        [SerializeField] private TMP_Text m_spentText;
        [Tooltip("설계 30 축복 알약(코인 캡슐 바로 아래)")]
        [SerializeField] private CanvasGroup m_blessing;
        [SerializeField] private Image m_blessingIcon;
        [SerializeField] private TMP_Text m_blessingText;
        [SerializeField] private TMP_Text m_blessingTime;
        [Tooltip("설계 55 행상(오른쪽 메뉴 줄): 얼굴 단추 · 아래 남은 시간 · 와 있으면 「!」. 누르지 않는다(2026-10-09 사용자)")]
        [SerializeField] private CanvasGroup m_merchant;
        [SerializeField] private Image m_merchantIcon;
        [SerializeField] private TMP_Text m_merchantTime;
        [SerializeField] private GameObject m_merchantBadge;
        [Tooltip("설계 49 물때 알약(행상 알약 아래): 물고기 아이콘 · 「물때」 · 「7/10」")]
        [SerializeField] private CanvasGroup m_wave;
        [SerializeField] private TMP_Text m_waveText;
        [SerializeField] private TMP_Text m_waveValue;
        [Tooltip("설계 41 평가 카드(화면 위 가운데): 별 · 「★7 평가」 · 남은 시간 · 시간 막대 · 조건 칸")]
        [SerializeField] private CanvasGroup m_evaluation;
        [SerializeField] private Image m_evaluationIcon;
        [Tooltip("별 그림(EvaluationView와 같은 다섯 장)")]
        [SerializeField] private Sprite[] m_stars;
        [SerializeField] private TMP_Text m_evaluationText;
        [SerializeField] private TMP_Text m_evaluationTime;
        [Tooltip("남은 시간 막대(Filled)")]
        [SerializeField] private Image m_evaluationBar;
        [Tooltip("조건 칸(큰 평가는 넷)")]
        [SerializeField] private InfoTile[] m_goals;
        [Tooltip("이름으로 찾는 조건 아이콘(말풍선 ♥ · !!). 빵은 Resources 경로")]
        [SerializeField] private List<InfoTile.IconRef> m_icons = new List<InfoTile.IconRef>();
        [Tooltip("설계 54 할 일 알약(위 가운데): 알약 전체가 버튼")]
        [SerializeField] private CanvasGroup m_quest;
        [SerializeField] private Button m_questButton;
        [SerializeField] private Image m_questIcon;
        [SerializeField] private TMP_Text m_questText;
        [SerializeField] private TMP_Text m_questValue;
        [Tooltip("다 하면 진행 대신 체크")]
        [SerializeField] private GameObject m_questCheck;
        [Tooltip("가기/받기 칩(주 버튼 조각). 받기면 코인 · 보상 값이 앞에 붙는다")]
        [SerializeField] private GameObject m_questCoin;
        [SerializeField] private TMP_Text m_questReward;
        [SerializeField] private TMP_Text m_questLabel;

        private Func<double, string> m_format;
        private double m_from;
        private double m_target;
        private double m_shown;
        private float m_t = 1f;
        private bool m_first = true;
        private float m_spentLeft;
        private Vector2 m_spentRest;
        private Func<double> m_blessingLeft;
        private float m_blessingDelay;
        private Func<double> m_merchantLeft;
        private Func<double> m_evaluationLeft;
        private double m_evaluationTotal;
        private IReadOnlyList<GoalData> m_goalData;
        private float m_pillTop;

        private bool m_questShown;
        private bool m_questDone;

        public bool SpentVisible => m_spentLeft > 0f;
        // 설계 55: 오른쪽 메뉴 줄의 행상 칸
        public RectTransform MerchantRail => (RectTransform)m_merchant.transform;

        // 설계 54: 할 일 알약을 눌렀다
        public event Action QuestClicked;

        private void Awake()
        {
            m_spent.gameObject.SetActive(false);
            m_blessing.gameObject.SetActive(false);
            m_merchant.gameObject.SetActive(false);
            m_wave.gameObject.SetActive(false);
            m_evaluation.gameObject.SetActive(false);
            m_quest.gameObject.SetActive(false);
            m_questButton.onClick.AddListener(() => QuestClicked?.Invoke());

            foreach (InfoTile goal in m_goals)
            {
                goal.gameObject.SetActive(false);
            }
            m_spentRest = ((RectTransform)m_spent.transform).anchoredPosition;
            m_pillTop = ((RectTransform)m_blessing.transform).anchoredPosition.y;
        }

        // delay: 석상 팝업에서 이름이 도는 동안은 결과를 미리 보이지 않는다
        // remaining: 남은 초를 읽는 함수(모델 시간을 그대로 보인다)
        public void ShowBlessing(string iconPath, string text, Func<double> remaining, float delay)
        {
            m_blessingIcon.sprite = Resources.Load<Sprite>(iconPath);
            m_blessingText.text = text;
            m_blessingLeft = remaining;
            m_blessingDelay = delay;

            if (delay <= 0f)
            {
                Reveal();
            }
        }

        // 걸려 있던 축복이 풀리면 은은한 소리
        public void HideBlessing()
        {
            m_blessingDelay = 0f;

            if (!m_blessing.gameObject.activeSelf)
            {
                return;
            }

            m_blessing.gameObject.SetActive(false);
            Stack();
            SoundManager.Instance.Play(SoundTable.k_BlessingEnd);
        }

        // 설계 31 · 32 · 55: remaining = 남은 시간(떠날 때까지 · 다음에 올 때까지). 없으면 회색 얼굴, 와 있으면 「!」, arrived면 톡 튀며 소리
        public void ShowMerchant(bool present, Func<double> remaining, bool arrived)
        {
            m_merchantBadge.SetActive(present);
            m_merchantIcon.color = present ? Color.white : k_AwayFace;
            m_merchantLeft = remaining;
            m_merchantTime.text = BigNumberFormatter.Clock(remaining());
            m_merchant.gameObject.SetActive(true);

            if (arrived)
            {
                StartCoroutine(UiFx.Pulse((RectTransform)m_merchant.transform));
                SoundManager.Instance.Play(SoundTable.k_MerchantArrive);
            }
        }

        // 설계 49: 물때 알약. value가 null이면 값 칸을 숨긴다(대물 물때의 「대물!」), pulse면 톡 튄다
        public void ShowWave(string text, string value, bool pulse)
        {
            m_waveText.text = text;
            m_waveValue.gameObject.SetActive(value != null);
            m_waveValue.text = value;

            if (!m_wave.gameObject.activeSelf)
            {
                m_wave.gameObject.SetActive(true);
                Stack();
            }

            if (pulse)
            {
                StartCoroutine(UiFx.Pulse((RectTransform)m_wave.transform));
            }
        }

        public void HideWave()
        {
            if (m_wave.gameObject.activeSelf)
            {
                m_wave.gameObject.SetActive(false);
                Stack();
            }
        }

        // 설계 41: 평가 카드를 켠다(남은 시간 · 조건 값은 매 프레임 읽기 함수로). total = 평가 전체 초(막대)
        public void ShowEvaluation(int star, string header, Func<double> remaining, double total, IReadOnlyList<GoalData> goals)
        {
            m_evaluationIcon.sprite = EvaluationView.StarSprite(m_stars, star);
            m_evaluationText.text = header;
            m_evaluationLeft = remaining;
            m_evaluationTotal = total;
            m_goalData = goals;
            m_evaluation.gameObject.SetActive(true);

            for (int i = 0; i < m_goals.Length; i++)
            {
                bool shown = i < goals.Count;
                m_goals[i].gameObject.SetActive(shown);

                if (shown)
                {
                    m_goals[i].SetIcon(InfoTile.Find(m_icons, goals[i].Icon));
                }
            }

            RefreshEvaluation();
            Stack();
            RefreshQuest();
            StartCoroutine(UiFx.Pulse((RectTransform)m_evaluation.transform));
        }

        public void HideEvaluation()
        {
            m_evaluationLeft = null;
            m_evaluation.gameObject.SetActive(false);
            Stack();
            RefreshQuest();
        }

        // 설계 54: 할 일 알약. done이면 진행 대신 체크와 「[코인] reward 받기」(reward가 null이면 코인 없이), 막 다 됐으면 한 번 톡
        public void ShowQuest(Sprite icon, string text, string value, bool done, string reward, string label)
        {
            m_questIcon.sprite = icon;
            m_questIcon.enabled = icon != null;
            m_questText.text = text;
            m_questValue.text = value;
            m_questValue.gameObject.SetActive(!done);
            m_questCheck.SetActive(done);
            m_questCoin.SetActive(done && reward != null);
            m_questReward.gameObject.SetActive(done && reward != null);
            m_questReward.text = reward;
            m_questLabel.text = label;
            bool pulse = done && !m_questDone && m_questShown;
            m_questShown = true;
            m_questDone = done;
            RefreshQuest();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)m_quest.transform);

            if (pulse && m_quest.gameObject.activeInHierarchy)
            {
                StartCoroutine(UiFx.Pulse((RectTransform)m_quest.transform));
            }
        }

        public void HideQuest()
        {
            m_questShown = false;
            RefreshQuest();
        }

        // 위 가운데는 평가 카드가 먼저다
        private void RefreshQuest()
        {
            m_quest.gameObject.SetActive(m_questShown && !m_evaluation.gameObject.activeSelf);
        }

        private void RefreshEvaluation()
        {
            double left = m_evaluationLeft();
            m_evaluationTime.text = BigNumberFormatter.Clock(left);
            m_evaluationBar.fillAmount = m_evaluationTotal > 0d ? Mathf.Clamp01((float)(left / m_evaluationTotal)) : 0f;

            for (int i = 0; i < m_goalData.Count && i < m_goals.Length; i++)
            {
                m_goals[i].SetValue(m_goalData[i].Value(), m_goalData[i].State());
            }
        }

        private void Reveal()
        {
            m_blessingTime.text = BigNumberFormatter.Clock(m_blessingLeft());
            m_blessing.alpha = 1f;
            m_blessing.gameObject.SetActive(true);
            Stack();
        }

        // 켜진 알약을 위에서부터 붙여 놓고, 쓴 코인 표시는 맨 아래 알약 밑으로. 평가 카드가 떠 있으면 그 아래부터
        private void Stack()
        {
            float y = m_pillTop;

            if (m_evaluation.gameObject.activeSelf)
            {
                RectTransform card = (RectTransform)m_evaluation.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                y = Mathf.Min(y, card.anchoredPosition.y - card.rect.height - k_Gap);
            }

            foreach (CanvasGroup pill in new[] { m_blessing, m_wave })
            {
                if (!pill.gameObject.activeSelf)
                {
                    continue;
                }

                RectTransform rect = (RectTransform)pill.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                y -= rect.sizeDelta.y + k_Gap;
            }

            ((RectTransform)m_spent.transform).anchoredPosition = new Vector2(m_spentRest.x, Mathf.Min(m_spentRest.y, y));
        }

        public void ShowSpent(string text)
        {
            m_spentText.text = text;
            m_spent.alpha = 1f;
            m_spent.gameObject.SetActive(true);
            m_spentLeft = k_SpentSeconds;
        }

        public void SetCoins(double target, Func<double, string> format)
        {
            m_format = format;
            m_from = m_first ? target : m_shown;
            m_target = target;
            m_t = m_first ? 1f : 0f;
            m_first = false;
            Apply();
        }

        private void Update()
        {
            if (m_blessingDelay > 0f)
            {
                m_blessingDelay -= Time.deltaTime;

                if (m_blessingDelay <= 0f)
                {
                    Reveal();
                }
            }
            else if (m_blessing.gameObject.activeSelf)
            {
                double left = m_blessingLeft();
                m_blessingTime.text = BigNumberFormatter.Clock(left);
                m_blessing.alpha = left > k_BlinkSeconds ? 1f : Mathf.Lerp(k_BlinkMin, 1f, Mathf.PingPong(Time.time * k_BlinkSpeed, 1f));
            }

            if (m_merchant.gameObject.activeSelf)
            {
                m_merchantTime.text = BigNumberFormatter.Clock(m_merchantLeft());
            }

            if (m_evaluationLeft != null)
            {
                RefreshEvaluation();
            }

            if (m_spentLeft > 0f)
            {
                m_spentLeft -= Time.unscaledDeltaTime;
                m_spent.alpha = Mathf.Clamp01(m_spentLeft / k_SpentFade);
                m_spent.gameObject.SetActive(m_spentLeft > 0f);
            }

            if (m_t >= 1f)
            {
                return;
            }

            m_t = Mathf.Min(1f, m_t + Time.deltaTime / k_CountSeconds);
            Apply();
        }

        private void Apply()
        {
            m_shown = m_from + (m_target - m_from) * m_t;
            m_coinsText.text = m_format(m_shown);
        }
    }
}
