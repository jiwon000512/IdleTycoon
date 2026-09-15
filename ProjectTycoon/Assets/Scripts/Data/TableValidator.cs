using System.Collections.Generic;
using System.Text.RegularExpressions;
using ZooTycoon.Core;

namespace ZooTycoon.Data
{
    // 데이터-테이블-규칙 7장. 봉투(table·version)는 EditMode 테스트가 검사한다(설계 01 D2).
    // 규칙 예외: 200줄을 넘지만 테이블마다 메서드 하나인 평평한 검사 목록이라 나눌 축이 없다
    public static class TableValidator
    {
        private static readonly Regex k_IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        private static readonly Regex k_AnimalIdPattern = new Regex("^a[0-9]{2}$");
        private static readonly Regex k_VisitorIdPattern = new Regex("^v[0-9]{2}$");

        public static IReadOnlyList<string> Validate(GameTables tables)
        {
            List<string> errors = new List<string>();

            ValidateGrades(tables, errors);
            ValidateAnimals(tables, errors);
            ValidateZooLevels(tables, errors);
            ValidateVisitors(tables, errors);
            ValidateStrings(tables, errors);
            ValidateConfig(tables, errors);

            return errors;
        }

        private static void ValidateGrades(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<int> sortOrders = new HashSet<int>();
            int weightSum = 0;

            foreach (GradeRecord grade in tables.Grades)
            {
                CheckId("grades", grade.Id, k_IdPattern, ids, errors);
                CheckSortOrder("grades", grade.SortOrder, sortOrders, errors);

                if (grade.GachaWeight < 0)
                {
                    errors.Add($"grades '{grade.Id}': gachaWeight가 0 미만이다.");
                }

                weightSum += grade.GachaWeight;
            }

            if (weightSum <= 0)
            {
                errors.Add("grades: gachaWeight 합이 0 이하다.");
            }
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

                if (!tables.HasGrade(animal.Grade))
                {
                    errors.Add($"animals '{animal.Id}': grade '{animal.Grade}'가 grades에 없다.");
                }

                if (animal.BaseIncomePerSecond <= 0d)
                {
                    errors.Add($"animals '{animal.Id}': baseIncomePerSecond가 0 이하다.");
                }

                if (animal.GachaWeight < 1)
                {
                    errors.Add($"animals '{animal.Id}': gachaWeight가 1 미만이다.");
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
            int promotionUnlocks = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                ZooLevelRecord level = levels[i];

                if (level.Level != i + 1)
                {
                    errors.Add($"zoo_levels[{i}]: level이 1부터 연속이 아니다(값 {level.Level}).");
                }

                if (level.UnlocksPromotion)
                {
                    promotionUnlocks++;
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

                if (level.CageCount <= levels[i - 1].CageCount)
                {
                    errors.Add($"zoo_levels[{i}]: cageCount가 단조 증가하지 않는다.");
                }
            }

            if (promotionUnlocks != 1)
            {
                errors.Add($"zoo_levels: unlocksPromotion이 true인 레벨이 {promotionUnlocks}개다(1개여야 함).");
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

            if (config.Gacha.BaseCost <= 0 || config.Promotion.BaseCost <= 0)
            {
                errors.Add("game_config: baseCost가 0 이하다.");
            }

            if (config.Gacha.CostGrowth <= 1d || config.Promotion.CostGrowth <= 1d)
            {
                errors.Add("game_config: costGrowth가 1 이하다.");
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

            if (config.Start.Coins < config.Gacha.BaseCost)
            {
                errors.Add("game_config: start.coins가 첫 뽑기 비용보다 적다.");
            }

            if (config.Visitors.MaxCount < 1 || config.Visitors.IncomeUnit <= 0d)
            {
                errors.Add("game_config: visitors.maxCount는 1 이상, incomeUnit은 0보다 커야 한다.");
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
