using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 18 → 설계 20: 편집 모드 상점 카드 하나(그림·이름·값). 탭하면 화면 가운데 근처에 생기고, 그 뒤 사물을 끌어 옮긴다
    public sealed class EditCardView : MonoBehaviour, IPointerClickHandler
    {
        private static readonly Color k_Disabled = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_label;
        [SerializeField] private TextMeshProUGUI m_sub;
        [SerializeField] private TextMeshProUGUI m_subShadow;
        [SerializeField] private CanvasGroup m_group;

        public string KindId { get; private set; }

        public event Action<EditCardView> Clicked;

        public void Show(string kindId, Sprite icon, string label, string sub, bool enabled)
        {
            KindId = kindId;
            m_icon.sprite = icon;
            m_icon.enabled = icon != null;
            m_label.text = label;
            m_sub.text = sub;
            m_subShadow.text = sub;
            m_group.alpha = enabled ? 1f : k_Disabled.a;
            m_group.blocksRaycasts = enabled;
            gameObject.SetActive(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }
    }
}
