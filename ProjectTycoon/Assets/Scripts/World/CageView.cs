using UnityEngine;

namespace ZooTycoon.World
{
    // 우리 = 논리 XZ 평면 위의 사각형. 바닥·앞길·울타리는 Awake에서 논리 사각형으로부터 세운다(설계 07-2)
    public sealed class CageView : MonoBehaviour
    {
        private const int k_FloorOrder = -1000;
        private const int k_PathOrder = -1500;

        [Tooltip("우리 중심(논리 XZ)")]
        [SerializeField] private Vector2 m_center;
        [Tooltip("바닥·울타리 경계(로컬 논리 XZ). Rect.x/y = X/Z")]
        [SerializeField] private Rect m_groundArea;
        [Tooltip("동물이 걸을 수 있는 영역(로컬 논리 XZ)")]
        [SerializeField] private Rect m_walkArea;
        [Tooltip("우리 앞 길(로컬 논리 XZ). 관광객은 이 안을 X로 가로지른다(설계 06 P5)")]
        [SerializeField] private Rect m_pathArea;
        [SerializeField] private Sprite m_floorSprite;
        [SerializeField] private Sprite m_pathSprite;
        [Tooltip("울타리 한 칸(기둥 + 오른쪽 위로 가는 가로대, 피벗 = 기둥 밑). Z변은 좌우 반전")]
        [SerializeField] private Sprite m_fenceSprite;
        [Tooltip("울타리 칸 간격(유닛)")]
        [SerializeField] private float m_fenceStep = 0.96f;

        // 이 우리에 스폰된 동물 수. WorldView가 올린다(설계 06 P10)
        public int AnimalCount { get; set; }

        public Vector2 Center => m_center;

        public Rect WorldBounds => Offset(m_groundArea);

        public Rect PathBounds => Offset(m_pathArea);

        private void Awake()
        {
            transform.position = Iso.ToScreen(m_center);
            Rect ground = WorldBounds;
            AddFlat("Floor", m_floorSprite, ground.center, k_FloorOrder);
            AddFlat("Path", m_pathSprite, PathBounds.center, k_PathOrder);
            BuildFence(ground);
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

        private void AddFlat(string name, Sprite sprite, Vector2 logicalCenter, int order)
        {
            SpriteRenderer renderer = NewRenderer(name, logicalCenter, sprite);
            renderer.sortingOrder = order;
        }

        // 네 변에 울타리 칸을 늘어놓는다. X변(앞·뒤)은 그대로, Z변(왼쪽·오른쪽)은 좌우 반전. 정렬점은 기둥 밑(Pivot)
        private void BuildFence(Rect ground)
        {
            for (float x = ground.xMin + m_fenceStep * 0.5f; x < ground.xMax; x += m_fenceStep)
            {
                AddFencePiece(new Vector2(x, ground.yMin), false);
                AddFencePiece(new Vector2(x, ground.yMax), false);
            }

            for (float z = ground.yMin + m_fenceStep * 0.5f; z < ground.yMax; z += m_fenceStep)
            {
                AddFencePiece(new Vector2(ground.xMin, z), true);
                AddFencePiece(new Vector2(ground.xMax, z), true);
            }
        }

        private void AddFencePiece(Vector2 logical, bool flip)
        {
            SpriteRenderer renderer = NewRenderer("Fence", logical, m_fenceSprite);
            renderer.flipX = flip;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        private SpriteRenderer NewRenderer(string name, Vector2 logical, Sprite sprite)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.position = Iso.ToScreen(logical);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }
    }
}
