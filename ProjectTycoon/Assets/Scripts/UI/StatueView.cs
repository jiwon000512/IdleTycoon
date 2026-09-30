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
    // 설계 30 석상 축복 팝업: 걸린 축복 카드(아이콘 · 이름 · 효과 · 남은 시간) + 안내 + 빌기 버튼(쉬는 시간이면 「다시 빌기 7:30」, 꺼짐). 광장 석상 앞 버튼으로 열린다.
    // 빌면 카드가 축복 이름을 빠르게 넘기다 멈춘다(소리 + 한 번 부풂). 남은 시간 · 쉬는 시간은 Presenter가 준 읽기 함수로 매 프레임 그린다. 규칙은 Core Blessing · StatuePresenter
    public sealed class StatueView : UIView
    {
        // 카드 하나. 걸린 축복이 없으면 null
        public sealed class CardData
        {
            public string IconPath;
            public string Name;
            public string Effect;
            public Func<double> Remaining;
        }

        // 이름이 도는 시간(상단 바 축복 알약도 이만큼 늦게 보인다) · 이름이 바뀌는 간격(초)
        public const float k_SpinSeconds = 0.9f;
        private const float k_SpinStep = 0.07f;

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [Tooltip("축복 카드: 아이콘 · 이름 · 효과 · 남은 시간")]
        [SerializeField] private RectTransform m_card;
        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_name;
        [SerializeField] private TextMeshProUGUI m_effect;
        [SerializeField] private TextMeshProUGUI m_time;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private Button m_prayButton;
        [SerializeField] private TextMeshProUGUI m_prayLabel;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Coroutine m_spin;
        private string m_prayText;
        private Func<double, string> m_againText;
        private CardData m_shown;
        private Func<double> m_cooldown;
        private bool m_canPray;

        public event Action CloseRequested;
        public event Action PrayClicked;

        public bool IsOpen => m_root.activeSelf;
        // 이름이 도는 중(그동안 카드를 덮어 그리지 않는다)
        public bool Spinning => m_spin != null;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_prayButton.onClick.AddListener(() => PrayClicked?.Invoke());
            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
        }

        // again: 쉬는 시간 남은 초 → 버튼 글(「다시 빌기 7:30」)
        public void SetLabels(string title, string hint, string pray, Func<double, string> again)
        {
            m_title.text = title;
            m_hint.text = hint;
            m_prayText = pray;
            m_againText = again;
        }

        public void Open()
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, 0f));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            StopSpin();
            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 카드(null이면 none 글) · 쉬는 시간(남은 초를 읽는 함수) · 빌 수 있나
        public void Show(CardData card, string none, Func<double> cooldown, bool canPray)
        {
            StopSpin();
            SetCard(card, none);
            SetPray(cooldown, canPray);
        }

        // 빌었다: 이름(names)을 빠르게 넘기다 card로 멈춘다
        public void PlayPray(CardData card, IReadOnlyList<string> names, Func<double> cooldown)
        {
            StopSpin();
            SetPray(cooldown, false);
            SoundManager.Instance.Play(SoundTable.k_StatueRoll);
            m_spin = StartCoroutine(Spin(card, names));
        }

        private void Update()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            if (m_shown != null && !Spinning)
            {
                m_time.text = BigNumberFormatter.Clock(m_shown.Remaining());
            }

            if (!m_canPray)
            {
                m_prayLabel.text = m_againText(m_cooldown());
            }
        }

        private IEnumerator Spin(CardData card, IReadOnlyList<string> names)
        {
            m_icon.enabled = false;
            m_effect.text = string.Empty;
            m_time.text = string.Empty;
            float elapsed = 0f;

            while (elapsed < k_SpinSeconds)
            {
                m_name.text = names[UnityEngine.Random.Range(0, names.Count)];
                yield return new WaitForSeconds(k_SpinStep);
                elapsed += k_SpinStep;
            }

            m_spin = null;
            SetCard(card, string.Empty);
            StartCoroutine(UiFx.Pulse(m_card));
            SoundManager.Instance.Play(SoundTable.k_StatueBlessed);
        }

        private void SetCard(CardData card, string none)
        {
            m_icon.enabled = card != null;
            m_icon.sprite = card != null ? Resources.Load<Sprite>(card.IconPath) : null;
            m_name.text = card != null ? card.Name : none;
            m_effect.text = card != null ? card.Effect : string.Empty;
            m_shown = card;
            m_time.text = card != null ? BigNumberFormatter.Clock(card.Remaining()) : string.Empty;
        }

        private void SetPray(Func<double> cooldown, bool canPray)
        {
            m_cooldown = cooldown;
            m_canPray = canPray;
            m_prayButton.interactable = canPray;
            m_prayLabel.text = canPray ? m_prayText : m_againText(cooldown());
        }

        private void StopSpin()
        {
            if (m_spin != null)
            {
                StopCoroutine(m_spin);
                m_spin = null;
            }
        }

        private void Run(ref Coroutine slot, IEnumerator routine)
        {
            if (slot != null)
            {
                StopCoroutine(slot);
            }

            slot = StartCoroutine(routine);
        }
    }
}
