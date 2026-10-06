using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.6: 표 행 → 행동. 행동 클래스는 여기 중첩 클래스로 두고 sim마다 partial 파일로 나눈다(공통: 이 파일 — 열기·통로·업그레이드·굴 파기·똥 치우기·점원 깨우기, 빵집: ActionFactory.Bakery.cs, 농장: ActionFactory.Farm.cs, 광장: ActionFactory.Plaza.cs).
    // 새 행동 = 그 sim 파일에 중첩 클래스 하나 + 여기 case 한 줄과 Ids 한 칸 + ActionTable 한 줄(낚시: Fishing/ActionFactory.Fishing.cs)
    public static partial class ActionFactory
    {
        // 코드에 행동 클래스가 있는 id 전부(검증기가 표와 맞춘다)
        public static readonly string[] Ids =
        {
            ActionTable.k_Open, ActionTable.k_OpenDig, ActionTable.k_Exit, ActionTable.k_Enter, ActionTable.k_Upgrade,
            ActionTable.k_TakeOut, ActionTable.k_Fill, ActionTable.k_Serve, ActionTable.k_Bake,  ActionTable.k_Dig,
            ActionTable.k_Wake, ActionTable.k_Clean, ActionTable.k_OpenPlant, ActionTable.k_Plant, ActionTable.k_Harvest, ActionTable.k_Till, ActionTable.k_Statue,
            ActionTable.k_Talk, ActionTable.k_DigFloor, ActionTable.k_Evaluate,
            ActionTable.k_OpenSummon, ActionTable.k_Summon, ActionTable.k_OpenStake, ActionTable.k_Haul, ActionTable.k_Thump, ActionTable.k_StageUp, ActionTable.k_HutUpgrade,
            ActionTable.k_DigStream,
        };

        public static InteractAction Create(ActionTable table)
        {
            switch (table.Id)
            {
                case ActionTable.k_Open:
                case ActionTable.k_OpenDig: return new Open(table);
                case ActionTable.k_Exit:
                case ActionTable.k_Enter: return new Pass(table);
                case ActionTable.k_Upgrade: return new Upgrade(table);
                case ActionTable.k_TakeOut: return new TakeOut(table);
                case ActionTable.k_Fill: return new Fill(table);
                case ActionTable.k_Serve: return new Serve(table);
                case ActionTable.k_Bake: return new Bake(table);
                case ActionTable.k_Dig: return new Dig(table);
                case ActionTable.k_Wake: return new Wake(table);
                case ActionTable.k_Clean: return new Clean(table);
                case ActionTable.k_OpenPlant: return new OpenPlant(table);
                case ActionTable.k_Plant: return new Plant(table);
                case ActionTable.k_Harvest: return new Harvest(table);
                case ActionTable.k_Till: return new Till(table);
                case ActionTable.k_Statue: return new OpenStatue(table);
                case ActionTable.k_Talk: return new TalkToMerchant(table);
                case ActionTable.k_DigFloor: return new DigFloor(table);
                case ActionTable.k_Evaluate: return new OpenBoard(table);
                case ActionTable.k_OpenSummon: return new OpenSummon(table);
                case ActionTable.k_Summon: return new Summon(table);
                case ActionTable.k_OpenStake: return new OpenStake(table);
                case ActionTable.k_Haul: return new Haul(table);
                case ActionTable.k_Thump: return new Thump(table);
                case ActionTable.k_StageUp: return new StageUp(table);
                case ActionTable.k_HutUpgrade: return new HutUpgrade(table);
                case ActionTable.k_DigStream: return new DigStream(table);
                default: throw new InvalidOperationException($"행동 '{table.Id}'의 코드가 없다.");
            }
        }

        // 시트 열기(open 돋보기 · open_dig 삽). 아무 사물이나 받고, TryInteract가 false를 돌려 화면이 시트를 연다. 밭 시트는 OpenPlant(ActionFactory.Farm.cs)
        private sealed class Open : InteractAction
        {
            public Open(ActionTable table) : base(table)
            {
            }

            public override bool OpensSheet => true;

            public override bool Accepts(Interactable target)
            {
                return true;
            }
        }

        // 설계 11: 나가기(빵집 → 광장)·들어가기(광장 → 빵집). 2026-09-26부터 auto: 통로 앞 띠(PassageInteractable.DistanceTo)에 들어오면 바로
        private sealed class Pass : InteractAction
        {
            public Pass(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PassageInteractable;
            }

            // 설계 40: 가게를 지키는 동안(평가 중)은 넘어가지 않는다
            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PassageInteractable && target.Area.CanLeave;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((PassageInteractable)target).Pass();
            }
        }

        // 업그레이드(시트 줄): 사물 행의 UpgradeInfo. 단계는 곳이 사물 종류별로 센다(종류 공통), 그 단계가 무슨 값인지는 사물(UpgradeValue)
        private sealed class Upgrade : SheetAction
        {
            public Upgrade(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target.Table.Upgrade != null;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                UpgradeInfo upgrade = target.Table.Upgrade;
                int level = target.UpgradeLevel;
                bool maxed = level >= upgrade.MaxLevel;
                double cost = upgrade.Cost(level);
                SheetOptionState state = maxed ? SheetOptionState.Max : Starless(worker, target, level) ? SheetOptionState.Locked : SheetOption.Afford(worker, cost);
                return new[] { new SheetOption(null, state, cost, level, target.UpgradeValue(level), target.UpgradeValue(maxed ? level : level + 1)) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                UpgradeInfo upgrade = target.Table.Upgrade;
                int level = target.UpgradeLevel;

                if (level >= upgrade.MaxLevel || Starless(worker, target, level) || !worker.Wallet.TrySpendCoins(upgrade.Cost(level)))
                {
                    return false;
                }

                target.Area.LevelUp(target.Table.Id);
                return true;
            }

            // 설계 40: 가게 별 마일스톤이 업그레이드 단계 상한을 연다
            private static bool Starless(Worker worker, Interactable target, int level)
            {
                return level >= worker.Wallet.Stars.Cap(target.Area.Id, Stars.k_Upgrade);
            }
        }

        // 굴 격자 설계 v0.5 → 설계 27: 파기(시트 줄, 빵집·농장 공용). 그 칸의 격자(DigInteractable.Grid)에 값을 치르고 판다
        private sealed class Dig : SheetAction
        {
            public Dig(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is DigInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                double cost = ((DigInteractable)target).Grid.DigCost;
                return new[] { new SheetOption(null, SheetOption.Afford(worker, cost), cost) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                DigInteractable dig = (DigInteractable)target;

                if (!dig.Grid.CanDig(dig.Cell) || !worker.Wallet.TrySpendCoins(dig.Grid.DigCost))
                {
                    return false;
                }

                // 웜뱃이 그 칸 쪽을 보고 파는 동안 선다(판 사건을 받는 그림이 방향 · 파는 중을 읽으니 파기 전에)
                Wombat wombat = dig.Area.Wombat;
                wombat.Dig(dig.ClosestPoint(wombat.Mover.Position));
                dig.Grid.Dig(dig.Cell);
                return true;
            }
        }

        // 설계 40 평가판 버튼: 평가 팝업을 연다(부르기는 팝업에서)
        private sealed class OpenBoard : InteractAction
        {
            public OpenBoard(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is BoardInteractable;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((BoardInteractable)target).Open();
            }
        }

        // 설계 24 → 37: 똥 치우기(버튼, 곳 공용). 웜뱃 range 안 똥을 전부 치운다
        private sealed class Clean : InteractAction
        {
            public Clean(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PoopInteractable;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((PoopInteractable)target).Area.CleanAround();
            }
        }

        // 설계 22: 딴짓 중인 점원 깨우기(버튼, 빵집 · 농장 · 광장 외출 그림 어디서나). 딴짓을 끊고 자리로 보내고 대화 clerk_wake를 건다
        private sealed class Wake : InteractAction
        {
            public Wake(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is ClerkInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is ClerkInteractable clerk && clerk.Clerk.Idling;
            }

            public override void Do(Worker worker, Interactable target)
            {
                Clerk clerk = ((ClerkInteractable)target).Clerk;
                clerk.WakeUp();
                clerk.Home.StartDialogue(DialogueTable.k_ClerkWake, clerk);
            }
        }
    }
}
