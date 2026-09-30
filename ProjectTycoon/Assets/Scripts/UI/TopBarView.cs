using System;
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
    // 설계 30: 석상 축복이 걸린 동안 코인 아래 축복 알약(아이콘 · 효과 · 남은 시간, 마지막 10초는 깜빡임). 그동안 쓴 코인 표시는 그 아래로 내려간다
    public sealed class TopBarView : UIView
    {
        private const float k_CountSeconds = 0.25f;
        private const float k_SpentSeconds = 0.8f;
        private const float k_SpentFade = 0.3f;
        private const double k_BlinkSeconds = 10d;
        private const float k_BlinkSpeed = 4f;
        private const float k_BlinkMin = 0.45f;

        [SerializeField] private TMP_Text m_coinsText;
        [Tooltip("코인을 쓴 순간 캡슐 아래에 잠깐 뜨는 「-24」(작은 코인 캡슐)")]
        [SerializeField] private CanvasGroup m_spent;
        [SerializeField] private TMP_Text m_spentText;
        [Tooltip("설계 30 축복 알약(코인 캡슐 바로 아래)")]
        [SerializeField] private CanvasGroup m_blessing;
        [SerializeField] private Image m_blessingIcon;
        [SerializeField] private TMP_Text m_blessingText;
        [SerializeField] private TMP_Text m_blessingTime;

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

        public bool SpentVisible => m_spentLeft > 0f;

        private void Awake()
        {
            m_spent.gameObject.SetActive(false);
            m_blessing.gameObject.SetActive(false);
            m_spentRest = ((RectTransform)m_spent.transform).anchoredPosition;
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
            PlaceSpent();
            SoundManager.Instance.Play(SoundTable.k_BlessingEnd);
        }

        private void Reveal()
        {
            m_blessingTime.text = BigNumberFormatter.Clock(m_blessingLeft());
            m_blessing.alpha = 1f;
            m_blessing.gameObject.SetActive(true);
            PlaceSpent();
        }

        private void PlaceSpent()
        {
            RectTransform blessing = (RectTransform)m_blessing.transform;
            float drop = m_blessing.gameObject.activeSelf ? blessing.sizeDelta.y + m_spentRest.y - blessing.anchoredPosition.y : 0f;
            ((RectTransform)m_spent.transform).anchoredPosition = m_spentRest - new Vector2(0f, Mathf.Max(0f, drop));
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
