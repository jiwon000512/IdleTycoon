using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 55: 가방(창고 · 유물) 팝업 머리의 탭 둘(점원 팝업 가게 탭과 같은 조각). 고른 탭은 selected 그림
    public sealed class BagTabs : MonoBehaviour
    {
        [SerializeField] private Button[] m_tabs;
        [SerializeField] private TextMeshProUGUI[] m_labels;
        [SerializeField] private Sprite m_normal;
        [SerializeField] private Sprite m_selected;

        private int m_current = -1;

        public event Action<int> Clicked;

        // 고른 탭을 또 누르면 그대로(유물 고른 것이 풀리지 않게, 2026-10-09 리뷰)
        private void Awake()
        {
            for (int i = 0; i < m_tabs.Length; i++)
            {
                int tab = i;
                m_tabs[i].onClick.AddListener(() =>
                {
                    if (tab != m_current)
                    {
                        Clicked?.Invoke(tab);
                    }
                });
            }
        }

        public void Show(int selected, string[] labels)
        {
            m_current = selected;

            for (int i = 0; i < m_tabs.Length; i++)
            {
                m_tabs[i].image.sprite = i == selected ? m_selected : m_normal;
                m_labels[i].text = labels[i];
            }
        }
    }
}
