using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 작은 탁자(자유 배치, 밑변 가운데). 표의 customer 자리가 앉는 자리이고 누가 앉았는지는 손님이 안다(RestaurantVisitor.Table · Seat).
    // 웜뱃이 그 손님 몫 접시를 들고 곁에 오면 놓는다(serve_dish)
    public sealed class DiningTableInteractable : Interactable, IPlaced
    {
        public const string k_Id = "dining_table";

        public RestaurantArea Restaurant { get; }
        public Vector2 Position { get; private set; }
        public IPlacedKind Kind => Table;

        public DiningTableInteractable(InteractableTable table, Vector2 position, RestaurantArea restaurant) : base(table, restaurant)
        {
            Restaurant = restaurant;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, Position);
        }

        public void MoveTo(Vector2 position)
        {
            Position = position;
        }

        // 접시를 그 손님 앞에 놓는다(먹기 시작). 다른 탁자 손님 몫이면 false
        internal bool Serve(RestaurantVisitor order)
        {
            if (order.Table != this || order.Served)
            {
                return false;
            }

            order.Serve();
            OnChanged();
            Restaurant.Bus.Publish(new Events.DishServed(this, order));
            return true;
        }
    }
}
