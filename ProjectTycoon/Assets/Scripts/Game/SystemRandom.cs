using System;
using ZooTycoon.Core;

namespace ZooTycoon.Game
{
    public sealed class SystemRandom : IRandom
    {
        private readonly Random m_random = new Random();

        public double NextDouble()
        {
            return m_random.NextDouble();
        }
    }
}
