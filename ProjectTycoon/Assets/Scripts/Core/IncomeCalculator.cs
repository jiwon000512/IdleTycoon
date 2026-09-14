namespace ZooTycoon.Core
{
    public static class IncomeCalculator
    {
        // 기획서 6.2: 초당 수입 = 기본 × (1 + 보너스 × (레벨 − 1))
        public static double AnimalIncome(double baseIncomePerSecond, int level, double incomeBonusPerLevel)
        {
            return baseIncomePerSecond * (1d + incomeBonusPerLevel * (level - 1));
        }

        // 기획서 6.4: 홍보 배수 = 1 + 단계당 배수 × 단계
        public static double PromotionMultiplier(ZooState state, GameTables tables)
        {
            return 1d + tables.Config.Promotion.MultiplierPerStage * state.PromotionStage;
        }

        public static double TotalIncomePerSecond(ZooState state, GameTables tables)
        {
            return TotalIncomePerSecond(state, tables, PromotionMultiplier(state, tables));
        }

        // 기획서 6.2: 총 수입 = Σ(보유 종 수입) × 홍보 배수
        public static double TotalIncomePerSecond(ZooState state, GameTables tables, double promotionMultiplier)
        {
            double bonusPerLevel = tables.Config.AnimalLevel.IncomeBonusPerLevel;
            double total = 0d;

            for (int i = 0; i < state.OwnedAnimals.Count; i++)
            {
                OwnedAnimal owned = state.OwnedAnimals[i];
                double baseIncome = tables.GetAnimal(owned.AnimalId).BaseIncomePerSecond;
                total += AnimalIncome(baseIncome, owned.Level, bonusPerLevel);
            }

            return total * promotionMultiplier;
        }
    }
}
