using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 05 P4: 프레임 배열을 순환. Animator 에셋 없이 JSON 시트만으로 동작한다.
    // 2026-09-29: 프레임마다 다른 시간(딛기·숨 머묾을 길게), 시작 박자(손님마다 섞기), 겹 그림(눈 깜빡임)
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_renderer;

        private Sprite[] m_frames;
        private float[] m_seconds;
        private float m_elapsed;
        private int m_index;
        // 겹 그림: 남은 시간 동안 지금 프레임 번호의 이 그림을 대신 보인다
        private Sprite[] m_overlay;
        private float m_overlayLeft;

        // 지금 보이는 프레임 번호(든 빵을 걷기 오르내림에 맞출 때)
        public int Index => m_index;

        // 실행 중에 붙인 애니메이터(광장 분수)는 렌더러를 같이 넘긴다
        public void Play(SpriteRenderer renderer, Sprite[] frames, float frameRate)
        {
            m_renderer = renderer;
            Play(frames, frameRate);
        }

        // 고른 속도. phase(0~1)는 한 바퀴 중 어디서 시작할지
        public void Play(Sprite[] frames, float frameRate, float phase = 0f)
        {
            float[] seconds = new float[frames.Length];

            for (int i = 0; i < seconds.Length; i++)
            {
                seconds[i] = 1f / frameRate;
            }

            Play(frames, seconds, phase);
        }

        // 프레임마다 시간(초). 프레임보다 짧으면 마지막 값을 이어 쓴다
        public void Play(Sprite[] frames, float[] seconds, float phase = 0f)
        {
            m_frames = frames;
            m_seconds = seconds;
            m_overlayLeft = 0f;
            m_index = 0;
            m_elapsed = 0f;
            float cycle = 0f;

            for (int i = 0; i < frames.Length; i++)
            {
                cycle += Seconds(i);
            }

            Advance(phase * cycle);
            Show();
        }

        // 지금 프레임 번호와 같은 번호의 겹 그림을 seconds 동안(눈 깜빡임). 프레임 수가 다르면 무시
        public void Overlay(Sprite[] frames, float seconds)
        {
            if (frames != null && m_frames != null && frames.Length == m_frames.Length)
            {
                m_overlay = frames;
                m_overlayLeft = seconds;
                Show();
            }
        }

        private float Seconds(int index)
        {
            return m_seconds[Mathf.Min(index, m_seconds.Length - 1)];
        }

        private void Advance(float dt)
        {
            m_elapsed += dt;

            while (m_frames.Length > 1 && m_elapsed >= Seconds(m_index))
            {
                m_elapsed -= Seconds(m_index);
                m_index = (m_index + 1) % m_frames.Length;
            }
        }

        private void Show()
        {
            m_renderer.sprite = m_overlayLeft > 0f ? m_overlay[m_index] : m_frames[m_index];
        }

        private void Update()
        {
            if (m_frames == null)
            {
                return;
            }

            // 바뀐 때만 그린다(다른 코드가 같은 렌더러에 그림을 넣는 경우를 덮지 않게)
            int index = m_index;
            bool overlay = m_overlayLeft > 0f;
            m_overlayLeft -= Time.deltaTime;
            Advance(Time.deltaTime);

            if (index != m_index || overlay != m_overlayLeft > 0f)
            {
                Show();
            }
        }
    }
}
