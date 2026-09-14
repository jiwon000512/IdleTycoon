using UnityEngine;

namespace ZooTycoon.World
{
    // 우리 = XZ 평면 위의 사각형. 원근은 카메라가 만든다(설계 03 A5)
    public sealed class CageView : MonoBehaviour
    {
        [Tooltip("동물이 걸을 수 있는 영역(로컬 XZ). Rect.x/y = X/Z")]
        [SerializeField] private Rect m_walkArea;
        [SerializeField] private SpriteRenderer m_ground;

        public Rect WorldBounds
        {
            get
            {
                Bounds bounds = m_ground.bounds;
                return new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
            }
        }

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장, 설계 03 P6)
        public Vector3 RandomWalkPoint()
        {
            return transform.position + new Vector3(
                Random.Range(m_walkArea.xMin, m_walkArea.xMax),
                0f,
                Random.Range(m_walkArea.yMin, m_walkArea.yMax));
        }
    }
}
