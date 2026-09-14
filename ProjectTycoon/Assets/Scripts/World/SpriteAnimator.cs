using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 05 P4: 프레임 배열을 일정 속도로 순환. Animator 에셋 없이 JSON 시트만으로 동작한다
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_renderer;

        private Sprite[] m_frames;
        private float m_frameSeconds;
        private float m_elapsed;
        private int m_index;

        public void Play(Sprite[] frames, float frameRate)
        {
            m_frames = frames;
            m_frameSeconds = 1f / frameRate;
            m_elapsed = 0f;
            m_index = 0;
            m_renderer.sprite = frames[0];
        }

        private void Update()
        {
            if (m_frames == null || m_frames.Length < 2)
            {
                return;
            }

            m_elapsed += Time.deltaTime;

            if (m_elapsed < m_frameSeconds)
            {
                return;
            }

            m_elapsed -= m_frameSeconds;
            m_index = (m_index + 1) % m_frames.Length;
            m_renderer.sprite = m_frames[m_index];
        }
    }
}
