using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameKit.UI;

namespace ZooTycoon.UI
{
    public sealed class GachaResultView : UIView
    {
        [SerializeField] private Image m_frame;
        [SerializeField] private Image m_animalImage;
        [SerializeField] private TMP_Text m_titleText;
        [SerializeField] private TMP_Text m_detailText;
        [Tooltip("카드가 떠 있는 시간(초). 지나면 스스로 닫힌다")]
        [SerializeField] private float m_displaySeconds = 1f;

        private Coroutine m_closeRoutine;

        public void Show(Sprite sprite, Color frameColor, string title, string detail)
        {
            m_frame.color = frameColor;
            m_animalImage.sprite = sprite;
            m_titleText.text = title;
            m_detailText.text = detail;

            StopCloseRoutine();
            m_closeRoutine = StartCoroutine(CloseAfterDelay());
        }

        private void OnDisable()
        {
            StopCloseRoutine();
        }

        private IEnumerator CloseAfterDelay()
        {
            yield return new WaitForSeconds(m_displaySeconds);
            m_closeRoutine = null;
            UIManager.Instance.Close(this);
        }

        private void StopCloseRoutine()
        {
            if (m_closeRoutine != null)
            {
                StopCoroutine(m_closeRoutine);
                m_closeRoutine = null;
            }
        }
    }
}
