using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 18: 편집 모드 상점 카드 하나(그림·이름·값). 카드를 끌면 굴에 놓는 드래그가 시작된다
    public sealed class EditCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Color k_Disabled = new Color(1f, 1f, 1f, 0.45f);

        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_label;
        [SerializeField] private TextMeshProUGUI m_sub;
        [SerializeField] private CanvasGroup m_group;

        public string KindId { get; private set; }

        public event Action<EditCardView, PointerEventData> DragBegan;
        public event Action<EditCardView, PointerEventData> Dragged;
        public event Action<EditCardView, PointerEventData> DragEnded;

        public void Show(string kindId, Sprite icon, string label, string sub, bool enabled)
        {
            KindId = kindId;
            m_icon.sprite = icon;
            m_icon.enabled = icon != null;
            m_label.text = label;
            m_sub.text = sub;
            m_group.alpha = enabled ? 1f : k_Disabled.a;
            m_group.blocksRaycasts = enabled;
            gameObject.SetActive(true);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DragEnded?.Invoke(this, eventData);
        }
    }
}
