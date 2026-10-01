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
    // 설계 31 · 32 좌판 팝업(뽑기만). 한 쪽에 위 글 · 카드 셋 · 아래 글 · 발치 버튼.
    // 뽑기 전: 카드 뒷면 셋 · 뽑기(반짝돌 값). 뽑으면 카드가 왼쪽부터 차례로 뒤집혀 후보(새 유물 / ★n로 · 아이콘 · 이름 · 고른 뒤 별 · 효과).
    // 고르면 나머지가 흐려지고 고른 카드의 새 별이 차오른 뒤 결과 글 · 「유물 보기」 · 「한 번 더」. 규칙은 Core Relics · RelicCartPresenter
    public sealed class RelicCartView : UIView
    {
        // 카드가 하나씩 뒤집히는 간격 · 한 장이 뒤집히는 초 · 새 별이 차오르기 전 쉼(초)
        private const float k_OfferStep = 0.12f;
        private const float k_FlipSeconds = 0.2f;
        private const float k_StarDelay = 0.25f;

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Sprite m_chip;
        [SerializeField] private Sprite m_chipSelected;
        [SerializeField] private TextMeshProUGUI m_offerLabel;
        [SerializeField] private RelicCard[] m_offers;
        [SerializeField] private TextMeshProUGUI m_offerHint;
        [SerializeField] private Button m_viewButton;
        [SerializeField] private TextMeshProUGUI m_viewLabel;
        [SerializeField] private Button m_drawButton;
        [SerializeField] private TextMeshProUGUI m_drawLabel;
        [SerializeField] private TextMeshProUGUI m_drawCost;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Coroutine m_offerFx;
        // 뒤집는 중인 앞면(연출이 끊기면 바로 앞면으로)
        private IReadOnlyList<RelicCard.Data> m_faces;
        private string m_draw;
        private string m_again;

        public event Action CloseRequested;
        public event Action DrawClicked;
        public event Action ViewClicked;
        public event Action<int> OfferClicked;

        public bool IsOpen => m_root.activeSelf;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_drawButton.onClick.AddListener(() => DrawClicked?.Invoke());
            m_viewButton.onClick.AddListener(() => ViewClicked?.Invoke());

            for (int i = 0; i < m_offers.Length; i++)
            {
                int index = i;
                m_offers[i].Button.onClick.AddListener(() => OfferClicked?.Invoke(index));
            }

            m_panelRest = m_panel.anchoredPosition;
            m_root.SetActive(false);
        }

        public void SetLabels(string title, string draw, string again, string view)
        {
            m_title.text = title;
            m_draw = draw;
            m_again = again;
            m_viewLabel.text = view;
        }

        // delay: 행상이 인사를 말하는 동안 기다렸다 뜬다(설계 34)
        public void Open(float delay)
        {
            if (m_root.activeSelf)
            {
                return;
            }

            m_root.SetActive(true);
            Run(ref m_fx, UiFx.Appear(m_rootGroup, m_panel, m_panelRest, delay));
            SoundManager.Instance.Play(SoundTable.k_UiOpen);
        }

        public void Close()
        {
            if (!m_root.activeSelf)
            {
                return;
            }

            StopOffer();
            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 뽑기 전: 카드 뒷면 셋, 뽑기 버튼만
        public void ShowReady(string label, string hint, int cost, bool canDraw)
        {
            StopOffer();

            foreach (RelicCard card in m_offers)
            {
                card.Root.gameObject.SetActive(true);
                card.SetBack(m_chip);
                card.Button.interactable = false;
            }

            m_offerLabel.text = label;
            m_offerHint.text = hint;
            ShowFoot(false, m_draw, cost, canDraw);
        }

        // 후보. animate면 뒷면에서 왼쪽부터 하나씩 뒤집힌다(그동안 누를 수 없다)
        public void ShowOffer(IReadOnlyList<RelicCard.Data> offers, string label, string hint, bool animate)
        {
            StopOffer();
            m_offerLabel.text = label;
            m_offerHint.text = hint;
            // 발치는 자리만 남겨 팝업 높이가 뽑기 전 · 결과와 같다
            m_viewButton.gameObject.SetActive(false);
            m_drawButton.gameObject.SetActive(false);

            for (int i = 0; i < m_offers.Length; i++)
            {
                bool shown = i < offers.Count;
                m_offers[i].Root.gameObject.SetActive(shown);

                if (shown)
                {
                    m_offers[i].Set(offers[i], m_chip, m_chipSelected);
                    m_offers[i].Button.interactable = !animate;
                }
            }

            if (animate)
            {
                SoundManager.Instance.Play(SoundTable.k_StatueRoll);
                m_faces = offers;
                m_offerFx = StartCoroutine(Flip());
            }
        }

        // 고른 카드만 남기고 흐리게, 새 별(stars번째)이 차오른 뒤 결과 글과 「유물 보기」 · 「한 번 더」
        public void ShowChosen(int index, int stars, string label, string hint, int cost, bool canDraw)
        {
            StopOffer();
            m_offerFx = StartCoroutine(Chosen(index, stars, label, hint, cost, canDraw));
        }

        // 카드마다 k_OfferStep씩 늦게, 가로로 접혔다(뒷면) 앞면으로 펴진다
        private IEnumerator Flip()
        {
            float half = k_FlipSeconds * 0.5f;
            float total = (m_faces.Count - 1) * k_OfferStep + k_FlipSeconds;
            bool[] flipped = new bool[m_faces.Count];

            for (int i = 0; i < m_faces.Count; i++)
            {
                m_offers[i].SetBack(m_chip);
            }

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < m_faces.Count; i++)
                {
                    float local = t - i * k_OfferStep;

                    if (local >= half && !flipped[i])
                    {
                        flipped[i] = true;
                        m_offers[i].Set(m_faces[i], m_chip, m_chipSelected);
                    }

                    m_offers[i].Root.localScale = new Vector3(Mathf.Clamp01(Mathf.Abs(local - half) / half), 1f, 1f);
                }

                yield return null;
            }

            m_offerFx = null;
            ShowFaces();
        }

        private void ShowFaces()
        {
            for (int i = 0; i < m_faces.Count; i++)
            {
                m_offers[i].Set(m_faces[i], m_chip, m_chipSelected);
                m_offers[i].Button.interactable = true;
            }

            m_faces = null;
        }

        private IEnumerator Chosen(int index, int stars, string label, string hint, int cost, bool canDraw)
        {
            RelicCard chosen = m_offers[index];
            chosen.Frame.sprite = m_chipSelected;

            for (int i = 0; i < m_offers.Length; i++)
            {
                m_offers[i].Button.interactable = false;
                m_offers[i].SetAlpha(i == index ? 1f : 0.35f);
            }

            Image star = chosen.Stars[Mathf.Clamp(stars, 1, chosen.Stars.Length) - 1];
            star.color = RelicCard.k_Shadow;
            m_offerLabel.text = string.Empty;
            m_offerHint.text = string.Empty;
            yield return new WaitForSecondsRealtime(k_StarDelay);
            star.color = Color.white;
            SoundManager.Instance.Play(SoundTable.k_StatueBlessed);
            yield return UiFx.Pulse(star.rectTransform);
            yield return UiFx.Pulse(chosen.Root);
            m_offerLabel.text = label;
            m_offerHint.text = hint;
            ShowFoot(true, m_again, cost, canDraw);
            m_offerFx = null;
        }

        private void ShowFoot(bool view, string draw, int cost, bool canDraw)
        {
            m_viewButton.gameObject.SetActive(view);
            m_drawButton.gameObject.SetActive(true);
            m_drawLabel.text = draw;
            m_drawCost.text = cost.ToString();
            m_drawButton.interactable = canDraw;
        }

        private void StopOffer()
        {
            if (m_offerFx != null)
            {
                StopCoroutine(m_offerFx);
                m_offerFx = null;
            }

            if (m_faces != null)
            {
                ShowFaces();
            }

            // 연출이 도중에 끊겨도 카드는 앞면 · 제 크기 · 제 빛깔로
            foreach (RelicCard card in m_offers)
            {
                card.Root.localScale = Vector3.one;
                card.SetAlpha(1f);
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
