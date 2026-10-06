using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameKit.UI;

namespace ZooTycoon.UI
{
    // 설계 42 곳 이름 띠(2026-10-06 사용자 시안 B): 화면 폭 진갈색 띠가 가운데에서 좌우로 0.25초 펼쳐지고 · 1.05초 · 0.3초 흐려진다.
    // 펼침은 RectMask2D 좌우 안쪽 여백을 줄여 띠와 글자를 함께 드러낸다. 다시 부르면 앞 띠를 지우고 처음부터. 터치를 막지 않는다
    public sealed class LocationView : UIView
    {
        private const float k_OpenSeconds = 0.25f;
        private const float k_HoldSeconds = 1.05f;
        private const float k_OutSeconds = 0.3f;
        private const float k_Pixel = 4f;

        [SerializeField] private CanvasGroup m_band;
        [SerializeField] private RectMask2D m_mask;
        [SerializeField] private TMP_Text m_name;

        private Coroutine m_show;

        private void Awake()
        {
            m_band.gameObject.SetActive(false);
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
            m_band.alpha = 1f;
            m_band.gameObject.SetActive(true);
            float half = m_mask.rectTransform.rect.width * 0.5f;

            for (float t = 0f; t < k_OpenSeconds; t += Time.unscaledDeltaTime)
            {
                float closed = 1f - t / k_OpenSeconds;
                SetInset(half * closed * closed * closed);
                yield return null;
            }

            SetInset(0f);
            yield return new WaitForSecondsRealtime(k_HoldSeconds);

            for (float t = 0f; t < k_OutSeconds; t += Time.unscaledDeltaTime)
            {
                m_band.alpha = 1f - t / k_OutSeconds;
                yield return null;
            }

            m_band.gameObject.SetActive(false);
            m_show = null;
        }

        // 좌우 같은 폭을 가린다. UI 한 칸(4) 단위로 끊어 띠 끝이 칸에 맞는다
        private void SetInset(float inset)
        {
            inset = Mathf.Round(inset / k_Pixel) * k_Pixel;
            m_mask.padding = new Vector4(inset, 0f, inset, 0f);
        }
    }
}
