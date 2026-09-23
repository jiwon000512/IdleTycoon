using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 08 → 사물 터치 기획: 가게 HUD는 "지상으로"와 가게 이름만(업그레이드 버튼은 사물 시트로 대체)
    public sealed class ShopHudView : UIView
    {
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private Button m_backButton;
        [SerializeField] private TMP_Text m_backText;

        public event Action BackClicked;

        private void Awake()
        {
            m_backButton.onClick.AddListener(BackButton_Clicked);
        }

        public void SetTexts(string title, string back)
        {
            m_titleText.text = title;
            m_backText.text = back;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void BackButton_Clicked()
        {
            BackClicked?.Invoke();
        }
    }
}
