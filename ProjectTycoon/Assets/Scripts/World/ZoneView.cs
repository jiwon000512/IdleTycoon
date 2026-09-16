using UnityEngine;

namespace ZooTycoon.World
{
    // 존 = 논리 XZ 평면 위의 사각형(설계 07-3: 6×8 타일 고정). 바닥·길은 지점 프리팹의 타일맵이 그리고, 울타리만 Awake에서 세운다
    public sealed class ZoneView : MonoBehaviour
    {
        [Tooltip("존 중심(논리 XZ, 타일 좌표)")]
        [SerializeField] private Vector2 m_center;
        [Tooltip("울타리 경계(로컬 논리 XZ). Rect.x/y = X/Z")]
        [SerializeField] private Rect m_groundArea;
        [Tooltip("동물이 걸을 수 있는 영역(로컬 논리 XZ)")]
        [SerializeField] private Rect m_walkArea;
        [Tooltip("존 앞 길(로컬 논리 XZ). 관광객은 이 안을 X로 가로지른다(설계 06 P5)")]
        [SerializeField] private Rect m_pathArea;
        [Tooltip("울타리 한 칸(기둥 + 오른쪽 위로 가는 가로대, 피벗 = 기둥 밑). Z변은 좌우 반전")]
        [SerializeField] private Sprite m_fenceSprite;
        [Tooltip("기둥만(뒤 모서리용, 피벗 = 기둥 밑)")]
        [SerializeField] private Sprite m_postSprite;

        // 이 존에 스폰된 동물 수. WorldView가 올린다(설계 06 P10)
        public int AnimalCount { get; set; }

        public Vector2 Center => m_center;

        public Rect WorldBounds => Offset(m_groundArea);

        public Rect PathBounds => Offset(m_pathArea);

        private void Awake()
        {
            transform.position = Iso.ToScreen(m_center);
            BuildFence(WorldBounds);
        }

        // 규칙 예외: 연출 난수는 UnityEngine.Random을 쓴다(프로그래밍-규약 5장, 설계 03 P6)
        public Vector2 RandomWalkPoint()
        {
            return m_center + new Vector2(
                Random.Range(m_walkArea.xMin, m_walkArea.xMax),
                Random.Range(m_walkArea.yMin, m_walkArea.yMax));
        }

        private Rect Offset(Rect local)
        {
            return new Rect(local.x + m_center.x, local.y + m_center.y, local.width, local.height);
        }

        // 네 변에 울타리 칸을 정수 좌표(타일 모서리)마다 늘어놓는다. 칸 = 기둥 + 다음 기둥까지의 가로대. X변(앞·뒤)은 그대로, Z변(왼쪽·오른쪽)은 좌우 반전.
        // 가로대가 +X·+Z로 뻗으므로 뒤 모서리(xMax, zMax)만 기둥이 비어 따로 세운다. 정렬점은 기둥 밑(Pivot)
        private void BuildFence(Rect ground)
        {
            for (float x = ground.xMin; x < ground.xMax; x += 1f)
            {
                AddFencePiece(new Vector2(x, ground.yMin), m_fenceSprite, false);
                AddFencePiece(new Vector2(x, ground.yMax), m_fenceSprite, false);
            }

            for (float z = ground.yMin; z < ground.yMax; z += 1f)
            {
                AddFencePiece(new Vector2(ground.xMin, z), m_fenceSprite, true);
                AddFencePiece(new Vector2(ground.xMax, z), m_fenceSprite, true);
            }

            AddFencePiece(new Vector2(ground.xMax, ground.yMax), m_postSprite, false);
        }

        private void AddFencePiece(Vector2 logical, Sprite sprite, bool flip)
        {
            GameObject child = new GameObject("Fence");
            child.transform.SetParent(transform, false);
            child.transform.position = Iso.ToScreen(logical);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipX = flip;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }
}
