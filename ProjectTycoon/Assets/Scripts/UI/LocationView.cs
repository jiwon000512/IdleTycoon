using System.Collections;
using TMPro;
using UnityEngine;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 42 곳 이름 팻말: 아트방 나무 팻말(loc_board, 반투명)에 곳 이름을 0.2초 나타남 · 0.7초 · 0.3초 사라짐.
    // 다시 부르면 앞 팻말을 지우고 처음부터. 터치를 막지 않는다
    public sealed class LocationView : UIView
    {
        private const float k_InSeconds = 0.2f;
        private const float k_HoldSeconds = 0.7f;
        private const float k_OutSeconds = 0.3f;

        [SerializeField] private CanvasGroup m_board;
        [SerializeField] private TMP_Text m_name;

        private Coroutine m_show;

        private void Awake()
        {
            m_board.alpha = 0f;
            m_board.gameObject.SetActive(false);
        }

        public void Show(string name)
        {
            m_name.text = name;

            if (m_show != null)
            {
                StopCoroutine(m_show);
            }

            m_show = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            m_board.gameObject.SetActive(true);
            yield return Fade(0f, 1f, k_InSeconds);
            yield return new WaitForSecondsRealtime(k_HoldSeconds);
            yield return Fade(1f, 0f, k_OutSeconds);
            m_board.gameObject.SetActive(false);
            m_show = null;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                m_board.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }

            m_board.alpha = to;
        }
    }
}
