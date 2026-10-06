namespace ZooTycoon.Core
{
    // 설계 47: 횟집 사건
    public static partial class Events
    {
        // 광장 문 앞에서 값을 치러 횟집을 열었다
        public readonly struct ShopOpened
        {
            public readonly WombatArea Shop;

            public ShopOpened(WombatArea shop)
            {
                Shop = shop;
            }
        }

        public readonly struct RestaurantVisitorArrived
        {
            public readonly RestaurantVisitor Visitor;

            public RestaurantVisitorArrived(RestaurantVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 구멍으로 나갔다(먹고 냈거나 화나서)
        public readonly struct RestaurantVisitorLeft
        {
            public readonly RestaurantVisitor Visitor;

            public RestaurantVisitorLeft(RestaurantVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 수조에서 물고기를 골라 주문했다(머리 위 말풍선에 그 물고기)
        public readonly struct DishOrdered
        {
            public readonly RestaurantVisitor Visitor;

            public DishOrdered(RestaurantVisitor visitor)
            {
                Visitor = visitor;
            }
        }

        // 웜뱃이 수조에 창고 물고기를 담았다
        public readonly struct TankFilled
        {
            public readonly TankInteractable Tank;
            public readonly int Count;

            public TankFilled(TankInteractable tank, int count)
            {
                Tank = tank;
                Count = count;
            }
        }

        // 웜뱃이 수조에서 손님이 잡아 둔 물고기를 꺼냈다
        public readonly struct FishTaken
        {
            public readonly TankInteractable Tank;
            public readonly RestaurantVisitor Order;

            public FishTaken(TankInteractable tank, RestaurantVisitor order)
            {
                Tank = tank;
                Order = order;
            }
        }

        // 웜뱃이 든 물고기를 도마에 놓았다
        public readonly struct FishPlaced
        {
            public readonly CuttingBoardInteractable Board;
            public readonly RestaurantVisitor Order;

            public FishPlaced(CuttingBoardInteractable board, RestaurantVisitor order)
            {
                Board = board;
                Order = order;
            }
        }

        // 도마에서 다 떠 접시가 웜뱃 손에
        public readonly struct DishCut
        {
            public readonly CuttingBoardInteractable Board;
            public readonly RestaurantVisitor Order;

            public DishCut(CuttingBoardInteractable board, RestaurantVisitor order)
            {
                Board = board;
                Order = order;
            }
        }

        // 탁자에 접시를 놓았다(손님이 먹기 시작)
        public readonly struct DishServed
        {
            public readonly DiningTableInteractable Table;
            public readonly RestaurantVisitor Order;

            public DishServed(DiningTableInteractable table, RestaurantVisitor order)
            {
                Table = table;
                Order = order;
            }
        }

        // 다 먹고 탁자에서 값을 냈다
        public readonly struct RestaurantVisitorPaid
        {
            public readonly RestaurantVisitor Visitor;
            public readonly double Coins;

            public RestaurantVisitorPaid(RestaurantVisitor visitor, double coins)
            {
                Visitor = visitor;
                Coins = coins;
            }
        }
    }
}
