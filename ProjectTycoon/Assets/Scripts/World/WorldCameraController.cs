using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZooTycoon.World
{
    // 설계 07-2: 순수 2D 직교 카메라. 초점(화면 XY)을 옮겨 팬한다. 쿼터뷰는 Iso가 좌표로 만든다
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        private const float k_Depth = -10f;

        [Tooltip("시작 초점(화면 XY)")]
        [SerializeField] private Vector2 m_focus;
        // 설계 07-5: 화면 사각형(뷰포트)이 지점 마름모(맵 + 나무 띠의 화면 경계) 밖으로 나가지 않게 초점을 가둔다
        private Vector2 m_center;
        private float m_halfWidth;
        private float m_halfHeight;

        private Camera m_camera;
        private bool m_dragging;
        private Vector3 m_lastPointerWorld;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            ApplyFocus();
        }

        // 지점의 화면 경계 사각형에 내접하는 마름모를 한계로 삼는다
        public void SetBounds(Rect screenBounds)
        {
            m_center = screenBounds.center;
            m_halfWidth = screenBounds.width * 0.5f;
            m_halfHeight = screenBounds.height * 0.5f;
            ApplyFocus();
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null)
            {
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                m_dragging = !EventSystem.current.IsPointerOverGameObject();
                m_lastPointerWorld = PointerWorld(pointer);
                return;
            }

            if (!pointer.press.isPressed)
            {
                m_dragging = false;
                return;
            }

            if (!m_dragging)
            {
                return;
            }

            Vector3 delta = PointerWorld(pointer) - m_lastPointerWorld;
            m_focus -= new Vector2(delta.x, delta.y);
            ApplyFocus();
            m_lastPointerWorld = PointerWorld(pointer);
        }

        // 뷰포트 네 모서리가 마름모 |x−cx|/A + |y−cy|/B ≤ 1 안에 있으려면 초점은 반폭 A − hw − (A/B)·hh 인 작은 마름모 안에 있어야 한다.
        // 밖이면 중심 쪽으로 줄여 넣는다(ponytail: 경계 위 가장 가까운 점 대신 비례 축소)
        private void ApplyFocus()
        {
            if (m_halfHeight > 0f)
            {
                float hh = m_camera.orthographicSize;
                float hw = hh * m_camera.aspect;
                float k = m_halfWidth / m_halfHeight;
                float limit = Mathf.Max(0f, m_halfWidth - hw - k * hh);
                Vector2 d = m_focus - m_center;
                float dist = Mathf.Abs(d.x) + k * Mathf.Abs(d.y);

                if (dist > limit)
                {
                    m_focus = m_center + d * (limit / dist);
                }
            }

            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(m_focus.x, m_focus.y, k_Depth);
        }

        private Vector3 PointerWorld(Pointer pointer)
        {
            Vector3 world = m_camera.ScreenToWorldPoint(pointer.position.ReadValue());
            world.z = 0f;
            return world;
        }
    }
}
