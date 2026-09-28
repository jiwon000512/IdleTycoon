using UnityEngine;
using GameKit.Audio;
using ZooTycoon.Core;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 10: 공용 버튼 눌림. 누르는 동안 0.95배 + 누름 소리(막힌 버튼은 그대로). 모든 UI 버튼에 붙어 있다.
    // 기준점(pivot)이 모서리여도 가운데를 향해 줄도록 줄어드는 만큼 위치를 같이 옮긴다
    [RequireComponent(typeof(Button))]
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float k_PressedScale = 0.95f;

        private Button m_button;
        private RectTransform m_rect;
        private Vector2 m_shift;

        private void Awake()
        {
            m_button = GetComponent<Button>();
            m_rect = (RectTransform)transform;
        }

        private void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_button.IsInteractable() && m_shift == Vector2.zero)
            {
                m_shift = Vector2.Scale(new Vector2(0.5f, 0.5f) - m_rect.pivot, m_rect.rect.size) * (1f - k_PressedScale);
                m_rect.anchoredPosition += m_shift;
                transform.localScale = Vector3.one * k_PressedScale;
                SoundManager.Instance.Play(SoundTable.k_UiTap);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            m_rect.anchoredPosition -= m_shift;
            m_shift = Vector2.zero;
            transform.localScale = Vector3.one;
        }
    }
}
