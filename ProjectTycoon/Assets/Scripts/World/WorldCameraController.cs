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
        [Tooltip("초점이 머물 수 있는 범위(화면 XY). WorldView가 우리 배치로부터 계산해 넣는다")]
        [SerializeField] private Rect m_bounds;

        private Camera m_camera;
        private bool m_dragging;
        private Vector3 m_lastPointerWorld;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            ApplyFocus();
        }

        public void SetBounds(Rect bounds)
        {
            m_bounds = bounds;
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

        private void ApplyFocus()
        {
            m_focus.x = Mathf.Clamp(m_focus.x, m_bounds.xMin, m_bounds.xMax);
            m_focus.y = Mathf.Clamp(m_focus.y, m_bounds.yMin, m_bounds.yMax);
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
