using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public sealed class FacilityButtonView : UIView
    {
        [SerializeField] private Button m_button;
        [SerializeField] private TMP_Text m_labelText;

        public event Action Clicked;

        private void OnEnable()
        {
            m_button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            m_button.onClick.RemoveListener(OnClicked);
        }

        public void SetLabel(string text)
        {
            m_labelText.text = text;
        }

        private void OnClicked()
        {
            Clicked?.Invoke();
        }
    }
}
