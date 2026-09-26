using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooTycoon.UI
{
    // 설계 21: 점원 팝업의 줄 하나(자리 줄·후보 줄 공용): 그림 · 이름 · 상태 · 오른쪽 버튼
    public sealed class ClerkRowView : MonoBehaviour
    {
        [SerializeField] private Image m_icon;
        [SerializeField] private TextMeshProUGUI m_name;
        [SerializeField] private TextMeshProUGUI m_sub;
        [SerializeField] private Button m_button;
        [SerializeField] private TextMeshProUGUI m_buttonLabel;

        public event Action<ClerkRowView> ButtonClicked;

        private void Awake()
        {
            m_button.onClick.AddListener(() => ButtonClicked?.Invoke(this));
        }

        public void Show(Sprite icon, string name, string sub, string button, bool enabled)
        {
            m_icon.sprite = icon;
            m_icon.enabled = icon != null;
            m_name.text = name;
            m_sub.text = sub;
            m_buttonLabel.text = button;
            m_button.interactable = enabled;
            gameObject.SetActive(true);
        }
    }
}
