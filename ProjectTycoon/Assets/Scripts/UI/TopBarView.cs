using UnityEngine;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public sealed class TopBarView : UIView
    {
        [SerializeField] private TMP_Text m_coinsText;
        [SerializeField] private TMP_Text m_zooLevelText;
        [SerializeField] private TMP_Text m_progressText;

        public void SetCoins(string text)
        {
            m_coinsText.text = text;
        }

        public void SetZooLevel(string text)
        {
            m_zooLevelText.text = text;
        }

        public void SetProgress(string text)
        {
            m_progressText.text = text;
        }
    }
}
