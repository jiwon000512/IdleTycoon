using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 47: 횟집. 사물(수조 · 도마 · 탁자 · 나가기 · 파기)을 조립하고 사물 사이(창고 → 수조 → 손님 → 도마 → 웜뱃 손 → 탁자)를 잇는다.
    // 배치 · 파기 · 똥은 빵집과 같은 공용 규칙. 문은 빵집 별 openStar부터 광장 문 앞에서 openCost로 연다(닫힌 동안 손님이 오지 않고 웜뱃도 못 들어간다).
    // 손님 목록 · 서는 자리 · 탁자 자리는 RestaurantArea.Visitors.cs
    public sealed partial class RestaurantArea : WombatArea, IShop
    {
        public const string k_Id = "restaurant";

        private readonly List<TankInteractable> m_tanks = new List<TankInteractable>();
        private readonly List<CuttingBoardInteractable> m_boards = new List<CuttingBoardInteractable>();
        private readonly List<DiningTableInteractable> m_tables = new List<DiningTableInteractable>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly DigSet m_digs;
        private readonly PassageInteractable m_exit;

        public RestaurantConfigTable Config { get; }
        public ZooState Wallet { get; }
        public BurrowGrid Grid { get; }
        public RestaurantLayout Layout { get; }
        public IReadOnlyList<DishTable> Dishes => Tables.GetAll<DishTable>();
        public IReadOnlyList<TankInteractable> Tanks => m_tanks;
        public IReadOnlyList<CuttingBoardInteractable> Boards => m_boards;
        public IReadOnlyList<DiningTableInteractable> DiningTables => m_tables;
        public bool IsOpen { get; private set; }
        // 빵집 별이 모자라 아직 열 수 없다(문 앞 「★n 필요」)
        public bool StarLocked => Wallet.Stars.Count(BakeryArea.k_Id) < Config.OpenStar;
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;
        public override string Id => k_Id;

        protected override BurrowNav WombatNav => Layout.WombatNav;
        protected override Vector2 Entrance => Layout.HoleFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }

        protected override IEnumerable<IPlaced> PlacedThings
        {
            get
            {
                foreach (TankInteractable tank in m_tanks)
                {
                    yield return tank;
                }

                foreach (CuttingBoardInteractable board in m_boards)
                {
                    yield return board;
                }

                foreach (DiningTableInteractable table in m_tables)
                {
                    yield return table;
                }
            }
        }

        public RestaurantArea(ZooState state, TableSet tables, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            Config = tables.Get<RestaurantConfigTable>(RestaurantConfigTable.k_Main);
            Wallet = state;
            Grid = new BurrowGrid(BurrowGrid.Columns(2, 4), Config.DigBaseCost, Config.DigCostGrowth, CellBounds.None, bus);
            bus.Subscribe<Events.Dug>(Bus_Dug);
            Layout = new RestaurantLayout(tables);
            m_digs = new DigSet(Row(DigInteractable.k_Id), Grid, Layout.Cells, this);
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);

            foreach (string id in new[] { TankInteractable.k_Id, CuttingBoardInteractable.k_Id, DiningTableInteractable.k_Id })
            {
                m_shopKinds.Add(Row(id));
            }

            foreach (StartThingData start in Config.Start)
            {
                Create(Row(start.Kind), new Vector2((float)start.X, (float)start.Y));
            }

            RebuildLayout();
        }

        // 광장 문 앞 시트: 빵집 별이 닿았으면 값을 치르고 연다
        internal bool TryOpen(Worker worker)
        {
            if (IsOpen || StarLocked || !worker.Wallet.TrySpendCoins(Config.OpenCost))
            {
                return false;
            }

            IsOpen = true;
            Bus.Publish(new Events.ShopOpened(this));
            return true;
        }

        // 설계 43: 저장에서 열린 문(값 · 사건 없이)
        internal void RestoreOpen()
        {
            IsOpen = true;
        }

        // 물고기가 있는 가장 가까운 수조. 없으면 가장 가까운 수조(거기서 기다린다)
        public TankInteractable TankFor(Vector2 from)
        {
            TankInteractable best = null;
            bool bestHas = false;
            float bestDistance = float.MaxValue;

            foreach (TankInteractable tank in m_tanks)
            {
                bool has = tank.Stock > 0;
                float distance = tank.DistanceTo(from);

                if (has && !bestHas || has == bestHas && distance < bestDistance)
                {
                    best = tank;
                    bestHas = has;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // 주문이 가장 적은 도마, 같으면 가까운 도마
        public CuttingBoardInteractable ShortestBoard(Vector2 from)
        {
            CuttingBoardInteractable best = null;

            foreach (CuttingBoardInteractable board in m_boards)
            {
                if (best == null || board.Orders.Count < best.Orders.Count
                    || board.Orders.Count == best.Orders.Count && board.DistanceTo(from) < best.DistanceTo(from))
                {
                    best = board;
                }
            }

            return best;
        }

        public override DigInteractable DigAt(Vector2 p)
        {
            return m_digs.At(p);
        }

        // 다 먹은 손님이 탁자에서 값을 낸다(황금 축복 · 행운 동전, 반올림)
        internal void Pay(RestaurantVisitor visitor)
        {
            double price = Math.Round(visitor.Dish.Price * Wallet.Scale(BlessingTable.k_Price));
            Wallet.AddCoins(price);
            Bus.Publish(new Events.RestaurantVisitorPaid(visitor, price));
        }

        // 그 수조에서 웜뱃이 다음에 꺼낼 물고기(도마 순서 · 주문 순서로 아직 꺼내지 않은 첫 주문). 없으면 null
        internal RestaurantVisitor NextFetch(TankInteractable tank)
        {
            foreach (CuttingBoardInteractable board in m_boards)
            {
                foreach (RestaurantVisitor order in board.Orders)
                {
                    if (order.Tank == tank && !order.FishTaken)
                    {
                        return order;
                    }
                }
            }

            return null;
        }

        // 화나서 나가는 손님: 도마 줄에서 빼고, 아직 수조에 있으면 다시 고를 수 있게, 웜뱃이 든 그 손님 몫(물고기 · 접시)은 사라진다
        internal void Cancel(RestaurantVisitor visitor)
        {
            foreach (CuttingBoardInteractable board in m_boards)
            {
                board.Remove(visitor);
            }

            if (visitor.Dish != null && !visitor.FishTaken && m_tanks.Contains(visitor.Tank))
            {
                visitor.Tank.Unreserve(visitor.Dish);
            }

            if (Wombat.Worker.Hands.Order == visitor)
            {
                Wombat.Worker.Hands.DropOrder(null);
            }
        }

        protected override void TickArea(double dt)
        {
            TickVisitors(dt);
        }

        protected override IPlacedKind KindOf(string kindId)
        {
            return Row(kindId);
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            InteractableTable table = (InteractableTable)kind;

            switch (kind.Id)
            {
                case TankInteractable.k_Id:
                {
                    TankInteractable tank = new TankInteractable(table, at, this);
                    m_tanks.Add(tank);
                    return tank;
                }
                case CuttingBoardInteractable.k_Id:
                {
                    CuttingBoardInteractable board = new CuttingBoardInteractable(table, at, this);
                    m_boards.Add(board);
                    return board;
                }
                case DiningTableInteractable.k_Id:
                {
                    DiningTableInteractable dining = new DiningTableInteractable(table, at, this);
                    m_tables.Add(dining);
                    return dining;
                }
                default:
                    throw new ArgumentException($"횟집에 놓을 수 없는 종류 '{kind.Id}'.");
            }
        }

        protected override void Destroy(IPlaced thing)
        {
            switch (thing)
            {
                case TankInteractable tank:
                    m_tanks.Remove(tank);
                    break;
                case CuttingBoardInteractable board:
                    m_boards.Remove(board);
                    break;
                case DiningTableInteractable table:
                    m_tables.Remove(table);
                    break;
            }
        }

        // 도마와 수조는 하나씩 남아야 하고, 주문이 걸린 도마 · 손님이 물고기를 잡아 둔 수조 · 손님이 잡은 탁자는 치울 수 없다
        protected override bool CanRemove(IPlaced thing)
        {
            switch (thing)
            {
                case CuttingBoardInteractable board:
                    return m_boards.Count > 1 && board.Orders.Count == 0;
                case TankInteractable tank:
                    return m_tanks.Count > 1 && tank.Reserved == 0;
                case DiningTableInteractable table:
                    return !Seated(table);
                default:
                    return true;
            }
        }

        protected override void OnPlacementChanged()
        {
            RebuildLayout();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        protected override void OnPoopsChanged()
        {
            SyncThings();
            ApplyPoopObstacles(Layout.Nav);
            RepathVisitors();
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }

        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != Grid)
            {
                return;
            }

            RebuildLayout();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        private void RebuildLayout()
        {
            Layout.Rebuild(Grid.Cells, m_tanks, m_boards, m_tables);
            ClearBuriedPoops();
            ApplyPoopObstacles(Layout.Nav);
            SyncThings();
            RepathVisitors();
            Unstick();
        }

        // 목록 순서는 똥 → 수조 → 도마 → 탁자 → 나가기 → 파기
        private void SyncThings()
        {
            m_digs.Sync();
            Placed.Clear();
            Placed.AddRange(Poops);
            Placed.AddRange(m_tanks);
            Placed.AddRange(m_boards);
            Placed.AddRange(m_tables);
            Placed.Add(m_exit);
            Placed.AddRange(m_digs.Things);
        }
    }
}
