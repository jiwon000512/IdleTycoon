using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public sealed class GachaButtonView : UIView
    {
        [SerializeField] private Button m_button;
        [SerializeField] private TMP_Text m_labelText;
        [SerializeField] private TMP_Text m_costText;
        [SerializeField] private TMP_Text m_probabilityText;

        public event Action Clicked;

        private void OnEnable()
        {
            m_button.onClick.AddListener(Button_Clicked);
        }

        private void OnDisable()
        {
            m_button.onClick.RemoveListener(Button_Clicked);
        }

        public void SetLabel(string text)
        {
            m_labelText.text = text;
        }

        public void SetCost(string text)
        {
            m_costText.text = text;
        }

        public void SetProbabilities(string text)
        {
            m_probabilityText.text = text;
        }

        public void SetInteractable(bool interactable)
        {
            m_button.interactable = interactable;
        }

        private void Button_Clicked()
        {
            OnClicked();
        }

        private void OnClicked()
        {
            Clicked?.Invoke();
        }
    }
}
