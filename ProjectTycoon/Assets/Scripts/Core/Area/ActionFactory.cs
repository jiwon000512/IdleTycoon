using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.6: 표 행 → 행동. 행동 클래스는 여기 중첩 클래스로 두고 sim마다 partial 파일로 나눈다(공통: 이 파일, 빵집: ActionFactory.Bakery.cs).
    // 새 행동 = 그 sim 파일에 중첩 클래스 하나 + 여기 case 한 줄과 Ids 한 칸 + ActionTable 한 줄
    public static partial class ActionFactory
    {
        // 코드에 행동 클래스가 있는 id 전부(검증기가 표와 맞춘다)
        public static readonly string[] Ids =
        {
            ActionTable.k_Open, ActionTable.k_OpenDig, ActionTable.k_Exit, ActionTable.k_Enter, ActionTable.k_Upgrade,
            ActionTable.k_TakeOut, ActionTable.k_Fill, ActionTable.k_Serve, ActionTable.k_Bake, ActionTable.k_PlaceShelf, ActionTable.k_PlaceOven, ActionTable.k_Dig,
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
                case ActionTable.k_PlaceShelf: return new PlaceShelf(table);
                case ActionTable.k_PlaceOven: return new PlaceOven(table);
                case ActionTable.k_Dig: return new Dig(table);
                default: throw new InvalidOperationException($"행동 '{table.Id}'의 코드가 없다.");
            }
        }

        // 시트 열기(open 돋보기 · open_dig 삽). 아무 사물이나 받고, TryInteract가 false를 돌려 화면이 시트를 연다
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

        // 설계 11: 나가기(빵집 → 광장)·들어가기(광장 → 빵집). 2026-09-26부터 auto: range에 들어오면 바로, 단 들어온 직후는 아니다(Armed)
        private sealed class Pass : InteractAction
        {
            public Pass(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is PassageInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is PassageInteractable passage && passage.Armed;
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
                SheetOptionState state = maxed ? SheetOptionState.Max : SheetOption.Afford(worker, cost);
                return new[] { new SheetOption(null, state, cost, level, target.UpgradeValue(level), target.UpgradeValue(maxed ? level : level + 1)) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                UpgradeInfo upgrade = target.Table.Upgrade;
                int level = target.UpgradeLevel;

                if (level >= upgrade.MaxLevel || !worker.Wallet.TrySpendCoins(upgrade.Cost(level)))
                {
                    return false;
                }

                target.Area.LevelUp(target.Table.Id);
                return true;
            }
        }
    }
}
