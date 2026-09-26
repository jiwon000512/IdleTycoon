using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 18 편집 모드 화면: 편집 버튼(늘 보임) · 편집 중에는 아래 패널(상점 카드 줄 + 완료)과 화면 전체 터치 판.
    // 화면 좌표는 카메라로 월드 좌표로 바꿔 넘긴다(곳 좌표로 바꾸는 것은 Presenter). 패널 위에 놓으면 보관.
    // 설계 20: 카드는 탭. 패널 위 화면 가운데의 월드 점을 같이 넘긴다
    public sealed class EditModeView : UIView
    {
        [Serializable]
        private struct KindIcon
        {
            public string Id;
            public Sprite Sprite;
        }

        public sealed class CardData
        {
            public string KindId;
            public string IconPath;
            public string Label;
            public string Sub;
            public bool Enabled;
        }

        [SerializeField] private Button m_editButton;
        [SerializeField] private Button m_doneButton;
        [SerializeField] private GameObject m_panel;
        [SerializeField] private RectTransform m_panelRect;
        [SerializeField] private RectTransform m_cardsRoot;
        [SerializeField] private EditCardView m_cardTemplate;
        [SerializeField] private PointerRelay m_dragArea;
        [SerializeField] private TextMeshProUGUI m_storeHint;
        [SerializeField] private TextMeshProUGUI m_doneLabel;
        [Tooltip("Resources에 없는 종류(진열대·오븐·계산대)의 카드 그림. 굽기가 채운다")]
        [SerializeField] private List<KindIcon> m_kindIcons = new List<KindIcon>();

        private readonly List<EditCardView> m_cards = new List<EditCardView>();
        private readonly Dictionary<string, Sprite> m_icons = new Dictionary<string, Sprite>();
        private Camera m_camera;

        public event Action EditClicked;
        public event Action DoneClicked;
        // 카드 종류와 패널 위 화면 가운데의 월드 점
        public event Action<string, System.Numerics.Vector2> CardClicked;
        public event Action<System.Numerics.Vector2> WorldPointerDown;
        public event Action<System.Numerics.Vector2> WorldPointerMoved;
        public event Action<System.Numerics.Vector2, bool> WorldPointerUp;

        private void Awake()
        {
            m_editButton.onClick.AddListener(() => EditClicked?.Invoke());
            m_doneButton.onClick.AddListener(() => DoneClicked?.Invoke());
            m_dragArea.PointerDown += data => WorldPointerDown?.Invoke(World(data));
            m_dragArea.Dragged += data => WorldPointerMoved?.Invoke(World(data));
            m_dragArea.PointerUp += data => WorldPointerUp?.Invoke(World(data), OverPanel(data));
            m_cardTemplate.gameObject.SetActive(false);
            SetEditing(false);
        }

        public void SetEditing(bool editing)
        {
            m_panel.SetActive(editing);
            m_dragArea.gameObject.SetActive(editing);
            m_editButton.gameObject.SetActive(!editing);
        }

        public void SetTexts(string storeHint, string done)
        {
            m_storeHint.text = storeHint;
            m_doneLabel.text = done;
        }

        public void SetCards(IReadOnlyList<CardData> cards)
        {
            while (m_cards.Count < cards.Count)
            {
                EditCardView card = Instantiate(m_cardTemplate, m_cardsRoot);
                card.Clicked += Card_Clicked;
                m_cards.Add(card);
            }

            for (int i = 0; i < m_cards.Count; i++)
            {
                if (i >= cards.Count)
                {
                    m_cards[i].gameObject.SetActive(false);
                    continue;
                }

                CardData data = cards[i];
                m_cards[i].Show(data.KindId, Icon(data.KindId, data.IconPath), data.Label, data.Sub, data.Enabled);
            }
        }

        private Sprite Icon(string kindId, string path)
        {
            if (m_icons.TryGetValue(kindId, out Sprite sprite))
            {
                return sprite;
            }

            foreach (KindIcon icon in m_kindIcons)
            {
                if (icon.Id == kindId)
                {
                    sprite = icon.Sprite;
                }
            }

            if (sprite == null && path != null)
            {
                sprite = Resources.Load<Sprite>(path);
            }

            m_icons[kindId] = sprite;
            return sprite;
        }

        private void Card_Clicked(EditCardView card)
        {
            Vector3[] corners = new Vector3[4];
            m_panelRect.GetWorldCorners(corners);
            float panelTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            Vector2 center = new Vector2(Screen.width * 0.5f, (panelTop + Screen.height) * 0.5f);
            CardClicked?.Invoke(card.KindId, World(center));
        }

        private bool OverPanel(PointerEventData data)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(m_panelRect, data.position, data.pressEventCamera);
        }

        private System.Numerics.Vector2 World(PointerEventData data)
        {
            return World(data.position);
        }

        private System.Numerics.Vector2 World(Vector2 screen)
        {
            if (m_camera == null)
            {
                m_camera = Camera.main;
            }

            Vector3 world = m_camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -m_camera.transform.position.z));
            return new System.Numerics.Vector2(world.x, world.y);
        }
    }
}
