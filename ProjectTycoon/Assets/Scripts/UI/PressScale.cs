using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 10: 공용 버튼 눌림. 누르는 동안 0.95배(막힌 버튼은 그대로). UIBaker가 모든 버튼에 붙인다
    [RequireComponent(typeof(Button))]
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float k_PressedScale = 0.95f;

        private Button m_button;

        private void Awake()
        {
            m_button = GetComponent<Button>();
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_button.IsInteractable())
            {
                transform.localScale = Vector3.one * k_PressedScale;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }
    }
}
