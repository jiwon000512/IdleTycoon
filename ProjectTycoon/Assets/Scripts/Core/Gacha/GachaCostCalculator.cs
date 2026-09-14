using System;

namespace ZooTycoon.Core
{
    public static class GachaCostCalculator
    {
        // 기획서 6.3: 비용 = base × growth^n (n = 누적 뽑기 횟수). 표의 값이 반올림 정수라 표시값과 차감값을 맞춘다
        public static double Cost(GameConfig.GachaConfig config, int pullCount)
        {
            return Math.Round(config.BaseCost * Math.Pow(config.CostGrowth, pullCount), MidpointRounding.AwayFromZero);
        }
    }
}
