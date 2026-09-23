using System.Collections.Generic;
using System.Text.RegularExpressions;
using ZooTycoon.Core;

namespace ZooTycoon.Data
{
    // 데이터-테이블-규칙 7장. 봉투(table·version)는 EditMode 테스트가 검사한다(설계 01 D2).
    public static class TableValidator
    {
        private static readonly Regex k_IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        private static readonly Regex k_VisitorIdPattern = new Regex("^v[0-9]{2}$");
        private static readonly Regex k_BreadIdPattern = new Regex("^b[0-9]{2}$");
        private static readonly string[] k_ShopUpgradeIds =
            { ShopSim.k_OvenCount, ShopSim.k_OvenSpeed, ShopSim.k_ShelfCapacity, ShopSim.k_CheckoutSpeed };
        private static readonly string[] k_ActionIds =
            { ShopSim.k_ActionTakeOut, ShopSim.k_ActionFill, ShopSim.k_ActionServe, ShopSim.k_ActionOpen, ShopSim.k_ActionDig };
        private static readonly string[] k_SoundIds = { SoundRecord.k_Pay, SoundRecord.k_OvenDone, SoundRecord.k_GiveUp };
        // 시트를 여는 행동은 버튼으로만
        private static readonly string[] k_ManualOnlyActionIds = { ShopSim.k_ActionOpen, ShopSim.k_ActionDig };

        public static IReadOnlyList<string> Validate(GameTables tables)
        {
            List<string> errors = new List<string>();

            ValidateVisitors(tables, errors);
            ValidateBreads(tables, errors);
            ValidateShopUpgrades(tables, errors);
            ValidateActions(tables, errors);
            ValidateInteractables(tables, errors);
            ValidateSounds(tables, errors);
            ValidateStrings(tables, errors);
            ValidateConfig(tables, errors);

            return errors;
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

                if (visitor.CarryAt != VisitorRecord.k_CarryHand && visitor.CarryAt != VisitorRecord.k_CarryHead)
                {
                    errors.Add($"visitors '{visitor.Id}': carryAt은 hand·head 중 하나여야 한다.");
                }

                if (visitor.Weight < 1)
                {
                    errors.Add($"visitors '{visitor.Id}': weight가 1 미만이다.");
                }

                if (visitor.IdleFrameRate <= 0d || visitor.MoveFrameRate <= 0d || visitor.Scale <= 0d || visitor.MoveSpeed <= 0d)
                {
                    errors.Add($"visitors '{visitor.Id}': idleFrameRate·moveFrameRate·scale·moveSpeed는 0보다 커야 한다.");
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

                if (upgrade.Target != "shelf" && upgrade.Target != "oven" && upgrade.Target != "counter" && upgrade.Target != "slot")
                {
                    errors.Add($"shop_upgrades '{upgrade.Id}': target은 shelf·oven·counter·slot 중 하나여야 한다.");
                }

                if (string.IsNullOrEmpty(upgrade.EffectFormat))
                {
                    errors.Add($"shop_upgrades '{upgrade.Id}': effectFormat이 비어 있다.");
                }

                if (upgrade.LookLevel < 0 || upgrade.LookLevel > upgrade.MaxLevel)
                {
                    errors.Add($"shop_upgrades '{upgrade.Id}': lookLevel은 0 이상 maxLevel 이하여야 한다.");
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

        // 설계 09 v0.4: 코드가 아는 행동 5개가 모두 있고, mode가 manual·auto, manual이면 아이콘, 시트 행동은 manual만
        private static void ValidateActions(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (ActionRecord action in tables.Actions)
            {
                CheckId("actions", action.Id, k_IdPattern, ids, errors);

                if (action.Mode != ActionRecord.k_Manual && action.Mode != ActionRecord.k_Auto)
                {
                    errors.Add($"actions '{action.Id}': mode는 manual·auto 중 하나여야 한다.");
                }

                if (action.Mode == ActionRecord.k_Manual && string.IsNullOrEmpty(action.Icon))
                {
                    errors.Add($"actions '{action.Id}': manual 행동은 icon이 있어야 한다.");
                }

                if (action.IsAuto && System.Array.IndexOf(k_ManualOnlyActionIds, action.Id) >= 0)
                {
                    errors.Add($"actions '{action.Id}': 시트를 여는 행동은 manual만 된다.");
                }
            }

            foreach (string id in k_ActionIds)
            {
                if (!ids.Contains(id))
                {
                    errors.Add($"actions: '{id}' 행이 없다.");
                }
            }
        }

        // 설계 10: 코드가 아는 효과음 3개가 모두 있고, volume 0~1, 간격·피치 증가 ≥ 0, pitchMax ≥ 1
        private static void ValidateSounds(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (SoundRecord sound in tables.Sounds)
            {
                CheckId("sounds", sound.Id, k_IdPattern, ids, errors);

                if (string.IsNullOrEmpty(sound.Clip))
                {
                    errors.Add($"sounds '{sound.Id}': clip이 있어야 한다.");
                }

                if (sound.Volume < 0d || sound.Volume > 1d)
                {
                    errors.Add($"sounds '{sound.Id}': volume은 0~1이어야 한다.");
                }

                if (sound.MinGap < 0d || sound.ComboSeconds < 0d || sound.PitchStep < 0d)
                {
                    errors.Add($"sounds '{sound.Id}': minGap·comboSeconds·pitchStep은 0 이상이어야 한다.");
                }

                if (sound.PitchMax < 1d)
                {
                    errors.Add($"sounds '{sound.Id}': pitchMax는 1 이상이어야 한다.");
                }
            }

            foreach (string id in k_SoundIds)
            {
                if (!ids.Contains(id))
                {
                    errors.Add($"sounds: '{id}' 행이 없다.");
                }
            }
        }

        // 설계 09 v0.4: 코드의 사물 종류 5개가 모두 있고, range > 0, actions가 1개 이상이며 모두 actions.json에 있다
        private static void ValidateInteractables(GameTables tables, List<string> errors)
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> actionIds = new HashSet<string>();

            foreach (ActionRecord action in tables.Actions)
            {
                actionIds.Add(action.Id);
            }

            foreach (InteractableRecord element in tables.Interactables)
            {
                CheckId("interactables", element.Id, k_IdPattern, ids, errors);

                if (element.Range <= 0d)
                {
                    errors.Add($"interactables '{element.Id}': range는 0보다 커야 한다.");
                }

                if (element.Actions == null || element.Actions.Count == 0)
                {
                    errors.Add($"interactables '{element.Id}': actions가 비어 있다.");
                    continue;
                }

                foreach (string action in element.Actions)
                {
                    if (!actionIds.Contains(action))
                    {
                        errors.Add($"interactables '{element.Id}': 행동 '{action}'이 actions.json에 없다.");
                    }
                }
            }

            foreach (string id in Interactable.k_KindIds)
            {
                if (!ids.Contains(id))
                {
                    errors.Add($"interactables: '{id}' 행이 없다.");
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

            if (config.Offline.MaxSeconds <= 0)
            {
                errors.Add("game_config: offline.maxSeconds가 0 이하다.");
            }

            GameConfig.ShopConfig shop = config.Shop;

            if (shop.ArrivalSeconds <= 0d || shop.CheckoutSeconds <= 0d || shop.HopSeconds <= 0d || shop.PickSeconds < 0d
                || shop.PatienceSeconds < 0d || shop.LookSeconds <= 0d || shop.MaxCustomers < 1 || shop.ShelfCapacity < 1 || shop.OvenCount < 1)
            {
                errors.Add("game_config: shop의 arrivalSeconds·checkoutSeconds·hopSeconds·lookSeconds는 0보다, pickSeconds·patienceSeconds는 0 이상, maxCustomers·shelfCapacity·ovenCount는 1 이상이어야 한다.");
            }

            // 굴 격자 설계 v0.5
            if (shop.CellWidth <= 0d || shop.CellHeight <= 0d || shop.EntranceHeight <= 0d || shop.WalkSpeed <= 0d || shop.DigBaseCost <= 0d || shop.DigCostGrowth < 1d)
            {
                errors.Add("game_config: shop의 cellWidth·cellHeight·entranceHeight·walkSpeed·digBaseCost는 0보다, digCostGrowth는 1 이상이어야 한다.");
            }

            // 설계 09
            if (shop.WombatSpeed <= 0d || shop.CarryCapacity < 1)
            {
                errors.Add("game_config: shop의 wombatSpeed는 0보다, carryCapacity는 1 이상이어야 한다.");
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
