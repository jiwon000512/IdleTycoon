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
    }
}
