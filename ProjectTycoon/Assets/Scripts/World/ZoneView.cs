using UnityEngine;

namespace ZooTycoon.World
{
    // 존 = 논리 XZ 평면 위의 6×8 타일 사각형(설계 07-3). 바닥·길은 지점 프리팹의 타일맵, 울타리는 Zone 프리팹에 구워진 자식 스프라이트
    public sealed class ZoneView : MonoBehaviour
    {
        [Tooltip("존 중심(논리 XZ, 타일 좌표)")]
        [SerializeField] private Vector2 m_center;
        [Tooltip("동물이 걸을 수 있는 영역(로컬 논리 XZ). Rect.x/y = X/Z")]
        [SerializeField] private Rect m_walkArea;
        [Tooltip("존 앞 길(로컬 논리 XZ). 관광객은 이 안을 X로 가로지른다(설계 06 P5)")]
        [SerializeField] private Rect m_pathArea;

        // 이 존에 스폰된 동물 수. WorldManager가 올린다(설계 06 P10)
        public int AnimalCount { get; set; }

        public Vector2 Center => m_center;

        public Rect PathBounds => new Rect(m_pathArea.x + m_center.x, m_pathArea.y + m_center.y, m_pathArea.width, m_pathArea.height);

        private void Awake()
        {
            transform.position = Iso.ToScreen(m_center);
        }

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장, 설계 03 P6)
        public Vector2 RandomWalkPoint()
        {
            return m_center + new Vector2(
                Random.Range(m_walkArea.xMin, m_walkArea.xMax),
                Random.Range(m_walkArea.yMin, m_walkArea.yMax));
        }
    }
}
