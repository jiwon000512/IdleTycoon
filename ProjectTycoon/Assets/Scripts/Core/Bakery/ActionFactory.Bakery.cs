using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.6: 빵집 행동(꺼내기·채우기·계산·굽기·진열대 놓기·오븐 설치·파기)
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

        // 설계 09 v0.4: 채우기(auto). 든 빵을 그 빵 진열대에 자리만큼 한 번에, 남은 건 계속 든다
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
                return target is ShelfInteractable shelf && worker.Hands.Bread == shelf.Bread && shelf.Stock < shelf.Capacity;
            }

            public override void Do(Worker worker, Interactable target)
            {
                ShelfInteractable shelf = (ShelfInteractable)target;
                worker.Hands.Remove(shelf.Put(worker.Hands.Count), shelf);
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

        // 설계 09: 굽기(시트 칩). 해금된 빵마다 한 줄(빈 오븐일 때만 고를 수 있음) + 다음 빵 잠김 줄
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

                if (oven.Bakery.NextBread != null)
                {
                    options.Add(new SheetOption(oven.Bakery.NextBread.Id, SheetOptionState.Locked));
                }

                return options;
            }

            // 해금된 빵만 굽는다
            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                OvenInteractable oven = (OvenInteractable)target;

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

        // 설계 08: 다음 빵 진열대 놓기(시트 줄). 빈 자리에서 산다. 진열대에서는 빈 자리가 없을 때 "먼저 굴을 넓혀요" 안내 줄(Blocked)
        private sealed class Unlock : SheetAction
        {
            public Unlock(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is SlotInteractable || target is ShelfInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                BakeryArea bakery = target is SlotInteractable slot ? slot.Bakery : ((ShelfInteractable)target).Bakery;
                BreadTable next = bakery.NextBread;
                List<SheetOption> options = new List<SheetOption>();

                if (next == null)
                {
                    return options;
                }

                if (target is SlotInteractable)
                {
                    options.Add(new SheetOption(next.Id, SheetOption.Afford(worker, next.UnlockCost), next.UnlockCost));
                }
                else if (bakery.Slots.Count == 0)
                {
                    options.Add(new SheetOption(next.Id, SheetOptionState.Blocked, next.UnlockCost));
                }

                return options;
            }

            // 빵은 BreadTable 행 순서대로만 해금한다(설계 08 결정 1)
            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                if (!(target is SlotInteractable slot))
                {
                    return false;
                }

                BreadTable next = slot.Bakery.NextBread;

                if (next == null || !slot.Bakery.IsEmptySlot(slot.Cell) || !worker.Wallet.TrySpendCoins(next.UnlockCost))
                {
                    return false;
                }

                slot.Bakery.PlaceShelf(next, slot.Cell);
                return true;
            }
        }

        // 설계 13 v0.6: 빈 자리에 오븐 설치(시트 줄). 업그레이드가 아니라 사는 것 — 가격은 빵집 설정, 오븐은 모두 ovenMax대까지
        private sealed class PlaceOven : SheetAction
        {
            public PlaceOven(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is SlotInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                BakeryArea bakery = ((SlotInteractable)target).Bakery;
                int ovens = bakery.Ovens.Count;
                bool full = ovens >= bakery.Config.OvenMax;
                double cost = Cost(bakery);
                SheetOptionState state = full ? SheetOptionState.Max : SheetOption.Afford(worker, cost);
                return new[] { new SheetOption(null, state, cost, 0, ovens, full ? ovens : ovens + 1) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                SlotInteractable slot = (SlotInteractable)target;
                BakeryArea bakery = slot.Bakery;

                if (!bakery.IsEmptySlot(slot.Cell) || bakery.Ovens.Count >= bakery.Config.OvenMax || !worker.Wallet.TrySpendCoins(Cost(bakery)))
                {
                    return false;
                }

                bakery.PlaceOven(slot.Cell);
                return true;
            }

            // 설치 가격 = ovenBaseCost × ovenCostGrowth^(시작 뒤 설치한 수)
            private static double Cost(BakeryArea bakery)
            {
                return bakery.Config.OvenBaseCost * Math.Pow(bakery.Config.OvenCostGrowth, bakery.Ovens.Count - BakeryArea.k_StartOvens);
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
