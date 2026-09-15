using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 07 P6: 우리 옆 정해진 자리에 서 있는 시설 카드. 움직이지 않으므로 Unit이 아니다
    public sealed class Facility : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private SpriteRenderer m_shadowRenderer;

        public void Initialize(Sprite sprite, Quaternion billboard)
        {
            m_spriteRenderer.sprite = sprite;
            m_spriteRenderer.transform.rotation = billboard;
            m_shadowRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
