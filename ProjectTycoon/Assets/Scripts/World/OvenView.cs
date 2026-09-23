using TMPro;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 오븐 하나. 굽는 빵 아이콘·굽기 타이머·다 구워 기다리는 개수
    public sealed class OvenView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;
        // 굽기 타이머: 진행 0~1을 미리 그린 프레임 중 하나로(Source~/make_bar.py B, 2026-09-23)
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;
        [SerializeField] private TextMeshPro m_readyText;
        [SerializeField] private Sprite m_baseBody;
        [SerializeField] private Sprite m_upgradedBody;

        public void SetLook(bool upgraded)
        {
            m_body.sprite = upgraded ? m_upgradedBody : m_baseBody;
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        public void ShowBaking(Sprite icon, float progress, int ready)
        {
            m_body.color = Color.white;
            m_icon.enabled = true;
            m_icon.sprite = icon;
            m_timer.enabled = ready == 0;
            SetProgress(progress);
            m_readyText.enabled = ready > 0;
            m_readyText.text = $"x{ready}";
        }

        // 손님 동선 설계 v0.2: 진행 표시만 매 프레임(오븐 사건은 초가 바뀔 때만 온다)
        public void SetProgress(float progress)
        {
            m_timer.sprite = m_timerFrames[Mathf.RoundToInt(Mathf.Clamp01(progress) * (m_timerFrames.Length - 1))];
        }

        public void ShowEmpty()
        {
            m_body.color = Color.white;
            m_icon.enabled = false;
            m_timer.enabled = false;
            m_readyText.enabled = false;
        }
    }
}
