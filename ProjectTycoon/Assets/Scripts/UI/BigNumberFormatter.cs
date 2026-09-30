using System;
using System.Globalization;

namespace ZooTycoon.UI
{
    public static class BigNumberFormatter
    {
        private const double k_Step = 1000d;
        private static readonly string[] k_Units = { "", "K", "M", "B", "T" };

        public static string Format(double value)
        {
            int unitIndex = 0;
            double scaled = value;

            while (scaled >= k_Step && unitIndex < k_Units.Length - 1)
            {
                scaled /= k_Step;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return Math.Floor(scaled).ToString(CultureInfo.InvariantCulture);
            }

            double truncated = Math.Floor(scaled * 10d) / 10d;
            return truncated.ToString("0.#", CultureInfo.InvariantCulture) + k_Units[unitIndex];
        }

        // 남은 초 → 「4:12」(분:초, 초는 올림)
        public static string Clock(double seconds)
        {
            int total = (int)Math.Ceiling(Math.Max(0d, seconds));
            return (total / 60).ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
