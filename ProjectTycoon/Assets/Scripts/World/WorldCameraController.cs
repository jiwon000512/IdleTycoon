using System;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 09: 직교 카메라가 웜뱃을 부드럽게 따라가고, 가게 경계(판 칸 + 둘레 흙 한 칸) 밖을 비추지 않게 가둔다.
    // 설계 18: 편집 모드에서는 따라가지 않고 빈 곳 드래그로 팬(같은 경계 안)
    // 비추기(Spot): 잠깐 다른 곳을 당겨서 보여 주고 웜뱃에게 돌아온다(점원이 굴에서 나올 때). 조작하면 바로 돌아온다
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraController : MonoBehaviour
    {
        private const float k_Depth = -10f;

        [Tooltip("웜뱃을 따라잡는 시간(초). 작을수록 딱 붙는다")]
        [SerializeField] private float m_followSeconds = 0.15f;
        [Tooltip("비추기: 당겨 보는 배율(정수 배라야 픽셀이 고르다)과 당기고 푸는 시간(초)")]
        [SerializeField] private float m_spotZoom = 2f;
        [SerializeField] private float m_spotSeconds = 0.25f;

        private Camera m_camera;
        private Transform m_target;
        private Rect m_bounds;
        private Vector2 m_velocity;
        private bool m_following = true;
        private float m_size;
        private float m_sizeVelocity;
        private Func<Vector2> m_spot;
        private Func<bool> m_spotCancel;
        private float m_spotLeft;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            m_size = m_camera.orthographicSize;
        }

        // seconds 동안 at을 당겨서 따라간다. cancel이 참이 되면 그만둔다. 편집 모드에서는 하지 않는다
        public void Spot(Func<Vector2> at, float seconds, Func<bool> cancel)
        {
            if (m_following)
            {
                m_spot = at;
                m_spotCancel = cancel;
                m_spotLeft = seconds;
            }
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

        // 편집 모드: 멈춰 서서 팬만 받는다. 다시 따라갈 때는 제자리에서 부드럽게 붙는다
        public void SetFollowing(bool following)
        {
            m_following = following;
            m_velocity = Vector2.zero;
            m_spotLeft = 0f;
        }

        public void Pan(Vector2 delta)
        {
            Apply(Clamp((Vector2)transform.position + delta));
        }

        private void LateUpdate()
        {
            m_spotLeft = m_spotLeft > 0f && !m_spotCancel() ? m_spotLeft - Time.unscaledDeltaTime : 0f;
            bool spot = m_spotLeft > 0f;
            m_camera.orthographicSize = Mathf.SmoothDamp(m_camera.orthographicSize, spot ? m_size / m_spotZoom : m_size, ref m_sizeVelocity, m_spotSeconds, Mathf.Infinity, Time.unscaledDeltaTime);

            if (m_target == null || !m_following)
            {
                return;
            }

            Vector2 focus = spot ? m_spot() : (Vector2)m_target.position;
            Vector2 next = Vector2.SmoothDamp(transform.position, Clamp(focus), ref m_velocity, spot ? m_spotSeconds : m_followSeconds, Mathf.Infinity, spot ? Time.unscaledDeltaTime : Time.deltaTime);
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
