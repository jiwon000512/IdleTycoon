using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 연출 1차: 상단 바는 코인. 값이 바뀌면 0.25초 동안 숫자가 오른다(첫 표시는 즉시)
    // 숫자는 공용 코인 캡슐(Prefabs/UI/CoinPill) 안의 금색 글자. 설계 25: 코인 캡슐 오른쪽에 재료 알약(아이콘 + 개수) 줄
    public sealed class TopBarView : UIView
    {
        private const float k_CountSeconds = 0.25f;
        private const float k_SpentSeconds = 0.8f;
        private const float k_SpentFade = 0.3f;
        private const float k_ItemGap = 8f;

        [SerializeField] private TMP_Text m_coinsText;
        [Tooltip("코인을 쓴 순간 캡슐 아래에 잠깐 뜨는 「-24」(작은 코인 캡슐)")]
        [SerializeField] private CanvasGroup m_spent;
        [SerializeField] private TMP_Text m_spentText;
        [SerializeField] private RectTransform m_coinPill;
        [Tooltip("재료 알약 줄(코인 캡슐 오른쪽을 따라간다)과 알약 틀(자식 Icon · Count)")]
        [SerializeField] private RectTransform m_itemRow;
        [SerializeField] private RectTransform m_itemTemplate;

        private readonly List<RectTransform> m_items = new List<RectTransform>();

        private Func<double, string> m_format;
        private double m_from;
        private double m_target;
        private double m_shown;
        private float m_t = 1f;
        private bool m_first = true;
        private float m_spentLeft;

        public bool SpentVisible => m_spentLeft > 0f;

        private void Awake()
        {
            m_spent.gameObject.SetActive(false);
            m_itemTemplate.gameObject.SetActive(false);
        }

        // (아이콘 경로, 개수 글) 재료마다
        public void SetItems(IReadOnlyList<(string IconPath, string Count)> items)
        {
            while (m_items.Count < items.Count)
            {
                m_items.Add(Instantiate(m_itemTemplate, m_itemRow));
            }

            for (int i = 0; i < m_items.Count; i++)
            {
                m_items[i].gameObject.SetActive(i < items.Count);

                if (i < items.Count)
                {
                    m_items[i].Find("Icon").GetComponent<Image>().sprite = Resources.Load<Sprite>(items[i].IconPath);
                    m_items[i].Find("Count").GetComponent<TMP_Text>().text = items[i].Count;
                }
            }
        }

        // 코인 캡슐 폭은 숫자에 따라 늘어난다
        private void LateUpdate()
        {
            m_itemRow.anchoredPosition = m_coinPill.anchoredPosition + new Vector2(m_coinPill.rect.width + k_ItemGap, 0f);
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
