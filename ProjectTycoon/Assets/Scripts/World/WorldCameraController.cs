using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 09: 직교 카메라가 웜뱃을 부드럽게 따라가고, 가게 경계(판 칸 + 둘레 흙 한 칸) 밖을 비추지 않게 가둔다
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        private const float k_Depth = -10f;

        [Tooltip("웜뱃을 따라잡는 시간(초). 작을수록 딱 붙는다")]
        [SerializeField] private float m_followSeconds = 0.15f;

        private Camera m_camera;
        private Transform m_target;
        private Rect m_bounds;
        private Vector2 m_velocity;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
        }

        public void Follow(Transform target, Rect bounds)
        {
            m_target = target;
            m_bounds = bounds;
            Apply(Clamp(target.position));
        }

        // 굴을 넓히면 경계가 바뀐다
        public void SetBounds(Rect bounds)
        {
            m_bounds = bounds;
        }

        private void LateUpdate()
        {
            if (m_target == null)
            {
                return;
            }

            Vector2 next = Vector2.SmoothDamp(transform.position, Clamp(m_target.position), ref m_velocity, m_followSeconds);
            Apply(next);
        }

        private void Apply(Vector2 focus)
        {
            transform.position = new Vector3(focus.x, focus.y, k_Depth);
        }

        private Vector2 Clamp(Vector2 focus)
        {
            float hh = m_camera.orthographicSize;
            float hw = hh * m_camera.aspect;
            return new Vector2(ClampAxis(focus.x, m_bounds.xMin + hw, m_bounds.xMax - hw),
                ClampAxis(focus.y, m_bounds.yMin + hh, m_bounds.yMax - hh));
        }

        // 경계가 화면보다 좁으면 가운데
        private static float ClampAxis(float value, float min, float max)
        {
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }
    }
}
