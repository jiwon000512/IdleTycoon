using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 59 ① 특기 셈: 특기가 그 효과면 value, 아니면 1. 후보 특기 뽑기(표 행 중 균등)
    public static class Trait
    {
        public static double Boost(TraitTable trait, string effect)
        {
            return trait != null && trait.Effect == effect ? trait.Value : 1d;
        }

        public static TraitTable Pick(IReadOnlyList<TraitTable> traits, IRandom random)
        {
            return traits[System.Math.Min(traits.Count - 1, (int)(random.NextDouble() * traits.Count))];
        }
    }
}
