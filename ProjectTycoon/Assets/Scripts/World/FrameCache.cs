using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZooTycoon.World
{
    // 설계 05 P5: 시트 경로 → 프레임 배열. 경로마다 한 번만 로드한다. 시트가 없으면 sprite 1장이 프레임 1개
    public sealed class FrameCache
    {
        private readonly Dictionary<string, Sprite[]> m_framesByPath = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

        public Sprite[] Get(string path)
        {
            if (!m_framesByPath.TryGetValue(path, out Sprite[] frames))
            {
                frames = Resources.LoadAll<Sprite>(path);
                // 슬라이스 이름은 "<시트>_<번호>". 문자열 정렬은 _10이 _2 앞에 오므로 번호로 정렬한다
                Array.Sort(frames, (a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
                m_framesByPath[path] = frames;
            }

            return frames;
        }

        private static int FrameIndex(string name)
        {
            int start = name.Length;

            while (start > 0 && char.IsDigit(name[start - 1]))
            {
                start--;
            }

            return start < name.Length ? int.Parse(name.Substring(start)) : 0;
        }
    }
}
