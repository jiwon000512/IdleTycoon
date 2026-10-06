using System.Collections.Generic;
using System.Numerics;

namespace ZooTycoon.Core
{
    // 설계 47: 둥근 도마(자유 배치, 여러 대). 기준점은 도마 뒤 웜뱃 자리(표의 worker 자리). 수조에서 고른 손님이 주문 줄에 들고,
    // 웜뱃이 수조에서 꺼내 온 물고기를 도마에 놓으면(place_fish) 웜뱃이 자리에 붙어 있고 손이 빈 동안 그 회를 뜬다(계산대처럼, 곁에서 조이스틱을 놓으면 자리까지 걷는다).
    // 다 뜨면 접시가 바로 웜뱃 손에. 속도는 업그레이드
    public sealed class CuttingBoardInteractable : Interactable, IPlaced
    {
        public const string k_Id = "cutting_board";
        private const float k_AtSpot = 0.05f;

        private readonly List<RestaurantVisitor> m_orders = new List<RestaurantVisitor>();
        private double m_remaining;

        public RestaurantArea Restaurant { get; }
        public Vector2 Position { get; private set; }
        public IPlacedKind Kind => Table;
        // 고른 순서의 주문(손님). 머리부터 뜬다
        public IReadOnlyList<RestaurantVisitor> Orders => m_orders;
        // 도마에 놓인 물고기(그 손님 몫). 웜뱃이 수조에서 가져와 놓는다
        public RestaurantVisitor OnBoard { get; private set; }
        // 놓인 물고기를 뜨기 시작했다(웜뱃이 자리를 비우면 멈춘 채 남는다). 화면이 게이지를 보인다
        public double Progress => OnBoard != null ? 1d - m_remaining / OnBoard.Dish.CutSeconds : 0d;
        public bool Cutting => Progress > 0d;
        public Vector2 WorkerSpot => Placement.SpotOf(this, SpotRole.Worker);

        public CuttingBoardInteractable(InteractableTable table, Vector2 position, RestaurantArea restaurant) : base(table, restaurant)
        {
            Restaurant = restaurant;
            Position = position;
        }

        public override float DistanceTo(Vector2 p)
        {
            return Vector2.Distance(p, WorkerSpot);
        }

        public void MoveTo(Vector2 position)
        {
            Position = position;
        }

        public bool WombatAtSpot => Vector2.Distance(Area.Wombat.Mover.Position, WorkerSpot) <= k_AtSpot;

        public override void Tick(double dt)
        {
            if (OnBoard == null || !Area.IsInRange(this))
            {
                return;
            }

            Wombat wombat = Area.Wombat;

            if (!WombatAtSpot)
            {
                if (Area.Target == this && !wombat.Guided && wombat.Input == Vector2.Zero)
                {
                    wombat.Guide(Restaurant.Layout.WombatNav, WorkerSpot, Facing.Down);
                }

                return;
            }

            if (!wombat.Worker.Hands.Empty)
            {
                return;
            }

            m_remaining -= dt * UpgradeValue(UpgradeLevel);

            if (m_remaining <= 0d)
            {
                Finish(wombat.Worker);
            }
        }

        internal void Add(RestaurantVisitor order)
        {
            m_orders.Add(order);
            OnChanged();
        }

        // 웜뱃이 든 물고기를 놓는다. 다른 도마 줄의 주문이면 이 도마 줄 머리로 옮긴다
        internal void Place(RestaurantVisitor order)
        {
            foreach (CuttingBoardInteractable board in Restaurant.Boards)
            {
                board.m_orders.Remove(order);
            }

            m_orders.Insert(0, order);
            OnBoard = order;
            m_remaining = order.Dish.CutSeconds;
            Restaurant.Bus.Publish(new Events.FishPlaced(this, order));
            OnChanged();
        }

        // 설계 24: 똥에 막혀 포기한 손님의 주문을 뺀다(놓인 물고기도 버린다)
        internal void Remove(RestaurantVisitor order)
        {
            if (OnBoard == order)
            {
                OnBoard = null;
            }

            if (m_orders.Remove(order))
            {
                OnChanged();
            }
        }

        private void Finish(Worker worker)
        {
            RestaurantVisitor order = OnBoard;
            m_orders.Remove(order);
            OnBoard = null;
            order.Cut = true;
            worker.Hands.HoldOrder(order, this);
            Restaurant.Bus.Publish(new Events.DishCut(this, order));
            OnChanged();
        }
    }
}
