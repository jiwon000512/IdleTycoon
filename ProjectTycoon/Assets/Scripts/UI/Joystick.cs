using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZooTycoon.UI
{
    // 설계 09: 플로팅 조이스틱. 이 영역(투명 이미지)을 누른 곳에 받침이 옮겨 오고, 손잡이는 반지름 안에서 손가락을 따라간다.
    // 쉬는 동안은 제자리에 흐리게 보인다. 값은 길이 0~1(뗄 때 0)
    public sealed class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform m_base;
        [SerializeField] private RectTransform m_knob;
        [SerializeField] private CanvasGroup m_group;
        [Tooltip("손잡이가 받침 가운데에서 움직이는 최대 거리(캔버스 px)")]
        [SerializeField] private float m_radius = 96f;
        [Tooltip("쉬는 동안 받침 투명도")]
        [SerializeField] private float m_idleAlpha = 0.45f;

        private RectTransform m_area;
        private Vector2 m_rest;

        public event Action<Vector2> Moved;

        private void Awake()
        {
            m_area = (RectTransform)transform;
            m_rest = m_base.anchoredPosition;
            m_group.alpha = m_idleAlpha;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_base.anchoredPosition = Local(eventData);
            m_group.alpha = 1f;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 offset = Vector2.ClampMagnitude(Local(eventData) - m_base.anchoredPosition, m_radius);
            m_knob.anchoredPosition = offset;
            OnMoved(offset / m_radius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        // 누른 채 앱이 포커스를 잃으면 떼는 신호가 오지 않아 웜뱃이 계속 걷는다. 포커스를 잃을 때도 뗀다
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Release();
            }
        }

        private void Release()
        {
            m_base.anchoredPosition = m_rest;
            m_knob.anchoredPosition = Vector2.zero;
            m_group.alpha = m_idleAlpha;
            OnMoved(Vector2.zero);
        }

        // 받침은 영역 가운데에 고정점을 둔다(영역 피벗 = 가운데)
        private Vector2 Local(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_area, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local;
        }

        private void OnMoved(Vector2 value)
        {
            Moved?.Invoke(value);
        }
    }
}
