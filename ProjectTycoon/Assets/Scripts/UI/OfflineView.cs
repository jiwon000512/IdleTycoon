using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.Audio;
using GameKit.UI;
using ZooTycoon.Core;

namespace ZooTycoon.UI
{
    // 설계 43: 돌아왔을 때 팝업. 제목 · 비운 시간 · 아이콘 칸(코인 · 재료) · 판매 · 월급 두 줄 · 받기. 값은 이미 지갑에 들어가 있어 받기는 닫기다
    public sealed class OfflineView : UIView
    {
        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private TextMeshProUGUI m_time;
        [SerializeField] private RectTransform m_tiles;
        [SerializeField] private TextMeshProUGUI m_sales;
        [SerializeField] private TextMeshProUGUI m_wages;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;
        // Resources 밖 아이콘(코인)
        [SerializeField] private List<InfoTile.IconRef> m_icons = new List<InfoTile.IconRef>();

        private Vector2 m_rest;
        private Coroutine m_fx;

        public event Action Taken;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => Taken?.Invoke());
            m_button.onClick.AddListener(() => Taken?.Invoke());
            m_rest = m_panel.anchoredPosition;
            m_root.SetActive(false);
        }

        public void Show(string title, string time, IReadOnlyList<InfoTile.Data> tiles, string sales, string wages, string button)
        {
            m_title.text = title;
            m_time.text = time;
            InfoTile.Fill(m_tiles, tiles, m_icons);
            SetLine(m_sales, sales);
            SetLine(m_wages, wages);
            m_buttonLabel.text = button;
            m_root.SetActive(true);
            Run(UiFx.Appear(m_rootGroup, m_panel, m_rest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            Run(UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_Pay);
        }

        private static void SetLine(TMP_Text text, string value)
        {
            text.text = value ?? string.Empty;
            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
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
