using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 18 → 설계 20: 편집 모드 상점 카드 하나(그림·이름·값). 탭하면 화면 가운데 근처에 생기고, 그 뒤 사물을 끌어 옮긴다.
    // 편집 패널 D(아트방, 2026-10-05): 값은 코인 + 진갈색 값(보관 · MAX는 코인 없이). 별 조건으로 잠긴 카드는 칩 모양 어두운 덮개 + 자물쇠 + 조건,
    // 이름은 덮개 위 크림색으로 보이고 흐리지 않는다. 코인이 모자란 카드만 흐리다
    public sealed class EditCardView : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color k_Disabled = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color k_Ink = new Color32(0x2E, 0x23, 0x20, 0xFF);
        private static readonly Color k_Cream = new Color32(0xFB, 0xF4, 0xE6, 0xFF);

        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_label;
        [Tooltip("값 줄(코인 + 값)")]
        [SerializeField] private GameObject m_price;
        [SerializeField] private Image m_coin;
        [SerializeField] private TextMeshProUGUI m_value;
        [Tooltip("잠긴 카드 덮개(자물쇠 + 조건 글)")]
        [SerializeField] private GameObject m_lockCover;
        [SerializeField] private TextMeshProUGUI m_lockText;
        [SerializeField] private CanvasGroup m_group;

        public string KindId { get; private set; }

        public event Action<EditCardView> Clicked;

        // value: 값 · 「보관 n」 · MAX · 「★n 필요」. coin: 값 앞에 코인, locked: 별 조건 덮개
        public void Show(string kindId, Sprite icon, string label, string value, bool coin, bool locked, bool enabled)
        {
            KindId = kindId;
            m_icon.sprite = icon;
            m_icon.enabled = icon != null;
            m_label.text = label;
            m_label.color = locked ? k_Cream : k_Ink;
            m_value.text = value;
            m_coin.gameObject.SetActive(coin);
            m_price.SetActive(!locked);
            m_lockCover.SetActive(locked);
            m_lockText.text = value;
            m_group.alpha = enabled || locked ? 1f : k_Disabled.a;
            m_group.blocksRaycasts = enabled;
            gameObject.SetActive(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }
    }
}
