using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 08: 가게 화면 HUD. 위에 가게 이름·지상으로, 아래에 업그레이드 버튼(v0.5)
    public sealed class ShopHudView : UIView
    {
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private Button m_backButton;
        [SerializeField] private TMP_Text m_backText;
        [SerializeField] private Button m_upgradeButton;
        [SerializeField] private TMP_Text m_upgradeText;

        public event Action BackClicked;
        public event Action UpgradeClicked;

        private void Awake()
        {
            m_backButton.onClick.AddListener(BackButton_Clicked);
            m_upgradeButton.onClick.AddListener(UpgradeButton_Clicked);
        }

        public void SetTexts(string title, string back, string upgrade)
        {
            m_titleText.text = title;
            m_backText.text = back;
            m_upgradeText.text = upgrade;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void BackButton_Clicked()
        {
            BackClicked?.Invoke();
        }

        private void UpgradeButton_Clicked()
        {
            UpgradeClicked?.Invoke();
        }
    }
}
