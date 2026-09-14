using System;

namespace ZooTycoon.Core
{
    public static class VisitorCalculator
    {
        // 기획서 6.4(v0.14): 관광객은 연출이며 수는 초당 수입에서 온다.
        // 수입 0 → 0명, 그 외 min(maxCount, 1 + floor(log2(1 + 수입 / incomeUnit)))
        public static int Count(double incomePerSecond, GameConfig.VisitorsConfig config)
        {
            if (incomePerSecond <= 0d)
            {
                return 0;
            }

            int count = 1 + (int)Math.Floor(Math.Log(1d + incomePerSecond / config.IncomeUnit, 2d));
            return Math.Min(config.MaxCount, count);
        }
    }
}
