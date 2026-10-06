using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 설계 47: 횟집 행동(수조 채우기 · 물고기 꺼내기 · 도마에 놓기 · 접시 놓기 · 횟집 열기). 회 뜨기는 도마가 자리에서 돌리는 타이머(CuttingBoardInteractable.Tick)
    public static partial class ActionFactory
    {
        // 채우기(auto): 수조 곁에 서면 창고 물고기를 자리만큼(가장 많은 종류부터)
        private sealed class FillTank : InteractAction
        {
            public FillTank(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is TankInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is TankInteractable tank && tank.CanFill(worker.Wallet);
            }

            public override void Do(Worker worker, Interactable target)
            {
                TankInteractable tank = (TankInteractable)target;
                int count = tank.Fill(worker.Wallet);
                tank.Area.Bus.Publish(new Events.TankFilled(tank, count));
            }
        }

        // 꺼내기(auto): 빈손으로 수조 곁에 서면 손님이 잡아 둔 물고기 하나를 손에(주문 순서)
        private sealed class TakeFish : InteractAction
        {
            public TakeFish(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is TankInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is TankInteractable tank && worker.Hands.Empty && tank.Restaurant.NextFetch(tank) != null;
            }

            public override void Do(Worker worker, Interactable target)
            {
                TankInteractable tank = (TankInteractable)target;
                RestaurantVisitor order = tank.Restaurant.NextFetch(tank);
                tank.TakeReserved(order.Dish);
                order.FishTaken = true;
                worker.Hands.HoldOrder(order, tank);
                tank.Area.Bus.Publish(new Events.FishTaken(tank, order));
            }
        }

        // 도마에 놓기(auto): 든 날 물고기를 빈 도마에(뜨기는 도마 자리에서)
        private sealed class PlaceFish : InteractAction
        {
            public PlaceFish(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is CuttingBoardInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return target is CuttingBoardInteractable board && board.OnBoard == null && worker.Hands.Order is RestaurantVisitor order && !order.Cut;
            }

            public override void Do(Worker worker, Interactable target)
            {
                CuttingBoardInteractable board = (CuttingBoardInteractable)target;
                board.Place(worker.Hands.Order);
                worker.Hands.DropOrder(board);
            }
        }

        // 놓기(auto): 든 접시가 이 탁자 손님 몫이면 그 손님 앞에
        private sealed class ServeDish : InteractAction
        {
            public ServeDish(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is DiningTableInteractable;
            }

            public override bool CanDo(Worker worker, Interactable target)
            {
                return worker.Hands.Order is RestaurantVisitor order && order.Cut && order.Table == target && !order.Served;
            }

            public override void Do(Worker worker, Interactable target)
            {
                DiningTableInteractable table = (DiningTableInteractable)target;

                if (table.Serve(worker.Hands.Order))
                {
                    worker.Hands.DropOrder(table);
                }
            }
        }

        // 횟집 열기(시트 줄): 광장 문 앞에서 빵집 별이 openStar 이상이면 openCost로(모자라면 「★n 필요」)
        private sealed class OpenShop : SheetAction
        {
            public OpenShop(ActionTable table) : base(table)
            {
            }

            public override bool Accepts(Interactable target)
            {
                return target is ShopGateInteractable;
            }

            public override IReadOnlyList<SheetOption> Options(Worker worker, Interactable target)
            {
                RestaurantArea shop = ((ShopGateInteractable)target).Shop;
                double cost = shop.Config.OpenCost;
                return new[] { new SheetOption(null, shop.StarLocked ? SheetOptionState.Locked : SheetOption.Afford(worker, cost), cost) };
            }

            public override bool TryChoose(Worker worker, Interactable target, string option)
            {
                return ((ShopGateInteractable)target).Shop.TryOpen(worker);
            }
        }
    }
}
