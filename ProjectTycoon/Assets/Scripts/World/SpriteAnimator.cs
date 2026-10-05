using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 05 P4: 프레임 배열을 순환. Animator 에셋 없이 JSON 시트만으로 동작한다.
    // 2026-09-29: 프레임마다 다른 시간(딛기·숨 머묾을 길게), 시작 박자(손님마다 섞기), 겹 그림(눈 깜빡임), 끼워 넣기(딴짓)
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
        // 끼워 넣기: 돌던 프레임이 처음으로 돌아올 때 한 번 보이고, 끝나면 돌던 프레임의 처음으로
        private Sprite[] m_pending;
        private float[] m_pendingSeconds;
        private Sprite[] m_loopFrames;
        private float[] m_loopSeconds;

        // 지금 보이는 프레임 번호(든 빵을 걷기 오르내림에 맞출 때)
        public int Index => m_index;
        // 끼워 넣은 프레임을 보이는 중(Index가 돌던 프레임 번호가 아니다)
        public bool Interjecting => m_loopFrames != null;

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
            m_pending = null;
            m_loopFrames = null;
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

        // 지금 프레임 번호와 같은 번호의 겹 그림을 seconds 동안(눈 깜빡임). 프레임 수가 다르거나 끼워 넣는 중이면 무시
        public void Overlay(Sprite[] frames, float seconds)
        {
            if (frames != null && m_frames != null && frames.Length == m_frames.Length && m_loopFrames == null)
            {
                m_overlay = frames;
                m_overlayLeft = seconds;
                Show();
            }
        }

        // 돌던 프레임이 처음으로 돌아올 때 frames를 한 칸 frameSeconds씩 한 번 보이고 돌던 프레임으로 돌아간다(딴짓).
        // 앞뒤가 모두 돌던 프레임의 처음(기본 자세)이라 이어진다. 다른 프레임을 Play하면 취소.
        // now: 기다리지 않고 바로 첫 칸부터(굴 파기). 끼워 넣던 것은 끊는다
        public void Interject(Sprite[] frames, float frameSeconds, bool now = false)
        {
            if (now && frames != null && m_frames != null)
            {
                if (m_loopFrames == null)
                {
                    m_loopFrames = m_frames;
                    m_loopSeconds = m_seconds;
                }

                m_frames = frames;
                m_seconds = new[] { frameSeconds };
                m_pending = null;
                m_overlayLeft = 0f;
                m_index = 0;
                m_elapsed = 0f;
                Show();
            }
            else if (frames != null && frames.Length > 1 && m_frames != null && m_loopFrames == null)
            {
                m_pending = frames;
                m_pendingSeconds = new[] { frameSeconds };
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

                if (m_index != 0)
                {
                    continue;
                }

                if (m_loopFrames != null)
                {
                    m_frames = m_loopFrames;
                    m_seconds = m_loopSeconds;
                    m_loopFrames = null;
                }
                else if (m_pending != null)
                {
                    m_loopFrames = m_frames;
                    m_loopSeconds = m_seconds;
                    m_frames = m_pending;
                    m_seconds = m_pendingSeconds;
                    m_pending = null;
                    m_overlayLeft = 0f;
                }
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
