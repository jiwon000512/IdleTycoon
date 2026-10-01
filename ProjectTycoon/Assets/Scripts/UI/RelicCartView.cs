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
    // 설계 31 유물 수레 팝업. 첫 화면: 끼운 유물 칸 셋(아이콘 · 별 · 효과) + 모은 유물 격자(못 모은 칸은 그림자) + 고른 유물 정보 줄(끼우기/빼기) + 안내 + 뽑기 버튼(반짝돌 값).
    // 뽑으면 후보 화면: 카드 셋(새 유물 / ★n로 · 아이콘 · 이름 · 고른 뒤 별 · 효과)이 차례로 뜨고, 고르면 부풀고 첫 화면으로. 규칙은 Core Relics · RelicCartPresenter
    public sealed class RelicCartView : UIView
    {
        // 카드 하나(칸 · 격자 · 후보가 같은 모양, 없는 글은 비워 둔다)
        [Serializable]
        public sealed class Card
        {
            public RectTransform Root;
            public Button Button;
            public Image Frame;
            public Image Icon;
            public Image[] Stars;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Effect;
            public GameObject Badge;
            public TextMeshProUGUI BadgeText;
        }

        public sealed class CardData
        {
            public string IconPath;
            public string Name;
            public string Effect;
            public int Stars;
            public bool Owned = true;
            public bool Selected;
            public string Badge;
        }

        // 후보 카드가 하나씩 뜨는 간격 · 고른 카드가 부푼 뒤 첫 화면으로 돌아가기까지(초)
        private const float k_OfferStep = 0.12f;
        private const float k_ChosenSeconds = 0.45f;
        // 못 모은 유물 · 빈 별은 외곽선 색 그림자
        private static readonly Color k_Shadow = new Color32(0x34, 0x20, 0x20, 90);

        [SerializeField] private GameObject m_root;
        [SerializeField] private CanvasGroup m_rootGroup;
        [SerializeField] private RectTransform m_panel;
        [SerializeField] private Button m_dim;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_title;
        [SerializeField] private Sprite m_chip;
        [SerializeField] private Sprite m_chipSelected;
        [Header("첫 화면")]
        [SerializeField] private GameObject m_mainPage;
        [SerializeField] private TextMeshProUGUI m_slotsLabel;
        [SerializeField] private Card[] m_slots;
        [SerializeField] private TextMeshProUGUI m_collectionLabel;
        [SerializeField] private Card[] m_cells;
        [SerializeField] private GameObject m_info;
        [SerializeField] private Card m_infoCard;
        [SerializeField] private Button m_infoButton;
        [SerializeField] private TextMeshProUGUI m_infoButtonLabel;
        [SerializeField] private TextMeshProUGUI m_hint;
        [SerializeField] private GameObject m_foot;
        [SerializeField] private Button m_drawButton;
        [SerializeField] private TextMeshProUGUI m_drawLabel;
        [SerializeField] private TextMeshProUGUI m_drawCost;
        [Header("후보 화면")]
        [SerializeField] private GameObject m_offerPage;
        [SerializeField] private TextMeshProUGUI m_offerLabel;
        [SerializeField] private Card[] m_offers;
        [SerializeField] private TextMeshProUGUI m_offerHint;

        private Vector2 m_panelRest;
        private Coroutine m_fx;
        private Coroutine m_offerFx;
        private string m_emptySlot;
        private Color m_effectColor;

        public event Action CloseRequested;
        public event Action DrawClicked;
        public event Action InfoClicked;
        public event Action<int> SlotClicked;
        public event Action<int> CellClicked;
        public event Action<int> OfferClicked;

        public bool IsOpen => m_root.activeSelf;
        public int SlotCount => m_slots.Length;
        public int CellCount => m_cells.Length;

        private void Awake()
        {
            m_dim.onClick.AddListener(() => CloseRequested?.Invoke());
            m_closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            m_drawButton.onClick.AddListener(() => DrawClicked?.Invoke());
            m_infoButton.onClick.AddListener(() => InfoClicked?.Invoke());
            Listen(m_slots, i => SlotClicked?.Invoke(i));
            Listen(m_cells, i => CellClicked?.Invoke(i));
            Listen(m_offers, i => OfferClicked?.Invoke(i));
            m_panelRest = m_panel.anchoredPosition;
            m_effectColor = m_slots[0].Effect.color;
            m_root.SetActive(false);
        }

        public void SetLabels(string title, string slots, string emptySlot, string draw, string pick, string pickHint)
        {
            m_title.text = title;
            m_slotsLabel.text = slots;
            m_emptySlot = emptySlot;
            m_drawLabel.text = draw;
            m_offerLabel.text = pick;
            m_offerHint.text = pickHint;
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

            StopOffer();
            Run(ref m_fx, UiFx.Vanish(m_rootGroup, m_root));
            SoundManager.Instance.Play(SoundTable.k_UiClose);
        }

        // 첫 화면: 칸(null이면 빈 칸) · 격자 · 「모은 유물 n/m」 · 고른 유물(null이면 정보 줄을 숨김)과 버튼 글 · 안내 · 뽑기
        public void ShowMain(IReadOnlyList<CardData> slots, IReadOnlyList<CardData> cells, string collection,
            CardData info, string infoButton, bool infoButtonOn, string hint, int cost, bool canDraw)
        {
            StopOffer();
            m_mainPage.SetActive(true);
            m_offerPage.SetActive(false);
            m_foot.SetActive(true);

            for (int i = 0; i < m_slots.Length; i++)
            {
                SetCard(m_slots[i], slots[i]);
                // 빈 칸은 안내 글 색으로 「빈 칸」
                m_slots[i].Effect.text = slots[i] != null ? slots[i].Effect : m_emptySlot;
                m_slots[i].Effect.color = slots[i] != null ? m_effectColor : m_hint.color;
            }

            for (int i = 0; i < m_cells.Length; i++)
            {
                SetCard(m_cells[i], cells[i]);
            }

            m_collectionLabel.text = collection;
            m_info.SetActive(info != null);

            if (info != null)
            {
                SetCard(m_infoCard, info);
                m_infoButtonLabel.text = infoButton;
                m_infoButton.interactable = infoButtonOn;
            }

            m_hint.text = hint;
            m_drawCost.text = cost.ToString();
            m_drawButton.interactable = canDraw;
        }

        // 후보 화면. animate면 카드가 왼쪽부터 하나씩 톡 뜬다
        public void ShowOffer(IReadOnlyList<CardData> offers, bool animate)
        {
            StopOffer();
            m_mainPage.SetActive(false);
            m_foot.SetActive(false);
            m_offerPage.SetActive(true);
            m_hint.text = string.Empty;

            for (int i = 0; i < m_offers.Length; i++)
            {
                bool shown = i < offers.Count;
                m_offers[i].Root.gameObject.SetActive(shown);

                if (shown)
                {
                    SetCard(m_offers[i], offers[i]);
                    m_offers[i].Button.interactable = true;
                }
            }

            if (animate)
            {
                SoundManager.Instance.Play(SoundTable.k_StatueRoll);
                m_offerFx = StartCoroutine(RevealOffers(offers.Count));
            }
        }

        // 고른 카드가 부풀고 다른 카드는 흐려진 뒤 done(첫 화면으로)
        public void PlayChosen(int index, Action done)
        {
            StopOffer();
            m_offerFx = StartCoroutine(Chosen(index, done));
        }

        // 첫 화면의 칸 · 격자 하나가 한 번 부푼다(고른 유물이 들어간 자리)
        public void PulseSlot(int index)
        {
            StartCoroutine(UiFx.Pulse(m_slots[index].Root));
        }

        public void PulseCell(int index)
        {
            StartCoroutine(UiFx.Pulse(m_cells[index].Root));
        }

        private IEnumerator RevealOffers(int count)
        {
            for (int i = 0; i < count; i++)
            {
                m_offers[i].Root.localScale = Vector3.zero;
            }

            for (int i = 0; i < count; i++)
            {
                m_offers[i].Root.localScale = Vector3.one;
                StartCoroutine(UiFx.Pulse(m_offers[i].Root));
                yield return new WaitForSeconds(k_OfferStep);
            }

            m_offerFx = null;
        }

        private IEnumerator Chosen(int index, Action done)
        {
            for (int i = 0; i < m_offers.Length; i++)
            {
                m_offers[i].Button.interactable = false;
                m_offers[i].Root.localScale = Vector3.one;
                SetAlpha(m_offers[i], i == index ? 1f : 0.35f);
            }

            SoundManager.Instance.Play(SoundTable.k_StatueBlessed);
            yield return UiFx.Pulse(m_offers[index].Root);
            yield return new WaitForSeconds(k_ChosenSeconds);
            m_offerFx = null;
            StopOffer();
            done();
        }

        private void SetCard(Card card, CardData data)
        {
            bool has = data != null;
            card.Icon.enabled = has;
            card.Icon.sprite = has ? Resources.Load<Sprite>(data.IconPath) : null;
            card.Icon.color = has && data.Owned ? Color.white : k_Shadow;

            if (card.Frame != null)
            {
                card.Frame.sprite = has && data.Selected ? m_chipSelected : m_chip;
            }

            for (int i = 0; i < card.Stars.Length; i++)
            {
                card.Stars[i].enabled = has;
                card.Stars[i].color = has && i < data.Stars ? Color.white : k_Shadow;
            }

            if (card.Name != null)
            {
                card.Name.text = has ? data.Name : string.Empty;
            }

            if (card.Effect != null)
            {
                card.Effect.text = has ? data.Effect : string.Empty;
            }

            if (card.Badge != null)
            {
                card.Badge.SetActive(has && !string.IsNullOrEmpty(data.Badge));
                card.BadgeText.text = has ? data.Badge : string.Empty;
            }
        }

        private static void SetAlpha(Card card, float alpha)
        {
            foreach (Graphic graphic in card.Root.GetComponentsInChildren<Graphic>())
            {
                Color color = graphic.color;
                color.a = graphic == card.Icon || Array.IndexOf(card.Stars, graphic) >= 0 ? Mathf.Min(color.a, alpha) : alpha;
                graphic.color = color;
            }
        }

        private static void Listen(Card[] cards, Action<int> clicked)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].Button.onClick.AddListener(() => clicked(index));
            }
        }

        private void StopOffer()
        {
            if (m_offerFx != null)
            {
                StopCoroutine(m_offerFx);
                m_offerFx = null;
            }

            // 연출이 도중에 끊겨도 카드는 제 크기 · 제 빛깔로
            foreach (Card card in m_offers)
            {
                card.Root.localScale = Vector3.one;
                SetAlpha(card, 1f);
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
