using System.Collections;
using UnityEngine;

namespace ZooTycoon.UI
{
    // 팝업 공용 등장·퇴장(.claude/rules/ui.md 5장): 열기 0.15초(아래에서 올라오며 나타남), 닫기 0.10초. 게임 속도와 무관한 시간으로 돈다
    public static class UiFx
    {
        public const float k_RowGap = 0.04f;

        private const float k_OpenSeconds = 0.15f;
        private const float k_CloseSeconds = 0.10f;
        private const float k_Rise = 40f;

        // body가 null이면 나타나기만 한다(레이아웃이 자리를 잡는 줄)
        public static IEnumerator Appear(CanvasGroup group, RectTransform body, Vector2 rest, float delay)
        {
            group.alpha = 0f;

            // 팝업은 그 화면을 맨 앞으로(먼저 연 화면의 오른쪽 버튼 줄이 팝업 · 닫기 위에 그려지지 않게)
            if (body != null)
            {
                group.GetComponentInParent<GameKit.UI.UIView>().transform.SetAsLastSibling();
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            for (float t = 0f; t < k_OpenSeconds; t += Time.unscaledDeltaTime)
            {
                float left = 1f - t / k_OpenSeconds;
                group.alpha = 1f - left * left;

                if (body != null)
                {
                    body.anchoredPosition = rest + Vector2.down * Mathf.Round(k_Rise * left * left);
                }

                yield return null;
            }

            group.alpha = 1f;

            if (body != null)
            {
                body.anchoredPosition = rest;
            }
        }

        // 값이 바뀐 칸이 한 번 부푼다(1 → 1.12 → 1, 0.18초). 눌림 PressScale과 같은 결
        public static IEnumerator Pulse(RectTransform body)
        {
            const float seconds = 0.18f;
            const float peak = 0.12f;

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / seconds * Mathf.PI);
                body.localScale = Vector3.one * (1f + peak * k);
                yield return null;
            }

            body.localScale = Vector3.one;
        }

        public static IEnumerator Vanish(CanvasGroup group, GameObject hide)
        {
            float from = group.alpha;

            for (float t = 0f; t < k_CloseSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = from * (1f - t / k_CloseSeconds);
                yield return null;
            }

            hide.SetActive(false);
            group.alpha = 1f;
        }
    }
}
