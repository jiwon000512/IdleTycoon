using System;
using UnityEngine;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 연출 1차: 상단 바는 코인만. 값이 바뀌면 0.25초 동안 숫자가 오른다(첫 표시는 즉시)
    // 상단 HUD 시안 C: 숫자는 금색 + 같은 글자의 진갈색 그림자(그림자가 캡슐 폭을 잡는다)
    public sealed class TopBarView : UIView
    {
        private const float k_CountSeconds = 0.25f;

        [SerializeField] private TMP_Text m_coinsText;
        [SerializeField] private TMP_Text m_coinsShadow;

        private Func<double, string> m_format;
        private double m_from;
        private double m_target;
        private double m_shown;
        private float m_t = 1f;
        private bool m_first = true;

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
            m_coinsShadow.text = m_coinsText.text;
        }
    }
}
