using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 13 v0.6 → 설계 17: 빵집 행동(꺼내기·채우기·계산·굽기(해금 포함)·진열대 설치·오븐 설치·파기)
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

        // 설계 17: 빈 자리에 진열대 설치(시트 줄). 가격은 빵집 설정, 진열대는 모두 shelfMax대까지. 새 진열대는 비어 있어 아무 빵이나 받는다
        private sealed class PlaceShelf : SheetAction
        {
            public PlaceShelf(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is SlotInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                BakeryArea bakery = ((SlotInteractable)target).Bakery;
                int shelves = bakery.Shelves.Count;
                bool full = shelves >= bakery.Config.ShelfMax;
                double cost = Cost(bakery);
                SheetOptionState state = full ? SheetOptionState.Max : SheetOption.Afford(worker, cost);
                return new[] { new SheetOption(null, state, cost, 0, shelves, full ? shelves : shelves + 1) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                SlotInteractable slot = (SlotInteractable)target;
                BakeryArea bakery = slot.Bakery;

                if (!bakery.IsEmptySlot(slot.Cell) || bakery.Shelves.Count >= bakery.Config.ShelfMax || !worker.Wallet.TrySpendCoins(Cost(bakery)))
                {
                    return false;
                }

                bakery.PlaceShelf(slot.Cell);
                return true;
            }

            // 설치 가격 = shelfBaseCost × shelfCostGrowth^(시작 뒤 설치한 수)
            private static double Cost(BakeryArea bakery)
            {
                return bakery.Config.ShelfBaseCost * Math.Pow(bakery.Config.ShelfCostGrowth, bakery.Shelves.Count - BakeryArea.k_StartShelves);
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
