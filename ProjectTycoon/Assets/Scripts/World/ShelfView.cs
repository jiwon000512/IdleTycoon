using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.5: 진열대 하나. 빵 아이콘만(재고 수는 옆에 선 ShelfSignView). 굴 격자 설계 v0.5: 잠긴 칸은 없다(빈 자리는 MarkerView)
    public sealed class ShelfView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_body;
        [SerializeField] private SpriteRenderer m_icon;
        [Tooltip("빵 밑이 앉는 높이(유닛, 진열대 발끝 기준) = 빵 상자 바닥(천 위, Source~/make_shop_props.py)")]
        [SerializeField] private float m_seat = 0.75f;

        // 빵 그림 자리(월드). 설계 10: 웜뱃이 채울 때 빵이 날아가는 곳
        public Vector3 IconPosition => m_icon.transform.position;
        // 편집 모드 외곽선을 붙일 몸체
        public SpriteRenderer Body => m_body;

        public void Bounce()
        {
            StartCoroutine(Fx.Bounce(m_body.transform));
        }

        public void Show(Sprite icon, int stock)
        {
            m_icon.enabled = stock > 0;
            m_icon.sprite = icon;

            // 빵마다 키가 달라 밑을 아랫판에 앉힌다(가운데 맞춤이면 작은 빵이 칸 안에 뜬다)
            if (icon != null)
            {
                m_icon.transform.localPosition = new Vector3(0f, m_seat - icon.bounds.min.y, 0f);
            }
        }
    }
}
