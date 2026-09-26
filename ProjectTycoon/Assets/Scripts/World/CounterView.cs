using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08: 계산대. 피벗은 계산대 줄 윗변 가운데. 웜뱃은 자식이고 위치는 Core를 읽는다(손님 동선 설계 v0.2). 줄 자리는 Core BakeryLayout.
    // 2026-09-25: 계산대 위 왼쪽의 영수증 출력기(16프레임). 평소 00(빈 출력기), 계산 중에는 진행에 따라 01~15로 영수증이 올라오고 끝나면 00. BakeryView가 매 프레임 넣는다
    public sealed class CounterView : MonoBehaviour
    {
        [SerializeField] private WombatView m_wombat;
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_timer;
        [SerializeField] private Sprite[] m_timerFrames;

        public WombatView Wombat => m_wombat;
        public SpriteRenderer Body => m_body;

        public void SetProgress(float progress, bool serving)
        {
            m_timer.sprite = m_timerFrames[serving ? 1 + Mathf.RoundToInt(Mathf.Clamp01(progress) * (m_timerFrames.Length - 2)) : 0];
        }

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }
    }
}
