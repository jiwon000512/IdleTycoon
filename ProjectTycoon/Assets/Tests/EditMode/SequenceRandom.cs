using System;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 프로그래밍-규약 11장: 결정적 난수 대역. 받은 값을 차례로 돌려준다
    internal sealed class SequenceRandom : IRandom
    {
        private readonly double[] m_values;
        private int m_index;

        public SequenceRandom(params double[] values)
        {
            m_values = values;
        }

        public double NextDouble()
        {
            if (m_index >= m_values.Length)
            {
                throw new InvalidOperationException($"준비한 난수 {m_values.Length}개를 다 썼다.");
            }

            return m_values[m_index++];
        }
    }
}
