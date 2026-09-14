using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZooTycoon.World
{
    // 설계 03 A5: 원근 카메라가 XZ 평면을 비스듬히 내려다본다. 초점(평면 위 한 점)을 옮겨 팬한다
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        [Tooltip("내려다보는 각도(도). 0 = 수평, 90 = 수직")]
        [SerializeField] private float m_tiltDegrees = 55f;
        [Tooltip("초점에서 카메라까지의 거리(유닛). 멀수록 넓게 보인다")]
        [SerializeField] private float m_distance = 15f;
        [Tooltip("시작 초점(XZ)")]
        [SerializeField] private Vector2 m_focus;
        [Tooltip("초점이 머물 수 있는 범위(XZ). WorldView가 우리 배치로부터 계산해 넣는다")]
        [SerializeField] private Rect m_bounds;

        private static readonly Plane s_ground = new Plane(Vector3.up, Vector3.zero);

        private Camera m_camera;
        private bool m_dragging;
        private Vector3 m_lastPointerGround;

        public Quaternion BillboardRotation => Quaternion.Euler(m_tiltDegrees, 0f, 0f);

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
                m_dragging = !EventSystem.current.IsPointerOverGameObject() && TryPointerOnGround(pointer, out m_lastPointerGround);
                return;
            }

            if (!pointer.press.isPressed)
            {
                m_dragging = false;
                return;
            }

            if (!m_dragging || !TryPointerOnGround(pointer, out Vector3 pointerGround))
            {
                return;
            }

            Vector3 delta = pointerGround - m_lastPointerGround;
            m_focus -= new Vector2(delta.x, delta.z);
            ApplyFocus();
            TryPointerOnGround(pointer, out m_lastPointerGround);
        }

        private void ApplyFocus()
        {
            m_focus.x = Mathf.Clamp(m_focus.x, m_bounds.xMin, m_bounds.xMax);
            m_focus.y = Mathf.Clamp(m_focus.y, m_bounds.yMin, m_bounds.yMax);
            transform.rotation = BillboardRotation;
            transform.position = new Vector3(m_focus.x, 0f, m_focus.y) - transform.forward * m_distance;
        }

        private bool TryPointerOnGround(Pointer pointer, out Vector3 point)
        {
            Ray ray = m_camera.ScreenPointToRay(pointer.position.ReadValue());

            if (s_ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }

            point = Vector3.zero;
            return false;
        }
    }
}
