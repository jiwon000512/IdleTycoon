using UnityEngine;

namespace ZooTycoon.World
{
    // 우리 = XZ 평면 위의 사각형. 원근은 카메라가 만든다(설계 03 A5)
    public sealed class CageView : MonoBehaviour
    {
        [Tooltip("동물이 걸을 수 있는 영역(로컬 XZ). Rect.x/y = X/Z")]
        [SerializeField] private Rect m_walkArea;
        [SerializeField] private SpriteRenderer m_ground;
        [Tooltip("우리 앞 길 타일. 관광객은 이 타일의 경계 안을 걷는다(설계 06 P5)")]
        [SerializeField] private SpriteRenderer m_path;

        public Rect WorldBounds => ToXZ(m_ground.bounds);

        public Rect PathBounds => ToXZ(m_path.bounds);

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장, 설계 03 P6)
        public Vector3 RandomWalkPoint()
        {
            return transform.position + new Vector3(
                Random.Range(m_walkArea.xMin, m_walkArea.xMax),
                0f,
                Random.Range(m_walkArea.yMin, m_walkArea.yMax));
        }

        private static Rect ToXZ(Bounds bounds)
        {
            return new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
        }
    }
}
