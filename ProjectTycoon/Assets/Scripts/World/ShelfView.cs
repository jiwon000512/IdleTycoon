using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 진열대 하나. 빵 아이콘만(재고 수는 옆에 선 ShelfSignView). 굴 격자 설계 v0.5: 잠긴 칸은 없다(빈 자리는 MarkerView)
    public sealed class ShelfView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;

        // 빵 그림 자리(월드). 설계 10: 웜뱃이 채울 때 빵이 날아가는 곳
        public Vector3 IconPosition => m_icon.transform.position;

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        public void Show(Sprite icon, int stock)
        {
            m_icon.enabled = stock > 0;
            m_icon.sprite = icon;
        }
    }
}
