using System.Collections.Generic;
using System.Linq;
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
            ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id,
            DigInteractable.k_Id, PassageInteractable.k_Exit, PassageInteractable.k_Door, ClerkInteractable.k_Id, PoopInteractable.k_Id,
            PlotInteractable.k_Id, StatueInteractable.k_Id, MerchantInteractable.k_Id, BarnInteractable.k_Id, StairInteractable.k_Id,
            StakeInteractable.k_Id, StreamBankInteractable.k_Id, FishingHutInteractable.k_Id, BossCatchInteractable.k_Id,
        };
        // 설계 22: 코드가 부르는 말풍선·대화
        private static readonly string[] k_BubbleIds =
        {
            BubbleTable.k_Wait, BubbleTable.k_Note, BubbleTable.k_Question, BubbleTable.k_Alert, BubbleTable.k_Heart, BubbleTable.k_Angry, BubbleTable.k_Chat,
            BubbleTable.k_Yuck, BubbleTable.k_Wow,
        };
        // 시트를 열거나 곳을 옮기는 행동은 버튼으로만
        private static readonly string[] k_ManualOnlyActionIds = { ActionTable.k_Open, ActionTable.k_OpenDig, ActionTable.k_OpenPlant };
        private static readonly string[] k_ConfigIds =
        {
            ConfigTable.k_StartCoins, ConfigTable.k_CellWidth, ConfigTable.k_CellHeight, ConfigTable.k_EntranceHeight,
            ConfigTable.k_WalkSpeed, ConfigTable.k_WombatSpeed, ConfigTable.k_HopSeconds, ConfigTable.k_CarryCapacity, ConfigTable.k_WombatDigSeconds, ConfigTable.k_PlaceCell,
            ConfigTable.k_PoopEvery, ConfigTable.k_PoopChance, ConfigTable.k_PoopMax, ConfigTable.k_PoopGap, ConfigTable.k_PoopAvoidRadius,
            ConfigTable.k_AutosaveSeconds, ConfigTable.k_OfflineMaxSeconds, ConfigTable.k_OfflineMinSeconds, ConfigTable.k_OfflineTripSeconds,
        };

        public static IReadOnlyList<string> Validate(TableSet tables)
        {
            List<string> errors = new List<string>();

            ValidateVisitors(tables, errors);
            ValidateClerks(tables, errors);
            ValidateClerkConfig(tables, errors);
            ValidateBubbles(tables, errors);
            ValidateDialogues(tables, errors);
            ValidateItems(tables, errors);
            ValidateBreads(tables, errors);
            ValidateCrops(tables, errors);
            ValidateActions(tables, errors);
            ValidateInteractables(tables, errors);
            ValidateSounds(tables, errors);
            ValidateBgms(tables, errors);
            ValidateDecorations(tables, errors);
            ValidateStrings(tables, errors);
            ValidateConfig(tables, errors);
            ValidateBakeryConfig(tables, errors);
            ValidatePlazaConfig(tables, errors);
            ValidateBlessings(tables, errors);
            ValidateRelics(tables, errors);
            ValidateFarmConfig(tables, errors);
            ValidateStars(tables, errors);
            ValidateFishing(tables, errors);

            return errors;
        }

        private static void ValidateVisitors(TableSet tables, List<string> errors)
        {
            if (tables.GetAll<VisitorTable>().Count == 0)
            {
                errors.Add("VisitorTable: 행이 하나도 없다.");
            }

            bool customer = false;
            bool clerk = false;

            foreach (VisitorTable visitor in tables.GetAll<VisitorTable>())
            {
                customer |= visitor.Role == VisitorRole.Customer;
                clerk |= visitor.Role == VisitorRole.Clerk;

                if (!k_VisitorIdPattern.IsMatch(visitor.Id))
                {
                    errors.Add($"VisitorTable '{visitor.Id}': 관광객 ID는 v + 두 자리 번호여야 한다.");
                }

                if (string.IsNullOrEmpty(visitor.Sprite))
                {
                    errors.Add($"VisitorTable '{visitor.Id}': sprite 경로가 비어 있다.");
                }

                if (visitor.Scale <= 0d || visitor.HandRatio <= 0d)
                {
                    errors.Add($"VisitorTable '{visitor.Id}': scale·handRatio는 0보다 커야 한다.");
                }
            }

            // 설계 21: 손님 외형과 점원 외형이 하나씩은 있어야 한다
            if (tables.GetAll<VisitorTable>().Count > 0 && (!customer || !clerk))
            {
                errors.Add("VisitorTable: role customer와 clerk 행이 하나씩은 있어야 한다.");
            }
        }

        // 설계 21 · 38: 점원 역할은 worker 자리가 있는 사물(오븐·계산대·농장 작업대)마다 하나, baseWage > 0
        private static void ValidateClerks(TableSet tables, List<string> errors)
        {
            Dictionary<string, InteractableTable> things = tables.GetAll<InteractableTable>().ToDictionary(t => t.Id);

            foreach (ClerkTable role in tables.GetAll<ClerkTable>())
            {
                CheckId("ClerkTable", role.Id, errors);

                if (string.IsNullOrEmpty(role.Name) || role.BaseWage <= 0d)
                {
                    errors.Add($"ClerkTable '{role.Id}': name이 있고 baseWage는 0보다 커야 한다.");
                }

                if (!things.TryGetValue(role.Id, out InteractableTable thing) || !HasWorkerSpot(thing))
                {
                    errors.Add($"ClerkTable '{role.Id}': InteractableTable에 같은 id의 사물이 없거나 그 사물에 worker 자리가 없다.");
                }
            }

            CheckRequired<ClerkTable>(tables, new[] { OvenInteractable.k_Id, CounterInteractable.k_Id, BarnInteractable.k_Id, FishingHutInteractable.k_Id }, errors);
        }

        private static bool HasWorkerSpot(InteractableTable thing)
        {
            return thing.Spots != null && thing.Spots.Any(spot => spot.Role == SpotRole.Worker);
        }

        private static void ValidateClerkConfig(TableSet tables, List<string> errors)
        {
            foreach (ClerkConfigTable config in tables.GetAll<ClerkConfigTable>())
            {
                if (config.WagePeriodSeconds <= 0d || config.CandidateCount < 1 || config.RefreshCost < 0d || config.SkillSkew <= 0d || config.WagePerSkill < 0d)
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': wagePeriodSeconds·skillSkew는 0보다, candidateCount는 1 이상, refreshCost·wagePerSkill은 0 이상이어야 한다.");
                }

                if (config.IdleSecondsMin < 0d || config.IdleSecondsMax < config.IdleSecondsMin || config.MarkerSpeedMin <= 0d || config.MarkerSpeedMax < config.MarkerSpeedMin
                    || config.NegotiateSeconds <= 0d || config.ChangeMin < 0d || config.ChangeMax < config.ChangeMin)
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': idleSeconds·markerSpeed·change는 Min 0 이상(markerSpeedMin은 0보다) Max ≥ Min, negotiateSeconds는 0보다 커야 한다.");
                }

                bool red = false;

                foreach (NegotiationZone zone in config.Zones ?? new List<NegotiationZone>())
                {
                    red |= zone.Color == ZoneColor.Red;

                    if (zone.HalfWidth <= 0d || zone.Keep < 0 || zone.Down < 0 || zone.Up < 0 || zone.Keep + zone.Down + zone.Up == 0)
                    {
                        errors.Add($"ClerkConfigTable '{config.Id}': zones의 halfWidth는 0보다, keep·down·up은 0 이상이고 합이 0보다 커야 한다.");
                    }
                }

                if (!red)
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': zones에 red가 있어야 한다(못 맞췄을 때·시간 초과).");
                }

                if (config.Names == null || config.Names.Count < config.CandidateCount)
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': names는 candidateCount 이상이어야 한다.");
                }

                // 설계 22
                if (config.StrollRadius <= 0d || config.StrollChance < 0d || config.OutingChance < 0d || config.ChatChance < 0d
                    || config.StrollChance + config.OutingChance + config.ChatChance > 1d || config.ChatOffset <= 0d || config.WakeSkips < 0 || config.QuestionHoldSeconds < 0d)
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': strollRadius·chatOffset은 0보다, strollChance·outingChance·chatChance는 0 이상이고 합이 1 이하, wakeSkips·questionHoldSeconds는 0 이상이어야 한다.");
                }

                // 수다: 대화가 DialogueTable에 있고 상대(partner) 줄이 있어야 주고받는다
                if (config.ChatChance > 0d && (config.ChatDialogues == null || config.ChatDialogues.Count == 0))
                {
                    errors.Add($"ClerkConfigTable '{config.Id}': chatChance가 0보다 크면 chatDialogues가 있어야 한다.");
                }

                foreach (string id in config.ChatDialogues ?? new List<string>())
                {
                    DialogueTable dialogue = System.Linq.Enumerable.FirstOrDefault(tables.GetAll<DialogueTable>(), d => d.Id == id);

                    if (dialogue == null || dialogue.Lines == null || !dialogue.Lines.Exists(l => l.Speaker == DialogueSpeaker.Partner))
                    {
                        errors.Add($"ClerkConfigTable '{config.Id}': 수다 대화 '{id}'가 DialogueTable에 없거나 partner 줄이 없다.");
                    }
                }
            }

            CheckRequired<ClerkConfigTable>(tables, new[] { ClerkConfigTable.k_Main }, errors);
        }

        // 설계 22: 말풍선 칸 번호·프레임·시간, 코드가 부르는 id 전부
        private static void ValidateBubbles(TableSet tables, List<string> errors)
        {
            foreach (BubbleTable bubble in tables.GetAll<BubbleTable>())
            {
                CheckId("BubbleTable", bubble.Id, errors);

                if (bubble.Frame < 0 || bubble.Frames < 1 || bubble.Seconds < 0d || bubble.Frames > 1 && bubble.FrameSeconds <= 0d)
                {
                    errors.Add($"BubbleTable '{bubble.Id}': frame·seconds는 0 이상, frames는 1 이상, 프레임이 여럿이면 frameSeconds는 0보다 커야 한다.");
                }
            }

            CheckRequired<BubbleTable>(tables, k_BubbleIds, errors);
        }

        // 설계 22: 대화 줄의 문구는 StringTable에, 이모지는 BubbleTable에 있어야 한다
        private static void ValidateDialogues(TableSet tables, List<string> errors)
        {
            HashSet<string> strings = Ids<StringTable>(tables);
            HashSet<string> bubbles = Ids<BubbleTable>(tables);

            foreach (DialogueTable dialogue in tables.GetAll<DialogueTable>())
            {
                CheckId("DialogueTable", dialogue.Id, errors);

                if (dialogue.LineSeconds <= 0d || dialogue.Lines == null || dialogue.Lines.Count == 0)
                {
                    errors.Add($"DialogueTable '{dialogue.Id}': lineSeconds는 0보다 크고 lines가 있어야 한다.");
                    continue;
                }

                foreach (DialogueLineData line in dialogue.Lines)
                {
                    if (line.Texts == null || line.Texts.Count == 0)
                    {
                        errors.Add($"DialogueTable '{dialogue.Id}': 줄의 texts가 비어 있다.");
                        continue;
                    }

                    foreach (string text in line.Texts)
                    {
                        if (!strings.Contains(text))
                        {
                            errors.Add($"DialogueTable '{dialogue.Id}': 문구 '{text}'가 StringTable에 없다.");
                        }
                    }

                    if (line.Bubble != null && !bubbles.Contains(line.Bubble))
                    {
                        errors.Add($"DialogueTable '{dialogue.Id}': 말풍선 '{line.Bubble}'이 BubbleTable에 없다.");
                    }
                }
            }

            CheckRequired<DialogueTable>(tables, new[] { DialogueTable.k_ClerkWake, DialogueTable.k_MerchantHello, DialogueTable.k_JudgePass, DialogueTable.k_JudgeFail }, errors);
        }

        // 설계 08 v0.5
        private static void ValidateBreads(TableSet tables, List<string> errors)
        {
            HashSet<string> itemIds = Ids<ItemTable>(tables);

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

                // 설계 25: 레시피는 있는 재료를 1개 이상씩(재료 없는 빵은 빈 목록)
                if (bread.Ingredients == null)
                {
                    errors.Add($"BreadTable '{bread.Id}': ingredients가 없다.");
                    continue;
                }

                foreach (IngredientData ingredient in bread.Ingredients)
                {
                    if (!itemIds.Contains(ingredient.Item) || ingredient.Count < 1)
                    {
                        errors.Add($"BreadTable '{bread.Id}': 재료 '{ingredient.Item}'이 ItemTable에 없거나 count가 1보다 작다.");
                    }
                }
            }
        }

        // 설계 25 · 26: 재료는 이름·설명·아이콘이 있고 시작 개수는 0 이상
        private static void ValidateItems(TableSet tables, List<string> errors)
        {
            HashSet<string> strings = Ids<StringTable>(tables);

            foreach (ItemTable item in tables.GetAll<ItemTable>())
            {
                CheckId("ItemTable", item.Id, errors);

                if (string.IsNullOrEmpty(item.Name) || string.IsNullOrEmpty(item.Desc) || string.IsNullOrEmpty(item.Icon) || item.Start < 0)
                {
                    errors.Add($"ItemTable '{item.Id}': name·desc·icon이 있고 start는 0 이상이어야 한다.");
                }

                // 설계 28: 획득처 · 사용처 글은 있으면 StringTable에
                if ((item.Source != null && !strings.Contains(item.Source)) || (item.Use != null && !strings.Contains(item.Use)))
                {
                    errors.Add($"ItemTable '{item.Id}': source·use 글이 StringTable에 없다.");
                }
            }
        }

        // 설계 25: 작물은 하나 이상(첫 행은 처음부터 열림), 거두는 재료가 ItemTable에 있고, 자라는 그림은 2단계 이상. 설계 35: 해금 값은 0 이상
        private static void ValidateCrops(TableSet tables, List<string> errors)
        {
            HashSet<string> itemIds = Ids<ItemTable>(tables);

            if (tables.GetAll<CropTable>().Count == 0)
            {
                errors.Add("CropTable: 행이 하나도 없다.");
            }

            foreach (CropTable crop in tables.GetAll<CropTable>())
            {
                CheckId("CropTable", crop.Id, errors);

                if (!itemIds.Contains(crop.Item))
                {
                    errors.Add($"CropTable '{crop.Id}': 재료 '{crop.Item}'이 ItemTable에 없다.");
                }

                if (crop.GrowSeconds <= 0d || crop.Yield < 1 || string.IsNullOrEmpty(crop.Sprite) || crop.Stages < 2 || crop.UnlockCost < 0d || string.IsNullOrEmpty(crop.ReadyMark))
                {
                    errors.Add($"CropTable '{crop.Id}': growSeconds는 0보다, yield는 1 이상, sprite · readyMark가 있고 stages는 2 이상, unlockCost는 0 이상이어야 한다.");
                }
            }
        }

        // 설계 09 v0.4 · 설계 13 v0.5: 코드가 아는 행동이 모두 있고, manual이면 아이콘, 시트 열기는 manual만, 시트 줄 행동은 sheet만
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
                    errors.Add($"ActionTable '{action.Id}': 시트를 여는 행동은 manual만 된다.");
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

        // 설계 10·23: 코드가 아는 효과음(SoundTable.Ids)이 모두 있고, volume 0~1, 간격·피치 증가 ≥ 0, pitchMax ≥ 1
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

            CheckRequired<SoundTable>(tables, SoundTable.Ids, errors);
        }

        // 설계 23: 곳 배경음악 — 빵집 행은 꼭 있고, clip이 있고, volume 0~1
        private static void ValidateBgms(TableSet tables, List<string> errors)
        {
            foreach (BgmTable bgm in tables.GetAll<BgmTable>())
            {
                CheckId("BgmTable", bgm.Id, errors);

                if (string.IsNullOrEmpty(bgm.Clip))
                {
                    errors.Add($"BgmTable '{bgm.Id}': clip이 있어야 한다.");
                }

                if (bgm.Volume < 0d || bgm.Volume > 1d)
                {
                    errors.Add($"BgmTable '{bgm.Id}': volume은 0~1이어야 한다.");
                }
            }

            CheckRequired<BgmTable>(tables, new[] { BgmTable.k_Bakery }, errors);
        }

        // 설계 09 v0.4: 코드의 사물 종류(k_InteractableIds)가 모두 있고, range > 0, actions가 1개 이상이며 모두 ActionTable에 있다.
        // 설계 38: 점원 자리 전용 사물(농장 작업대)은 웜뱃이 다루지 않아 actions가 비고 worker 자리만 있다
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
                    if (!HasWorkerSpot(element))
                    {
                        errors.Add($"InteractableTable '{element.Id}': actions가 비어 있다(점원 자리 전용이면 worker 자리가 있어야 한다).");
                    }

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
                ValidatePlaced("InteractableTable", element, errors);
            }

            CheckRequired<InteractableTable>(tables, k_InteractableIds, errors);
        }

        // 설계 18: 살 수 있는(price가 있는) 사물은 바닥 사각형이 있고 자리 오프셋이 있으며 가격이 성립한다. 장식은 모두 살 수 있다
        private static void ValidatePlaced(string table, IPlacedKind kind, List<string> errors)
        {
            PriceInfo price = kind.Price;

            if (price == null)
            {
                return;
            }

            if (kind.HalfWidth <= 0d || kind.Depth <= 0d || kind.Spots == null || kind.Spots.Count == 0)
            {
                errors.Add($"{table} '{kind.Id}': 살 수 있는 사물은 halfWidth·depth가 0보다 크고 spots가 있어야 한다.");
            }

            if (price.BaseCost < 0d || price.Growth < 1d || price.Max < 1 || price.Start < 0)
            {
                errors.Add($"{table} '{kind.Id}': price의 baseCost·start는 0 이상, growth·max는 1 이상이어야 한다.");
            }
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

                if (decor.Price == null)
                {
                    errors.Add($"DecorationTable '{decor.Id}': price가 없다.");
                }
                else
                {
                    ValidatePlaced("DecorationTable", decor, errors);
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

                if ((row.Id == ConfigTable.k_CarryCapacity || row.Id == ConfigTable.k_PoopMax) && row.Value != System.Math.Floor(row.Value))
                {
                    errors.Add($"ConfigTable '{row.Id}': carryCapacity · poopMax는 정수여야 한다.");
                }

                // 설계 24 · 37: 똥 확률은 1 이하
                if (row.Id == ConfigTable.k_PoopChance && row.Value > 1d)
                {
                    errors.Add($"ConfigTable '{row.Id}': poopChance는 0~1이어야 한다.");
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

                // 설계 44: 뒷벽 문은 칸 안 · 서로 다른 칸 · 서로 다른 곳이고, 손님이 드나드는 빵집 문이 있다
                PlazaDoorData[] doors = plaza.Doors ?? new PlazaDoorData[0];

                if (doors.Any(d => string.IsNullOrEmpty(d.To) || d.Col < 0 || d.Col >= plaza.Cols)
                    || doors.Select(d => d.Col).Distinct().Count() != doors.Length
                    || doors.Select(d => d.To).Distinct().Count() != doors.Length
                    || doors.All(d => d.To != BakeryArea.k_Id))
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': doors는 칸 안(0 ~ cols−1) · 서로 다른 칸 · 서로 다른 곳이고 bakery 문이 있어야 한다.");
                }
            }

            CheckRequired<PlazaConfigTable>(tables, new[] { PlazaConfigTable.k_Main }, errors);
        }

        // 설계 30: 석상 축복(코드가 거는 여섯이 다 있고, 문구 · 아이콘이 있고, 값 · 시간 > 0, 비중 합 > 0) · 광장 쉬는 시간 > 0 · 석상 사물은 바닥 사각형이 있다
        private static void ValidateBlessings(TableSet tables, List<string> errors)
        {
            HashSet<string> strings = Ids<StringTable>(tables);
            double weights = 0d;

            foreach (BlessingTable blessing in tables.GetAll<BlessingTable>())
            {
                CheckId("BlessingTable", blessing.Id, errors);
                weights += blessing.Weight;

                if (!strings.Contains(blessing.Name ?? string.Empty) || !strings.Contains(blessing.Format ?? string.Empty) || string.IsNullOrEmpty(blessing.Icon))
                {
                    errors.Add($"BlessingTable '{blessing.Id}': name · format이 StringTable에 없거나 icon이 비었다.");
                }

                if (blessing.Value <= 0d || blessing.Seconds <= 0d || blessing.Weight < 0d)
                {
                    errors.Add($"BlessingTable '{blessing.Id}': value · seconds는 0보다 크고 weight는 0 이상이어야 한다.");
                }
            }

            if (weights <= 0d)
            {
                errors.Add("BlessingTable: weight 합이 0보다 커야 한다.");
            }

            CheckRequired<BlessingTable>(tables, BlessingTable.Ids, errors);

            foreach (PlazaConfigTable plaza in tables.GetAll<PlazaConfigTable>())
            {
                if (plaza.BlessingCooldown <= 0d)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': blessingCooldown은 0보다 커야 한다.");
                }
            }

            InteractableTable statue = tables.GetAll<InteractableTable>().FirstOrDefault(t => t.Id == StatueInteractable.k_Id);

            if (statue != null && (statue.HalfWidth <= 0d || statue.Depth <= 0d))
            {
                errors.Add("InteractableTable 'statue': 길을 막는 halfWidth · depth가 0보다 커야 한다.");
            }
        }

        // 설계 31: 유물(효과 키가 코드에 있고, 문구 · 아이콘이 있고, 별 값 셋이 0보다 크며 별마다 세지고(주판 · 시계는 작아지고), 비중 합 > 0) ·
        // 광장 뽑기 재료는 ItemTable에, 값 · 칸 ≥ 1 · 수레 사물은 바닥 사각형이 있다
        private static void ValidateRelics(TableSet tables, List<string> errors)
        {
            HashSet<string> strings = Ids<StringTable>(tables);
            double weights = 0d;

            foreach (RelicTable relic in tables.GetAll<RelicTable>())
            {
                CheckId("RelicTable", relic.Id, errors);
                weights += relic.Weight;

                if (!strings.Contains(relic.Name ?? string.Empty) || !strings.Contains(relic.Format ?? string.Empty) || string.IsNullOrEmpty(relic.Icon))
                {
                    errors.Add($"RelicTable '{relic.Id}': name · format이 StringTable에 없거나 icon이 비었다.");
                }

                if (!RelicTable.Effects.Contains(relic.Effect) || relic.Weight < 0d)
                {
                    errors.Add($"RelicTable '{relic.Id}': effect가 코드에 없거나(RelicTable.Effects) weight가 0보다 작다.");
                }

                bool shrinks = relic.Effect == RelicTable.k_Abacus || relic.Effect == RelicTable.k_Clock;

                if (relic.Values == null || relic.Values.Length != 3 || relic.Values.Any(v => v <= 0d)
                    || !relic.Values.Zip(relic.Values.Skip(1), (a, b) => shrinks ? b < a : b > a).All(ok => ok))
                {
                    errors.Add($"RelicTable '{relic.Id}': values는 0보다 큰 셋이고 별마다 커져야 한다(abacus · clock은 작아진다).");
                }
            }

            if (weights <= 0d)
            {
                errors.Add("RelicTable: weight 합이 0보다 커야 한다.");
            }

            HashSet<string> itemIds = Ids<ItemTable>(tables);

            foreach (PlazaConfigTable plaza in tables.GetAll<PlazaConfigTable>())
            {
                if (plaza.RelicItem == null || !itemIds.Contains(plaza.RelicItem) || plaza.RelicCost < 1 || plaza.RelicSlots < 1)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': relicItem은 ItemTable에 있고, relicCost · relicSlots는 1 이상이어야 한다.");
                }

                // 떠돌이 행상: 머무는 동안 다음 방문이 오지 않게, 외형은 역할 merchant 행
                VisitorTable look = tables.GetAll<VisitorTable>().FirstOrDefault(v => v.Id == plaza.MerchantLook);

                if (plaza.MerchantFirst < 0d || plaza.MerchantStay <= 0d || plaza.MerchantEvery <= plaza.MerchantStay || look == null || look.Role != VisitorRole.Merchant)
                {
                    errors.Add($"PlazaConfigTable '{plaza.Id}': merchantFirst ≥ 0, 0 < merchantStay < merchantEvery, merchantLook은 VisitorTable의 merchant 행이어야 한다.");
                }
            }
        }

        // 설계 28: 거름 · 덤 재료는 ItemTable에, 배율 · 확률 범위. 설계 27: 밭 여백
        private static void ValidateFarmConfig(TableSet tables, List<string> errors)
        {
            HashSet<string> itemIds = Ids<ItemTable>(tables);

            foreach (FarmConfigTable farm in tables.GetAll<FarmConfigTable>())
            {
                if (farm.ManureItem == null || !itemIds.Contains(farm.ManureItem) || farm.BonusItem == null || !itemIds.Contains(farm.BonusItem)
                    || farm.ManureGrowScale <= 0d || farm.ManureGrowScale > 1d || farm.BonusChance < 0d || farm.BonusChance > 1d || farm.ManureBonusScale < 1d)
                {
                    errors.Add($"FarmConfigTable '{farm.Id}': manureItem·bonusItem은 ItemTable에 있고, manureGrowScale은 0 초과 1 이하, bonusChance는 0~1, manureBonusScale은 1 이상이어야 한다.");
                }

                if (farm.FieldInset < 0d || farm.FieldInset >= 1d || farm.EarthEvery < 1)
                {
                    errors.Add($"FarmConfigTable '{farm.Id}': fieldInset는 0 이상 1 미만, earthEvery는 1 이상이어야 한다.");
                }
            }

            CheckRequired<FarmConfigTable>(tables, new[] { FarmConfigTable.k_Main }, errors);
            ValidateFarmFloors(tables, errors);
        }

        // 설계 27 · 39: 층은 시작 칸(가운데 두 열 × startRows줄, 입구 줄 포함)을 담고, 시작 밭은 시작 칸의 밭 줄 안, 값은 0보다 크고 증가율은 1 이상.
        // 첫 층은 농장(FarmArea.k_Id)이고 처음부터 열려 openCost 0, 아래층은 openCost > 0. 층마다 점원 팝업 탭 글(clerk_tab_<id>)
        private static void ValidateFarmFloors(TableSet tables, List<string> errors)
        {
            IReadOnlyList<FarmFloorTable> floors = tables.GetAll<FarmFloorTable>();
            HashSet<string> strings = Ids<StringTable>(tables);

            if (floors.Count == 0 || floors[0].Id != FarmArea.k_Id)
            {
                errors.Add($"FarmFloorTable: 첫 행은 '{FarmArea.k_Id}'(1층)여야 한다.");
            }

            for (int i = 0; i < floors.Count; i++)
            {
                FarmFloorTable floor = floors[i];

                if (floor.FloorCols < 2 || floor.StartRows < 2 || floor.StartRows > floor.FloorRows || floor.StartFields < 0 || floor.StartFields > 2 * (floor.StartRows - 1)
                    || floor.DigBaseCost <= 0d || floor.DigCostGrowth < 1d || floor.TillCost <= 0d)
                {
                    errors.Add($"FarmFloorTable '{floor.Id}': floorCols는 2 이상, startRows는 2 ~ floorRows, startFields는 0 ~ 2 × (startRows − 1), digBaseCost·tillCost는 0보다 크고 digCostGrowth는 1 이상이어야 한다.");
                }

                if (i == 0 ? floor.OpenCost != 0d : floor.OpenCost <= 0d)
                {
                    errors.Add($"FarmFloorTable '{floor.Id}': openCost는 첫 층 0, 아래층은 0보다 커야 한다.");
                }

                if (!strings.Contains("clerk_tab_" + floor.Id))
                {
                    errors.Add($"FarmFloorTable '{floor.Id}': StringTable에 clerk_tab_{floor.Id}가 있어야 한다.");
                }
            }
        }

        // 설계 40: 별 평가 공식 값의 범위, 보상 재료 · 평가단장 외형, 가게마다 star 0 마일스톤(상한 모두 > 0), 마일스톤 글은 StringTable에
        private static void ValidateStars(TableSet tables, List<string> errors)
        {
            HashSet<string> items = Ids<ItemTable>(tables);
            HashSet<string> strings = Ids<StringTable>(tables);
            IReadOnlyList<StarMilestoneTable> milestones = tables.GetAll<StarMilestoneTable>();

            foreach (StarConfigTable star in tables.GetAll<StarConfigTable>())
            {
                VisitorTable judge = tables.GetAll<VisitorTable>().FirstOrDefault(v => v.Id == star.JudgeLook);

                if (star.TimeBase <= 0d || star.TimePerStar < 0d || star.TimeMax < star.TimeBase || star.BigTime <= 0d || star.BigEvery < 2 || star.Rush < 1d
                    || star.ServeBase < 1 || star.ServePerStar < 0 || star.SellBase < 1 || star.SellPerStar < 0d || star.LostBase < 0 || star.LostEvery < 1
                    || star.PriceBonus < 0d || star.VisitorsBonus < 0d || star.AdmirePerStar < 0d || star.Cooldown < 0d)
                {
                    errors.Add($"StarConfigTable '{star.Id}': 시간 > 0(최대 ≥ 기본), bigEvery ≥ 2, rush ≥ 1, 만족 · 판매 기본 ≥ 1, lostEvery ≥ 1, 나머지 값 ≥ 0이어야 한다.");
                }

                if (star.TipFrom < 0 || star.TipBase < 0d || star.TipPerStar < 0d || star.TipMax < star.TipBase || star.TipMax > 1d || star.TipRate <= 0d)
                {
                    errors.Add($"StarConfigTable '{star.Id}': 팁은 tipFrom ≥ 0, 0 ≤ tipBase ≤ tipMax ≤ 1, tipPerStar ≥ 0, tipRate > 0이어야 한다.");
                }

                if (star.RewardItem == null || !items.Contains(star.RewardItem) || star.RewardCount < 0 || judge == null || judge.Role != VisitorRole.Judge)
                {
                    errors.Add($"StarConfigTable '{star.Id}': rewardItem은 ItemTable에, rewardCount ≥ 0, judgeLook은 VisitorTable의 judge 행이어야 한다.");
                }

                StarMilestoneTable start = milestones.FirstOrDefault(m => m.Shop == star.Id && m.Star == 0);

                if (start == null || start.ShelfMax <= 0 || start.OvenMax <= 0 || start.CounterMax <= 0 || start.UpgradeMax <= 0)
                {
                    errors.Add($"StarMilestoneTable: '{star.Id}' 가게의 star 0 행이 있고 상한이 모두 0보다 커야 한다.");
                }
            }

            foreach (StarMilestoneTable milestone in milestones)
            {
                if (tables.GetAll<StarConfigTable>().All(c => c.Id != milestone.Shop) || milestone.Star < 0 || milestone.ShelfMax < 0 || milestone.OvenMax < 0
                    || milestone.CounterMax < 0 || milestone.UpgradeMax < 0 || milestone.Text != null && !strings.Contains(milestone.Text)
                    || milestones.Count(m => m.Shop == milestone.Shop && m.Star == milestone.Star) > 1)
                {
                    errors.Add($"StarMilestoneTable '{milestone.Id}': shop은 StarConfigTable에, star ≥ 0(가게마다 한 행), 상한 ≥ 0, text는 null이거나 StringTable에 있어야 한다.");
                }
            }

            CheckRequired<StarConfigTable>(tables, new[] { BakeryArea.k_Id }, errors);
        }

        private static void CheckId(string table, string id, List<string> errors)
        {
            if (!k_IdPattern.IsMatch(id))
            {
                errors.Add($"{table}: id '{id}'가 ID 패턴에 맞지 않는다.");
            }
        }

        // 코드가 Id로 부르는 행이 모두 있어야 한다
        // 설계 44: 낚시터 숫자 · 낚싯대 · 물고기. 물길은 축 정렬 점 둘 이상, 말뚝 하나 이상 열림, 비중 · 배수 · 시간 > 0,
        // 소환에 나오는 계열 대 · 특별한 대 셋 · 떼 물고기 · 대물 행이 있고, 물고기 재료는 ItemTable에, 크기는 min ≤ max
        private static void ValidateFishing(TableSet tables, List<string> errors)
        {
            CheckRequired<FishingConfigTable>(tables, new[] { FishingConfigTable.k_Main }, errors);
            CheckRequired<RodTable>(tables, new[] { RodTable.k_Bamboo, RodTable.k_Iron, RodTable.k_Bait }, errors);
            HashSet<string> items = Ids<ItemTable>(tables);

            foreach (FishingConfigTable config in tables.GetAll<FishingConfigTable>())
            {
                FishingPointData[] stream = config.Stream ?? new FishingPointData[0];
                bool axis = stream.Length >= 2 && Enumerable.Range(1, stream.Length - 1).All(i => stream[i].X == stream[i - 1].X || stream[i].Y == stream[i - 1].Y);
                FishTable boss = tables.GetAll<FishTable>().FirstOrDefault(f => f.Id == config.BossFish);

                if (config.Cols < 2 || config.Rows < 2 || !axis || config.StreamWidth <= 0d)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': cols · rows는 2 이상, stream은 축 정렬 점 둘 이상, streamWidth > 0이어야 한다.");
                }

                double plan = Enumerable.Range(1, System.Math.Max(0, stream.Length - 1)).Sum(i => System.Math.Abs(stream[i].X - stream[i - 1].X) + System.Math.Abs(stream[i].Y - stream[i - 1].Y));

                if (config.DugStart <= 0d || config.DugStart > plan || config.DigStep <= 0d || config.DigCost <= 0d || config.DigGrowth < 1d)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': dugStart는 0 초과 stream 길이 이하, digStep · digCost > 0, digGrowth ≥ 1이어야 한다.");
                }

                if (config.Stakes == null || !config.Stakes.Any(s => s.Cost <= 0d) || config.Stakes.Any(s => s.Cost < 0d))
                {
                    errors.Add($"FishingConfigTable '{config.Id}': stakes는 하나 이상 처음부터 열려(cost 0) 있고 cost는 0 이상이어야 한다.");
                }

                if (config.WaveSeconds <= 0d || config.SpawnGap < 0d || config.SchoolBase < 1 || config.SchoolPerStage < 0d || config.WeightGrowth < 1d
                    || config.BossEvery < 2 || boss == null || !boss.Boss)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': waveSeconds > 0, spawnGap ≥ 0, schoolBase ≥ 1, schoolPerStage ≥ 0, weightGrowth ≥ 1, bossEvery ≥ 2, bossFish는 FishTable의 boss 행이어야 한다.");
                }

                if (config.TrophyChance < 0d || config.TrophyChance > 1d || config.TrophyWeightScale < 1d || config.TrophyCatch < 1 || config.TrophyHoldSeconds <= 0d || config.HaulSeconds <= 0d)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': trophyChance는 0~1, trophyWeightScale ≥ 1, trophyCatch ≥ 1, trophyHoldSeconds · haulSeconds > 0이어야 한다.");
                }

                if (config.SummonCost <= 0d || config.SummonGrowth < 1d || config.GradeWeights == null || config.GradeWeights.Length < 1 || config.GradeWeights.Any(w => w < 0d)
                    || config.GradeWeights.Sum() <= 0d || config.GradeScale < 1d || config.MergeCount < 2)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': summonCost > 0, summonGrowth ≥ 1, gradeWeights는 0 이상이고 합 > 0, gradeScale ≥ 1, mergeCount ≥ 2여야 한다.");
                }

                if (config.FamilySteps == null || config.FamilyScales == null || config.FamilySteps.Length != config.FamilyScales.Length
                    || config.FamilySteps.Any(n => n < 2) || config.FamilyScales.Any(x => x < 1d))
                {
                    errors.Add($"FishingConfigTable '{config.Id}': familySteps · familyScales는 같은 길이, 문턱은 2 이상, 배수는 1 이상이어야 한다.");
                }

                if (config.ThumpRadius <= 0d || config.ThumpStun <= 0d || config.ThumpCooldown <= 0d || config.ThumpSeconds <= 0d || config.SitScale < 1d)
                {
                    errors.Add($"FishingConfigTable '{config.Id}': thumpRadius · thumpStun · thumpCooldown · thumpSeconds > 0, sitScale ≥ 1이어야 한다.");
                }
            }

            foreach (RodTable rod in tables.GetAll<RodTable>())
            {
                if (string.IsNullOrEmpty(rod.Family) || rod.Reel <= 0d || rod.Range <= 0d || rod.SmallScale <= 0d || rod.BigScale <= 0d || rod.Slow <= 0d || rod.Slow > 1d)
                {
                    errors.Add($"RodTable '{rod.Id}': family가 있고 reel · range · smallScale · bigScale > 0, slow는 0 초과 1 이하여야 한다.");
                }

                if (rod.IsSpecial && (rod.Special != RodTable.k_Pulley && rod.Special != RodTable.k_Lighthouse && rod.Special != RodTable.k_Whirlpool || rod.Effect <= 0d))
                {
                    errors.Add($"RodTable '{rod.Id}': special은 pulley · lighthouse · whirlpool 중 하나이고 effect > 0이어야 한다.");
                }
            }

            if (!tables.GetAll<RodTable>().Any(rod => !rod.IsSpecial && !rod.Locked) || tables.GetAll<RodTable>().Count(rod => rod.IsSpecial) < 3)
            {
                errors.Add("RodTable: 소환에 나오는 계열 대가 하나 이상, 특별한 대가 셋 이상 있어야 한다.");
            }

            foreach (FishTable fish in tables.GetAll<FishTable>())
            {
                if (!items.Contains(fish.Item ?? "") || fish.Speed <= 0d || fish.Spawn < 0d || fish.Sizes == null || fish.Sizes.Length < 1
                    || fish.Sizes.Any(size => size.Min <= 0d || size.Max < size.Min || size.Chance < 0d) || fish.Sizes.Sum(size => size.Chance) <= 0d)
                {
                    errors.Add($"FishTable '{fish.Id}': item은 ItemTable에, speed > 0, spawn ≥ 0, sizes는 0 < min ≤ max이고 chance 합 > 0이어야 한다.");
                }
            }

            if (!tables.GetAll<FishTable>().Any(fish => !fish.Boss && fish.Spawn > 0d))
            {
                errors.Add("FishTable: 떼에 나오는 물고기(spawn > 0)가 하나 이상 있어야 한다.");
            }
        }

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
