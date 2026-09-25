using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08: 계산대. 피벗은 계산대 줄 윗변 가운데. 웜뱃은 자식이고 위치는 Core를 읽는다(손님 동선 설계 v0.2). 줄 자리는 Core BakeryLayout
    public sealed class CounterView : MonoBehaviour
    {
        [SerializeField] private WombatView m_wombat;
        [SerializeField] private SpriteRenderer m_body;

        public WombatView Wombat => m_wombat;

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }
    }
}
