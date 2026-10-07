using System;

namespace ZooTycoon.Core
{
    // 씨앗이 정해진 난수(Mall이 곳을 기본값으로 만들 때). 설계 44의 FishingSim에서 옮겨 왔다(설계 52에 그 파일을 지움)
    internal sealed class SeededRandom : IRandom
    {
        private readonly Random m_random;

        public SeededRandom(int seed)
        {
            m_random = new Random(seed);
        }

        public double NextDouble()
        {
            return m_random.NextDouble();
        }
    }
}
