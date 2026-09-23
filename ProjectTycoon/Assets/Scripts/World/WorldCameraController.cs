using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ZooTycoon.World
{
    // 설계 07-2: 순수 2D 직교 카메라. 초점(화면 XY)을 옮겨 팬한다. 쿼터뷰는 Iso가 좌표로 만든다
    // 설계 08: 드래그 없이 떼면 Tapped(월드 좌표). 가게 화면에서는 경계를 가게 사각형으로 바꾼다(v0.5)
    // 굴 격자 설계 D4: 가게에서는 경계를 넘겨 끌면 넘긴 만큼의 일부만 따라가고 놓으면 되돌아온다(고무줄). 드래그가 시작되면 Dragged(끌어 드러내는 방향)
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        private const float k_Depth = -10f;

        [Tooltip("시작 초점(화면 XY)")]
        [SerializeField] private Vector2 m_focus;
        [Tooltip("누른 뒤 이만큼(화면 픽셀) 안에서 떼면 탭")]
        [SerializeField] private float m_tapMaxMovePixels = 24f;
        [Tooltip("가게: 경계를 넘긴 거리 중 따라가는 비율")]
        [SerializeField] private float m_overscrollFactor = 0.35f;
        [Tooltip("가게: 놓은 뒤 경계로 돌아오는 시간(초)")]
        [SerializeField] private float m_returnSeconds = 0.25f;
        // 설계 07-5: 화면 사각형(뷰포트)이 지점 마름모(맵 + 나무 띠의 화면 경계) 밖으로 나가지 않게 초점을 가둔다
        private Vector2 m_center;
        private float m_halfWidth;
        private float m_halfHeight;

        private Camera m_camera;
        private bool m_dragging;
        private bool m_dragReported;
        private bool m_pressedOnWorld;
        private Vector2 m_pressScreen;
        private Vector3 m_lastPointerWorld;
        private bool m_inShop;
        private Rect m_shopBounds;
        private Vector2 m_overworldFocus;
        // 가게: 넘긴 몫까지 더한 초점, 되돌아오기
        private Vector2 m_rawFocus;
        private Vector2 m_returnFrom;
        private float m_returnT = 1f;

        public event Action<Vector3> Tapped;
        public event Action<Vector2Int> Dragged;

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

        public void EnterShop(Rect shopBounds, Vector2 focus)
        {
            m_overworldFocus = m_focus;
            m_dragging = false;
            ShowShop(shopBounds, focus);
        }

        // 가게 안에서 경계가 바뀌었을 때(굴 확장)도 부른다
        public void ShowShop(Rect shopBounds, Vector2 focus)
        {
            m_inShop = true;
            m_shopBounds = shopBounds;
            m_focus = focus;
            m_returnT = 1f;
            ApplyFocus();
            m_rawFocus = m_focus;
        }

        public void ExitShop()
        {
            m_inShop = false;
            m_dragging = false;
            m_returnT = 1f;
            m_focus = m_overworldFocus;
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
                m_pressedOnWorld = !EventSystem.current.IsPointerOverGameObject();
                m_dragging = m_pressedOnWorld;
                m_dragReported = false;
                m_returnT = 1f;
                m_rawFocus = m_focus;
                m_pressScreen = pointer.position.ReadValue();
                m_lastPointerWorld = PointerWorld(pointer);
                return;
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                bool still = (pointer.position.ReadValue() - m_pressScreen).magnitude <= m_tapMaxMovePixels;

                if (m_pressedOnWorld && still)
                {
                    OnTapped(PointerWorld(pointer));
                }

                if (m_dragging && m_inShop && m_focus != ClampShop(m_focus))
                {
                    m_returnFrom = m_focus;
                    m_returnT = 0f;
                }

                m_dragging = false;
                m_pressedOnWorld = false;
                return;
            }

            if (m_returnT < 1f)
            {
                m_returnT = Mathf.Min(1f, m_returnT + Time.deltaTime / m_returnSeconds);
                float ease = 1f - (1f - m_returnT) * (1f - m_returnT);
                m_focus = Vector2.Lerp(m_returnFrom, ClampShop(m_returnFrom), ease);
                ApplyFocus();
            }

            if (!m_dragging || !pointer.press.isPressed)
            {
                return;
            }

            Vector3 delta = PointerWorld(pointer) - m_lastPointerWorld;
            m_rawFocus -= new Vector2(delta.x, delta.y);
            m_focus = m_rawFocus;
            ApplyFocus();
            m_lastPointerWorld = PointerWorld(pointer);

            Vector2 moved = pointer.position.ReadValue() - m_pressScreen;

            if (!m_dragReported && moved.magnitude > m_tapMaxMovePixels)
            {
                m_dragReported = true;
                // 손가락 반대쪽이 드러난다: 왼쪽으로 끌면 오른쪽(+col), 위로 끌면 아래(+row)
                OnDragged(Mathf.Abs(moved.x) >= Mathf.Abs(moved.y)
                    ? new Vector2Int(-Math.Sign(moved.x), 0)
                    : new Vector2Int(0, Math.Sign(moved.y)));
            }
        }

        // 뷰포트 네 모서리가 마름모 |x−cx|/A + |y−cy|/B ≤ 1 안에 있으려면 초점은 반폭 A − hw − (A/B)·hh 인 작은 마름모 안에 있어야 한다.
        // 밖이면 중심 쪽으로 줄여 넣는다(ponytail: 경계 위 가장 가까운 점 대신 비례 축소)
        private void ApplyFocus()
        {
            if (m_inShop)
            {
                Vector2 clamped = ClampShop(m_focus);
                m_focus = m_dragging ? clamped + (m_focus - clamped) * m_overscrollFactor : m_returnT < 1f ? m_focus : clamped;
            }
            else if (m_halfHeight > 0f)
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

        private Vector2 ClampShop(Vector2 focus)
        {
            float hh = m_camera.orthographicSize;
            float hw = hh * m_camera.aspect;
            return new Vector2(ClampAxis(focus.x, m_shopBounds.xMin + hw, m_shopBounds.xMax - hw),
                ClampAxis(focus.y, m_shopBounds.yMin + hh, m_shopBounds.yMax - hh));
        }

        // 경계가 화면보다 좁으면 가운데
        private static float ClampAxis(float value, float min, float max)
        {
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
        }

        private Vector3 PointerWorld(Pointer pointer)
        {
            Vector3 world = m_camera.ScreenToWorldPoint(pointer.position.ReadValue());
            world.z = 0f;
            return world;
        }

        private void OnTapped(Vector3 world)
        {
            Tapped?.Invoke(world);
        }

        private void OnDragged(Vector2Int direction)
        {
            Dragged?.Invoke(direction);
        }
    }
}
