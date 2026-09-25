using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameKit.Tables;
using ZooTycoon.Core;

namespace ZooTycoon.Data
{
    // 데이터-테이블-규칙 7장: 테이블마다 칸 값 규칙. 봉투(table·version)는 EditMode 테스트가, Id 빈 값·중복과 table 이름은 TableSet이 읽을 때 검사한다.
    public static class TableValidator
    {
        private static readonly Regex k_IdPattern = new Regex("^[a-z][a-z0-9_]*$");
        private static readonly Regex k_VisitorIdPattern = new Regex("^v[0-9]{2}$");
        private static readonly Regex k_BreadIdPattern = new Regex("^b[0-9]{2}$");
        // 코드에 클래스가 있는 사물(행동은 ActionFactory.Ids)
        private static readonly string[] k_InteractableIds =
        {
            ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id, SlotInteractable.k_Id,
            DigInteractable.k_Id, PassageInteractable.k_Exit, PassageInteractable.k_Door,
        };
        private static readonly string[] k_SoundIds = { SoundTable.k_Pay, SoundTable.k_OvenDone };
        // 시트를 열거나 곳을 옮기는 행동은 버튼으로만
        private static readonly string[] k_ManualOnlyActionIds = { ActionTable.k_Open, ActionTable.k_OpenDig, ActionTable.k_Exit, ActionTable.k_Enter };
        private static readonly string[] k_ConfigIds =
        {
            ConfigTable.k_StartCoins, ConfigTable.k_CellWidth, ConfigTable.k_CellHeight, ConfigTable.k_EntranceHeight,
            ConfigTable.k_WalkSpeed, ConfigTable.k_WombatSpeed, ConfigTable.k_HopSeconds, ConfigTable.k_CarryCapacity,
        };

        public static IReadOnlyList<string> Validate(TableSet tables)
        {
            List<string> errors = new List<string>();

            ValidateVisitors(tables, errors);
            ValidateBreads(tables, errors);
            ValidateActions(tables, errors);
            ValidateInteractables(tables, errors);
            ValidateSounds(tables, errors);
            ValidateDecorations(tables, errors);
            ValidateStrings(tables, errors);
            ValidateConfig(tables, errors);
            ValidateBakeryConfig(tables, errors);
            ValidatePlazaConfig(tables, errors);

            return errors;
        }

        private static void ValidateVisitors(TableSet tables, List<string> errors)
        {
            if (tables.GetAll<VisitorTable>().Count == 0)
            {
                errors.Add("VisitorTable: 행이 하나도 없다.");
            }

            foreach (VisitorTable visitor in tables.GetAll<VisitorTable>())
            {
                if (!k_VisitorIdPattern.IsMatch(visitor.Id))
                {
                    errors.Add($"VisitorTable '{visitor.Id}': 관광객 ID는 v + 두 자리 번호여야 한다.");
                }

                if (string.IsNullOrEmpty(visitor.Sprite))
                {
                    errors.Add($"VisitorTable '{visitor.Id}': sprite 경로가 비어 있다.");
                }

                if (visitor.IdleFrameRate <= 0d || visitor.MoveFrameRate <= 0d || visitor.Scale <= 0d)
                {
                    errors.Add($"VisitorTable '{visitor.Id}': idleFrameRate·moveFrameRate·scale은 0보다 커야 한다.");
                }
            }
        }

        // 설계 08 v0.5
        private static void ValidateBreads(TableSet tables, List<string> errors)
        {
            if (tables.GetAll<BreadTable>().Count == 0)
            {
                errors.Add("BreadTable: 행이 하나도 없다.");
            }

            foreach (BreadTable bread in tables.GetAll<BreadTable>())
            {
                if (!k_BreadIdPattern.IsMatch(bread.Id))
                {
                    errors.Add($"BreadTable '{bread.Id}': 빵 ID는 b + 두 자리 번호여야 한다.");
                }

                if (string.IsNullOrEmpty(bread.Sprite))
                {
                    errors.Add($"BreadTable '{bread.Id}': sprite 경로가 비어 있다.");
                }

                if (bread.BakeSeconds <= 0d || bread.BatchSize < 1 || bread.Price <= 0d || bread.Weight < 1 || bread.UnlockCost < 0d)
                {
                    errors.Add($"BreadTable '{bread.Id}': bakeSeconds·price는 0보다, batchSize·weight는 1 이상, unlockCost는 0 이상이어야 한다.");
                }
            }
        }

        // 설계 09 v0.4 · 설계 13 v0.5: 코드가 아는 행동이 모두 있고, manual이면 아이콘, 시트 열기·곳 옮기기는 manual만, 시트 줄 행동은 sheet만
        private static void ValidateActions(TableSet tables, List<string> errors)
        {
            foreach (ActionTable action in tables.GetAll<ActionTable>())
            {
                CheckId("ActionTable", action.Id, errors);

                if (action.Mode == ActionMode.Manual && string.IsNullOrEmpty(action.Icon))
                {
                    errors.Add($"ActionTable '{action.Id}': manual 행동은 icon이 있어야 한다.");
                }

                if (action.Mode != ActionMode.Manual && System.Array.IndexOf(k_ManualOnlyActionIds, action.Id) >= 0)
                {
                    errors.Add($"ActionTable '{action.Id}': 시트를 열거나 곳을 옮기는 행동은 manual만 된다.");
                }

                // 설계 13 v0.5: 행동 클래스가 있어야 하고, 시트 줄을 내놓는 클래스(SheetAction)면 mode sheet
                if (System.Array.IndexOf(ActionFactory.Ids, action.Id) < 0)
                {
                    errors.Add($"ActionTable '{action.Id}': 코드에 행동 클래스가 없다.");
                }
                else if (ActionFactory.Create(action) is SheetAction != (action.Mode == ActionMode.Sheet))
                {
                    errors.Add($"ActionTable '{action.Id}': 시트 행동은 mode sheet, 나머지는 auto·manual이어야 한다.");
                }
            }

            CheckRequired<ActionTable>(tables, ActionFactory.Ids, errors);
        }

        // 설계 10: 코드가 아는 효과음 3개가 모두 있고, volume 0~1, 간격·피치 증가 ≥ 0, pitchMax ≥ 1
        private static void ValidateSounds(TableSet tables, List<string> errors)
        {
            foreach (SoundTable sound in tables.GetAll<SoundTable>())
            {
                CheckId("SoundTable", sound.Id, errors);

                if (string.IsNullOrEmpty(sound.Clip))
                {
                    errors.Add($"SoundTable '{sound.Id}': clip이 있어야 한다.");
                }

                if (sound.Volume < 0d || sound.Volume > 1d)
                {
                    errors.Add($"SoundTable '{sound.Id}': volume은 0~1이어야 한다.");
                }

                if (sound.MinGap < 0d || sound.ComboSeconds < 0d || sound.PitchStep < 0d)
                {
                    errors.Add($"SoundTable '{sound.Id}': minGap·comboSeconds·pitchStep은 0 이상이어야 한다.");
                }

                if (sound.PitchMax < 1d)
                {
                    errors.Add($"SoundTable '{sound.Id}': pitchMax는 1 이상이어야 한다.");
                }
            }

            CheckRequired<SoundTable>(tables, k_SoundIds, errors);
        }

        // 설계 09 v0.4: 코드의 사물 종류 5개가 모두 있고, range > 0, actions가 1개 이상이며 모두 ActionTable에 있다
        private static void ValidateInteractables(TableSet tables, List<string> errors)
        {
            HashSet<string> actionIds = Ids<ActionTable>(tables);

            foreach (InteractableTable element in tables.GetAll<InteractableTable>())
            {
                CheckId("InteractableTable", element.Id, errors);

                if (element.Range <= 0d)
                {
                    errors.Add($"InteractableTable '{element.Id}': range는 0보다 커야 한다.");
                }

                if (element.Actions == null || element.Actions.Count == 0)
                {
                    errors.Add($"InteractableTable '{element.Id}': actions가 비어 있다.");
                    continue;
                }

                foreach (string action in element.Actions)
                {
                    if (!actionIds.Contains(action))
                    {
                        errors.Add($"InteractableTable '{element.Id}': 행동 '{action}'이 ActionTable에 없다.");
                    }
                }

                ValidateUpgrade(element, errors);
            }

            CheckRequired<InteractableTable>(tables, k_InteractableIds, errors);
        }

        // 설계 11: 장식 그림·막는 자리·들를 곳, 놓인 장식(PlazaDecorTable)은 있는 장식만
        private static void ValidateDecorations(TableSet tables, List<string> errors)
        {
            foreach (DecorationTable decor in tables.GetAll<DecorationTable>())
            {
                CheckId("DecorationTable", decor.Id, errors);

                if (string.IsNullOrEmpty(decor.Sprite))
                {
                    errors.Add($"DecorationTable '{decor.Id}': sprite 경로가 비어 있다.");
                }

                if (decor.HalfWidth < 0d || decor.Depth < 0d)
                {
                    errors.Add($"DecorationTable '{decor.Id}': halfWidth·depth는 0 이상이어야 한다.");
                }

                if (decor.Frames < 0 || (decor.Frames > 0 && decor.FrameRate <= 0d))
                {
                    errors.Add($"DecorationTable '{decor.Id}': frames는 0 이상, frames가 있으면 frameRate는 0보다 커야 한다.");
                }

                if (decor.Spots == null)
                {
                    errors.Add($"DecorationTable '{decor.Id}': spots가 없다.");
                }
            }

            HashSet<string> decorIds = Ids<DecorationTable>(tables);

            foreach (PlazaDecorTable placed in tables.GetAll<PlazaDecorTable>())
            {
                if (!decorIds.Contains(placed.Decoration))
                {
                    errors.Add($"PlazaDecorTable '{placed.Id}': 장식 '{placed.Decoration}'이 DecorationTable에 없다.");
                }
            }
        }

        private static void ValidateStrings(TableSet tables, List<string> errors)
        {
            foreach (StringTable row in tables.GetAll<StringTable>())
            {
                CheckId("StringTable", row.Id, errors);

                if (string.IsNullOrEmpty(row.Ko))
                {
                    errors.Add($"StringTable '{row.Id}': ko가 비어 있다.");
                }
            }
        }

        // 전역 값은 전부 0보다 크다(시작 코인만 0 이상). 드는 빵 수는 정수
        private static void ValidateConfig(TableSet tables, List<string> errors)
        {
            foreach (ConfigTable row in tables.GetAll<ConfigTable>())
            {
                if (row.Id == ConfigTable.k_StartCoins ? row.Value < 0d : row.Value <= 0d)
                {
                    errors.Add($"ConfigTable '{row.Id}': value가 너무 작다(startCoins는 0 이상, 나머지는 0보다 커야 한다).");
                }

                if (row.Id == ConfigTable.k_CarryCapacity && row.Value != System.Math.Floor(row.Value))
                {
                    errors.Add($"ConfigTable '{row.Id}': carryCapacity는 정수여야 한다.");
                }
            }

            CheckRequired<ConfigTable>(tables, k_ConfigIds, errors);
        }

        // 설계 13 v0.6: upgrade 행동이 있는 사물만 업그레이드 데이터를 갖고, 숫자는 비용·단계·효과가 성립해야 한다
        private static void ValidateUpgrade(InteractableTable element, List<string> errors)
        {
            UpgradeInfo upgrade = element.Upgrade;

            if (element.Actions.Contains(ActionTable.k_Upgrade) != (upgrade != null))
            {
                errors.Add($"InteractableTable '{element.Id}': upgrade 행동과 upgrade 데이터는 함께 있어야 한다.");
            }

            if (upgrade == null)
            {
                return;
            }

            if (upgrade.BaseCost <= 0d || upgrade.CostGrowth < 1d || upgrade.MaxLevel < 1 || upgrade.EffectPerLevel <= 0d)
            {
                errors.Add($"InteractableTable '{element.Id}': upgrade의 baseCost·effectPerLevel은 0보다, costGrowth·maxLevel은 1 이상이어야 한다.");
            }

            if (upgrade.LookLevel < 0 || upgrade.LookLevel > upgrade.MaxLevel)
            {
                errors.Add($"InteractableTable '{element.Id}': upgrade의 lookLevel은 0 이상 maxLevel 이하여야 한다.");
            }

            if (string.IsNullOrEmpty(upgrade.Name) || string.IsNullOrEmpty(upgrade.EffectFormat))
            {
                errors.Add($"InteractableTable '{element.Id}': upgrade의 name·effectFormat이 비어 있다.");
            }
        }

        private static void ValidateBakeryConfig(TableSet tables, List<string> errors)
        {
            foreach (BakeryConfigTable bakery in tables.GetAll<BakeryConfigTable>())
            {
                if (bakery.CheckoutSeconds <= 0d || bakery.PickSeconds < 0d || bakery.PatienceSeconds < 0d || bakery.LookSeconds <= 0d
                    || bakery.MaxCustomers < 1 || bakery.ShelfCapacity < 1)
                {
                    errors.Add($"BakeryConfigTable '{bakery.Id}': checkoutSeconds·lookSeconds는 0보다, pickSeconds·patienceSeconds는 0 이상, maxCustomers·shelfCapacity는 1 이상이어야 한다.");
                }

                // 굴 격자 설계 v0.5
                if (bakery.DigBaseCost <= 0d || bakery.DigCostGrowth < 1d)
                {
                    errors.Add($"BakeryConfigTable '{bakery.Id}': digBaseCost는 0보다, digCostGrowth는 1 이상이어야 한다.");
                }

                // 설계 13 v0.6: 오븐 설치
                if (bakery.OvenBaseCost <= 0d || bakery.OvenCostGrowth < 1d || bakery.OvenMax < BakeryArea.k_StartOvens)
                {
                    errors.Add($"BakeryConfigTable '{bakery.Id}': ovenBaseCost는 0보다, ovenCostGrowth는 1 이상, ovenMax는 시작 오븐 수 이상이어야 한다.");
                }
            }

            CheckRequired<BakeryConfigTable>(tables, new[] { BakeryConfigTable.k_Bakery }, errors);
        }

        // 설계 11
        private static void ValidatePlazaConfig(TableSet tables, List<string> errors)
        {
            foreach (PlazaConfigTable plaza in tables.GetAll<PlazaConfigTable>())
            {
                if (plaza.Cols < 2 || plaza.Rows < 2 || plaza.ArrivalSeconds <= 0d || plaza.MaxVisitors < 1)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': cols·rows는 2 이상, arrivalSeconds는 0보다, maxVisitors는 1 이상이어야 한다.");
                }

                if (plaza.VisitSecondsMin < 0d || plaza.VisitSecondsMax < plaza.VisitSecondsMin || plaza.VisitsMin < 0 || plaza.VisitsMax < plaza.VisitsMin)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': visitSecondsMin·visitsMin은 0 이상, Max는 Min 이상이어야 한다.");
                }

                if (plaza.BrowseChance < 0d || plaza.BrowseChance > 1d || plaza.EmoteChance < 0d || plaza.EmoteChance > 1d)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': browseChance·emoteChance는 0~1이어야 한다.");
                }
            }

            CheckRequired<PlazaConfigTable>(tables, new[] { PlazaConfigTable.k_Main }, errors);
        }

        private static void CheckId(string table, string id, List<string> errors)
        {
            if (!k_IdPattern.IsMatch(id))
            {
                errors.Add($"{table}: id '{id}'가 ID 패턴에 맞지 않는다.");
            }
        }

        // 코드가 Id로 부르는 행이 모두 있어야 한다
        private static void CheckRequired<T>(TableSet tables, string[] ids, List<string> errors) where T : Table<string>
        {
            HashSet<string> have = Ids<T>(tables);

            foreach (string id in ids)
            {
                if (!have.Contains(id))
                {
                    errors.Add($"{typeof(T).Name}: '{id}' 행이 없다.");
                }
            }
        }

        private static HashSet<string> Ids<T>(TableSet tables) where T : Table<string>
        {
            HashSet<string> ids = new HashSet<string>();

            foreach (T row in tables.GetAll<T>())
            {
                ids.Add(row.Id);
            }

            return ids;
        }
    }
}
