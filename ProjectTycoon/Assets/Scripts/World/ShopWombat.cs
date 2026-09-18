using System;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 08 v0.6: 가게 웜뱃. 계산대에서는 숨쉬기(기다릴 때 정면, 계산 중 뒷모습),
    // 굽기 심부름 때는 받은 시간 안에 오븐까지 걸어갔다 온다(아래로 = 정면 걷기, 위로 = 뒷모습 걷기). 시간은 ShopSim이 센다
    public sealed class ShopWombat : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator m_animator;
        [SerializeField] private Sprite[] m_frontIdle;
        [SerializeField] private Sprite[] m_backIdle;
        [SerializeField] private Sprite[] m_frontWalk;
        [SerializeField] private Sprite[] m_backWalk;
        [Tooltip("숨쉬기 초당 프레임. 4프레임 ÷ 숨 한 번 1.6초")]
        [SerializeField] private float m_idleFrameRate = 2.5f;
        [Tooltip("걷기 초당 프레임")]
        [SerializeField] private float m_walkFrameRate = 8f;

        private Vector3 m_homeLocal;
        private Vector3 m_target;
        private float m_speed;
        private bool m_walking;
        private bool m_serving;
        private Sprite[] m_playing;
        private Action m_arrived;

        // 계산대 자리. 굴이 넓어져 계산대가 내려가도 따라간다
        public Vector3 Home => transform.parent.TransformPoint(m_homeLocal);

        private void Awake()
        {
            m_homeLocal = transform.localPosition;
            PlayIdle();
        }

        public void SetServing(bool serving)
        {
            m_serving = serving;

            if (!m_walking)
            {
                PlayIdle();
            }
        }

        public void WalkTo(Vector3 target, float seconds, Action arrived = null)
        {
            m_target = target;
            m_speed = Vector3.Distance(transform.position, target) / Mathf.Max(seconds, 0.01f);
            m_walking = true;
            m_arrived = arrived;
            Play(target.y < transform.position.y ? m_frontWalk : m_backWalk, m_walkFrameRate);
        }

        public void WalkHome(float seconds)
        {
            WalkTo(Home, seconds, () => transform.localPosition = m_homeLocal);
        }

        private void Update()
        {
            if (!m_walking)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, m_target, m_speed * Time.deltaTime);

            if (transform.position != m_target)
            {
                return;
            }

            m_walking = false;
            PlayIdle();
            Action arrived = m_arrived;
            m_arrived = null;
            arrived?.Invoke();
        }

        private void PlayIdle()
        {
            Play(m_serving ? m_backIdle : m_frontIdle, m_idleFrameRate);
        }

        // 같은 프레임 배열이면 다시 시작하지 않는다(숨쉬기가 끊기지 않게)
        private void Play(Sprite[] frames, float frameRate)
        {
            if (m_playing == frames)
            {
                return;
            }

            m_playing = frames;
            m_animator.Play(frames, frameRate);
        }
    }
}
