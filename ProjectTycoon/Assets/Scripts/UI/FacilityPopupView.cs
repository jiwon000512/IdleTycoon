using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public sealed class FacilityPopupView : UIView
    {
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_closeText;
        [SerializeField] private FacilityRowView m_rowPrefab;
        [SerializeField] private Transform m_rowParent;

        public event Action CloseClicked;

        private void OnEnable()
        {
            m_closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable()
        {
            m_closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        public void SetTitle(string text)
        {
            m_titleText.text = text;
        }

        public void SetCloseLabel(string text)
        {
            m_closeText.text = text;
        }

        public FacilityRowView AddRow()
        {
            return Instantiate(m_rowPrefab, m_rowParent);
        }

        public void Show()
        {
            UIManager.Instance.Open<FacilityPopupView>();
        }

        public void Hide()
        {
            UIManager.Instance.Close(this);
        }

        private void OnCloseClicked()
        {
            CloseClicked?.Invoke();
        }
    }
}
