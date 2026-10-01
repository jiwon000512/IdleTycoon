using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZooTycoon.UI
{
    // 설계 09: 플로팅 조이스틱. 이 영역(투명 이미지)을 누른 곳에 받침이 옮겨 오고, 손잡이는 반지름 안에서 손가락을 따라간다.
    // 쉬는 동안은 제자리에 흐리게 보인다. 값은 길이 0~1(뗄 때 0).
    // PC: 누르지 않는 동안 WASD · 방향키도 같은 값으로 보낸다(손잡이가 그쪽으로 기운다)
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
        private bool m_pressed;
        private Vector2 m_keys;

        public event Action<Vector2> Moved;

        private void Awake()
        {
            m_area = (RectTransform)transform;
            m_rest = m_base.anchoredPosition;
            m_group.alpha = m_idleAlpha;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_pressed = true;
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
            m_pressed = false;
            m_keys = Vector2.zero;
            m_base.anchoredPosition = m_rest;
            m_knob.anchoredPosition = Vector2.zero;
            m_group.alpha = m_idleAlpha;
            OnMoved(Vector2.zero);
        }

        // 키보드 방향이 바뀔 때만 보낸다(누르고 있는 조이스틱이 먼저)
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (m_pressed || keyboard == null)
            {
                return;
            }

            Vector2 keys = new Vector2(
                Axis(keyboard.dKey, keyboard.rightArrowKey) - Axis(keyboard.aKey, keyboard.leftArrowKey),
                Axis(keyboard.wKey, keyboard.upArrowKey) - Axis(keyboard.sKey, keyboard.downArrowKey)).normalized;

            if (keys == m_keys)
            {
                return;
            }

            m_keys = keys;
            m_knob.anchoredPosition = keys * m_radius;
            m_group.alpha = keys == Vector2.zero ? m_idleAlpha : 1f;
            OnMoved(keys);
        }

        private static float Axis(UnityEngine.InputSystem.Controls.KeyControl key, UnityEngine.InputSystem.Controls.KeyControl arrow)
        {
            return key.isPressed || arrow.isPressed ? 1f : 0f;
        }

        // 영역 피벗 기준 좌표. 받침 앵커가 영역 피벗과 같은 점이라 그대로 받침 위치가 된다
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
