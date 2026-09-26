using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.6 → 설계 17·18: 빵집 행동(꺼내기·채우기·계산·굽기(해금 포함)·파기). 진열대·오븐·계산대 설치는 편집 모드(WombatArea.Placement)
    public static partial class ActionFactory
    {
        // 설계 09 v0.4: 꺼내기(auto). 오븐의 다 구운 빵을 들 수 있는 만큼 손에
        private sealed class TakeOut : InteractAction
        {
            public TakeOut(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is OvenInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is OvenInteractable oven && oven.Ready > 0 && worker.Hands.SpaceFor(oven.Bread) > 0;
            }

            public override void Do(Worker worker, Interactable target)
            {
                OvenInteractable oven = (OvenInteractable)target;
                BreadTable bread = oven.Bread;
                worker.Hands.Add(bread, oven.Take(worker.Hands.SpaceFor(bread)), oven);
            }
        }

        // 설계 09 v0.4 → 설계 17: 채우기(auto). 든 빵을 그 빵 진열대에 자리만큼 한 번에, 남은 건 계속 든다.
        // 빈 진열대는 그 빵 진열대 어디에도 자리가 없을 때만 쓴다(모든 진열대가 한 빵이 되지 않게)
        private sealed class Fill : InteractAction
        {
            public Fill(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is ShelfInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                BreadTable bread = worker.Hands.Bread;
                return bread != null && target is ShelfInteractable shelf && (shelf.HasRoomFor(bread) || shelf.Bread == null && !shelf.Bakery.HasRoomFor(bread));
            }

            public override void Do(Worker worker, Interactable target)
            {
                ShelfInteractable shelf = (ShelfInteractable)target;
                worker.Hands.Remove(shelf.Put(worker.Hands.Bread, worker.Hands.Count), shelf);
            }
        }

        // 설계 09 v0.4: 계산. auto면 행동이 아니라 계산대가 range 안에서 돌리는 타이머(CounterInteractable.Tick)라 CanDo가 늘 false,
        // manual이면 버튼 한 번에 서 있는 줄 머리 계산
        private sealed class Serve : InteractAction
        {
            public Serve(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is CounterInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return !Table.IsAuto && target is CounterInteractable counter && counter.HeadWaiting;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ((CounterInteractable)target).Serve();
            }
        }

        // 설계 09 → 설계 17: 굽기(시트 칩). 해금된 빵마다 한 칩(빈 오븐일 때만 고를 수 있음) + 다음 빵 해금 칩(값 = unlockCost, 사면 열리고 빈 오븐이면 바로 굽는다)
        private sealed class Bake : SheetAction
        {
            public Bake(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is OvenInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                OvenInteractable oven = (OvenInteractable)target;
                List<SheetOption> options = new List<SheetOption>();

                foreach (BreadTable bread in oven.Bakery.UnlockedBreads)
                {
                    options.Add(new SheetOption(bread.Id, oven.IsEmpty ? SheetOptionState.Enabled : SheetOptionState.Blocked));
                }

                BreadTable next = oven.Bakery.NextBread;

                if (next != null)
                {
                    options.Add(new SheetOption(next.Id, SheetOption.Afford(worker, next.UnlockCost), next.UnlockCost));
                }

                return options;
            }

            // 해금된 빵은 굽고, 다음 빵은 값을 치러 연다(BreadTable 행 순서대로만)
            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                OvenInteractable oven = (OvenInteractable)target;
                BreadTable next = oven.Bakery.NextBread;

                if (next != null && next.Id == option)
                {
                    if (!worker.Wallet.TrySpendCoins(next.UnlockCost))
                    {
                        return false;
                    }

                    oven.Bakery.UnlockBread(next);

                    if (oven.IsEmpty)
                    {
                        oven.TryStart(next);
                    }

                    return true;
                }

                foreach (BreadTable bread in oven.Bakery.UnlockedBreads)
                {
                    if (bread.Id == option)
                    {
                        return oven.TryStart(bread);
                    }
                }

                return false;
            }
        }

        // 굴 격자 설계 v0.5: 그 칸 파기(시트 줄). 비용 = digBaseCost × digCostGrowth^(판 칸 수)
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
                double cost = ((DigInteractable)target).Bakery.Grid.DigCost;
                return new[] { new SheetOption(null, SheetOption.Afford(worker, cost), cost) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                DigInteractable dig = (DigInteractable)target;
                BurrowGrid grid = dig.Bakery.Grid;

                if (!grid.CanDig(dig.Cell) || !worker.Wallet.TrySpendCoins(grid.DigCost))
                {
                    return false;
                }

                grid.Dig(dig.Cell);
                return true;
            }
        }
    }
}
