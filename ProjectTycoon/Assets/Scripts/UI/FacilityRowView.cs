using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ZooTycoon.UI
{
    // 설계 07 P5: 꾸미기 팝업의 시설 한 줄. 팝업이 프리팹에서 만든다
    public sealed class FacilityRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_nameText;
        [SerializeField] private TMP_Text m_stageText;
        [SerializeField] private TMP_Text m_effectText;
        [SerializeField] private Button m_upgradeButton;
        [SerializeField] private TMP_Text m_costText;

        public event Action<FacilityRowView> UpgradeClicked;

        private void OnEnable()
        {
            m_upgradeButton.onClick.AddListener(OnUpgradeClicked);
        }

        private void OnDisable()
        {
            m_upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        }

        public void SetName(string text)
        {
            m_nameText.text = text;
        }

        public void SetStage(string text)
        {
            m_stageText.text = text;
        }

        public void SetEffect(string text)
        {
            m_effectText.text = text;
        }

        public void SetCost(string text)
        {
            m_costText.text = text;
        }

        public void SetInteractable(bool interactable)
        {
            m_upgradeButton.interactable = interactable;
        }

        private void OnUpgradeClicked()
        {
            UpgradeClicked?.Invoke(this);
        }
    }
}
