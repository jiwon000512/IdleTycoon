namespace ZooTycoon.Core
{
    public static class IncomeCalculator
    {
        // 기획서 6.2: 종의 초당 수입 = 기본 × (1 + 보너스 × (마리 수 − 1))
        public static double AnimalIncome(double baseIncomePerSecond, int count, double incomeBonusPerAnimal)
        {
            return baseIncomePerSecond * (1d + incomeBonusPerAnimal * (count - 1));
        }

        // 기획서 6.4: 시설 하나의 배수 = 1 + 단계당 배수 × 단계
        public static double FacilityMultiplier(FacilityRecord facility, int stage)
        {
            return 1d + facility.MultiplierPerStage * stage;
        }

        // 기획서 6.4: 시설 배수 = Π(시설 하나의 배수)
        public static double FacilityMultiplier(ZooState state, GameTables tables)
        {
            double multiplier = 1d;

            for (int i = 0; i < tables.Facilities.Count; i++)
            {
                FacilityRecord facility = tables.Facilities[i];
                multiplier *= FacilityMultiplier(facility, state.GetFacilityStage(facility.Id));
            }

            return multiplier;
        }

        // 기획서 6.2: 총 수입 = Σ(보유 종 수입) × 시설 배수
        public static double TotalIncomePerSecond(ZooState state, GameTables tables)
        {
            double bonusPerAnimal = tables.Config.AnimalCount.IncomeBonusPerAnimal;
            double total = 0d;

            for (int i = 0; i < state.OwnedAnimals.Count; i++)
            {
                OwnedAnimal owned = state.OwnedAnimals[i];
                double baseIncome = tables.GetAnimal(owned.AnimalId).BaseIncomePerSecond;
                total += AnimalIncome(baseIncome, owned.Count, bonusPerAnimal);
            }

            return total * FacilityMultiplier(state, tables);
        }
    }
}
