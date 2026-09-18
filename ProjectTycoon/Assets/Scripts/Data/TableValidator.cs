using System.Collections.Generic;
using System.Text.RegularExpressions;
using ZooTycoon.Core;

namespace ZooTycoon.Data
{
    // 데이터-테이블-규칙 7장. 봉투(table·version)는 EditMode 테스트가 검사한다(설계 01 D2).
    public static class TableValidator
    {
        private static readonly Regex k_IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        private static readonly Regex k_AnimalIdPattern = new Regex("^a[0-9]{2}$");
        private static readonly Regex k_VisitorIdPattern = new Regex("^v[0-9]{2}$");
        private static readonly Regex k_BreadIdPattern = new Regex("^b[0-9]{2}$");
        private static readonly string[] k_ShopUpgradeIds =
            { ShopSim.k_OvenCount, ShopSim.k_OvenSpeed, ShopSim.k_ShelfCapacity, ShopSim.k_CheckoutSpeed };

        public static IReadOnlyList<string> Validate(GameTables tables)
        {
            List<string> errors = new List<string>();

            ValidateAnimals(tables, errors);
            ValidateZooLevels(tables, errors);
            ValidateVisitors(tables, errors);
            ValidateBreads(tables, errors);
            ValidateShopUpgrades(tables, errors);
            ValidateStrings(tables, errors);
            ValidateConfig(tables, errors);

            return errors;
        }

        private static void ValidateAnimals(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<int> sortOrders = new HashSet<int>();

            foreach (AnimalRecord animal in tables.Animals)
            {
                CheckId("animals", animal.Id, k_IdPattern, ids, errors);
                CheckSortOrder("animals", animal.SortOrder, sortOrders, errors);

                if (animal.Id != null && !k_AnimalIdPattern.IsMatch(animal.Id))
                {
                    errors.Add($"animals '{animal.Id}': 동물 ID는 a + 두 자리 번호여야 한다.");
                }

                if (animal.BaseIncomePerSecond <= 0d)
                {
                    errors.Add($"animals '{animal.Id}': baseIncomePerSecond가 0 이하다.");
                }

                if (string.IsNullOrEmpty(animal.Sprite))
                {
                    errors.Add($"animals '{animal.Id}': sprite 경로가 비어 있다.");
                }

                if (animal.FrameRate <= 0d || animal.Scale <= 0d || animal.MoveSpeed <= 0d)
                {
                    errors.Add($"animals '{animal.Id}': frameRate·scale·moveSpeed는 0보다 커야 한다.");
                }

                if (animal.IdleSecondsMin < 0d || animal.IdleSecondsMax < animal.IdleSecondsMin)
                {
                    errors.Add($"animals '{animal.Id}': 0 ≤ idleSecondsMin ≤ idleSecondsMax여야 한다.");
                }
            }
        }

        private static void ValidateZooLevels(GameTables tables, List<string> errors)
        {
            IReadOnlyList<ZooLevelRecord> levels = tables.ZooLevels;

            for (int i = 0; i < levels.Count; i++)
            {
                ZooLevelRecord level = levels[i];

                if (level.Level != i + 1)
                {
                    errors.Add($"zoo_levels[{i}]: level이 1부터 연속이 아니다(값 {level.Level}).");
                }

                if (i == 0)
                {
                    if (level.RequiredTotalCoins != 0d)
                    {
                        errors.Add("zoo_levels: 첫 레벨의 requiredTotalCoins가 0이 아니다.");
                    }

                    continue;
                }

                if (level.RequiredTotalCoins <= levels[i - 1].RequiredTotalCoins)
                {
                    errors.Add($"zoo_levels[{i}]: requiredTotalCoins가 단조 증가하지 않는다.");
                }
            }
        }

        private static void ValidateVisitors(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<int> sortOrders = new HashSet<int>();

            if (tables.Visitors.Count == 0)
            {
                errors.Add("visitors: 행이 하나도 없다.");
            }

            foreach (VisitorRecord visitor in tables.Visitors)
            {
                CheckId("visitors", visitor.Id, k_IdPattern, ids, errors);
                CheckSortOrder("visitors", visitor.SortOrder, sortOrders, errors);

                if (visitor.Id != null && !k_VisitorIdPattern.IsMatch(visitor.Id))
                {
                    errors.Add($"visitors '{visitor.Id}': 관광객 ID는 v + 두 자리 번호여야 한다.");
                }

                if (string.IsNullOrEmpty(visitor.Sprite))
                {
                    errors.Add($"visitors '{visitor.Id}': sprite 경로가 비어 있다.");
                }

                if (visitor.Weight < 1)
                {
                    errors.Add($"visitors '{visitor.Id}': weight가 1 미만이다.");
                }

                if (visitor.FrameRate <= 0d || visitor.Scale <= 0d || visitor.MoveSpeed <= 0d)
                {
                    errors.Add($"visitors '{visitor.Id}': frameRate·scale·moveSpeed는 0보다 커야 한다.");
                }

                if (visitor.ViewSecondsMin < 0d || visitor.ViewSecondsMax < visitor.ViewSecondsMin)
                {
                    errors.Add($"visitors '{visitor.Id}': 0 ≤ viewSecondsMin ≤ viewSecondsMax여야 한다.");
                }
            }
        }

        // 설계 08 v0.5
        private static void ValidateBreads(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();

            if (tables.Breads.Count == 0)
            {
                errors.Add("breads: 행이 하나도 없다.");
            }

            foreach (BreadRecord bread in tables.Breads)
            {
                CheckId("breads", bread.Id, k_IdPattern, ids, errors);

                if (bread.Id != null && !k_BreadIdPattern.IsMatch(bread.Id))
                {
                    errors.Add($"breads '{bread.Id}': 빵 ID는 b + 두 자리 번호여야 한다.");
                }

                if (string.IsNullOrEmpty(bread.Sprite))
                {
                    errors.Add($"breads '{bread.Id}': sprite 경로가 비어 있다.");
                }

                if (bread.BakeSeconds <= 0d || bread.BatchSize < 1 || bread.Price <= 0d || bread.Weight < 1 || bread.UnlockCost < 0d)
                {
                    errors.Add($"breads '{bread.Id}': bakeSeconds·price는 0보다, batchSize·weight는 1 이상, unlockCost는 0 이상이어야 한다.");
                }
            }
        }

        // 설계 08 v0.5: ShopSim이 부르는 id 4개가 모두 있어야 한다
        private static void ValidateShopUpgrades(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (ShopUpgradeRecord upgrade in tables.ShopUpgrades)
            {
                CheckId("shop_upgrades", upgrade.Id, k_IdPattern, ids, errors);

                if (upgrade.BaseCost <= 0d || upgrade.CostGrowth < 1d || upgrade.MaxLevel < 1 || upgrade.EffectPerLevel <= 0d)
                {
                    errors.Add($"shop_upgrades '{upgrade.Id}': baseCost·effectPerLevel은 0보다, costGrowth·maxLevel은 1 이상이어야 한다.");
                }
            }

            foreach (string id in k_ShopUpgradeIds)
            {
                if (!ids.Contains(id))
                {
                    errors.Add($"shop_upgrades: '{id}' 행이 없다.");
                }
            }
        }

        private static void ValidateStrings(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (StringRecord row in tables.StringRows)
            {
                CheckId("strings", row.Id, k_IdPattern, ids, errors);

                if (string.IsNullOrEmpty(row.Ko))
                {
                    errors.Add($"strings '{row.Id}': ko가 비어 있다.");
                }
            }
        }

        private static void ValidateConfig(GameTables tables, List<string> errors)
        {
            GameConfig config = tables.Config;

            if (config.Start.Coins < 0)
            {
                errors.Add("game_config: start.coins가 0 미만이다.");
            }

            if (config.AnimalCount.IncomeBonusPerAnimal < 0d)
            {
                errors.Add("game_config: animalCount.incomeBonusPerAnimal이 0 미만이다.");
            }

            if (config.Offline.MaxSeconds <= 0)
            {
                errors.Add("game_config: offline.maxSeconds가 0 이하다.");
            }

            if (config.Income.TickSeconds <= 0d)
            {
                errors.Add("game_config: income.tickSeconds가 0 이하다.");
            }

            if (config.Visitors.MaxCount < 1 || config.Visitors.IncomeUnit <= 0d)
            {
                errors.Add("game_config: visitors.maxCount는 1 이상, incomeUnit은 0보다 커야 한다.");
            }

            GameConfig.ShopConfig shop = config.Shop;

            if (shop.ArrivalSeconds <= 0d || shop.CheckoutSeconds <= 0d || shop.EnterSeconds < 0d || shop.RowWalkSeconds < 0d
                || shop.ToQueueSeconds < 0d || shop.PatienceSeconds < 0d || shop.WombatWalkSeconds < 0d || shop.MaxCustomers < 1 || shop.ShelfCapacity < 1 || shop.OvenCount < 1)
            {
                errors.Add("game_config: shop의 arrivalSeconds·checkoutSeconds는 0보다, 다른 시간은 0 이상, maxCustomers·shelfCapacity·ovenCount는 1 이상이어야 한다.");
            }
        }

        private static void CheckId(string table, string id, Regex pattern, HashSet<string> seen, List<string> errors)
        {
            if (id == null || !pattern.IsMatch(id))
            {
                errors.Add($"{table}: id '{id}'가 ID 패턴에 맞지 않는다.");
                return;
            }

            if (!seen.Add(id))
            {
                errors.Add($"{table}: id '{id}'가 중복이다.");
            }
        }

        private static void CheckSortOrder(string table, int sortOrder, HashSet<int> seen, List<string> errors)
        {
            if (!seen.Add(sortOrder))
            {
                errors.Add($"{table}: sortOrder {sortOrder}가 중복이다.");
            }
        }
    }
}
