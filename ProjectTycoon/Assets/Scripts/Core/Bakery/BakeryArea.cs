using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 → 설계 13 → 설계 18: 빵집. 사물(진열대·오븐·계산대·나가기·파기)을 조립하고 사물 사이(오븐 → 웜뱃 손 → 진열대 → 손님 → 계산대)를 잇는다.
    // 설계 18: 진열대·오븐·계산대는 자유 배치(밑변 가운데 좌표). 사고 옮기고 보관하는 규칙은 WombatArea.Placement. 굴 파기는 칸 그대로.
    // 손님 동선 설계 v0.2: 매 프레임 돌고, 손님(BakeryVisitor)의 목록·비켜 걷기·서는 자리는 BakeryArea.Visitors.cs. 설계 24 · 37 웜뱃 똥은 곳 공용 WombatArea.Poops.cs(손님 땅 둘레만 여기).
    // 설계 11: 손님은 광장에서 Admit으로 들어오고, 웜뱃은 구멍 앞 나가기로 광장에 간다(없는 동안 계산이 멈춘다)
    public sealed partial class BakeryArea : WombatArea
    {
        private readonly BakeryConfigTable m_config;
        private readonly ZooState m_state;
        private readonly List<BreadTable> m_unlocked = new List<BreadTable>();
        private readonly List<ShelfInteractable> m_shelves = new List<ShelfInteractable>();
        private readonly List<OvenInteractable> m_ovens = new List<OvenInteractable>();
        private readonly List<CounterInteractable> m_counters = new List<CounterInteractable>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly DigSet m_digs;
        private readonly PassageInteractable m_exit;

        public const string k_Id = "bakery";

        public BakeryConfigTable Config => m_config;
        public BurrowGrid Grid { get; }
        public BakeryLayout Layout { get; }
        public IReadOnlyList<BreadTable> UnlockedBreads => m_unlocked;
        public IReadOnlyList<ShelfInteractable> Shelves => m_shelves;
        public IReadOnlyList<OvenInteractable> Ovens => m_ovens;
        public IReadOnlyList<CounterInteractable> Counters => m_counters;
        // 첫 계산대(웜뱃이 시작하는 곳)
        public CounterInteractable Counter => m_counters[0];
        public BreadTable NextBread => m_unlocked.Count < Tables.GetAll<BreadTable>().Count ? Tables.GetAll<BreadTable>()[m_unlocked.Count] : null;
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;
        public override string Id => k_Id;
        // 설계 25: 오븐이 재료를 꺼내는 창고
        public ZooState Wallet => m_state;

        protected override BurrowNav WombatNav => Layout.WombatNav;
        protected override Vector2 Entrance => Layout.HoleFloor;

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }
        protected override BurrowShape.Result Shape => Layout.Shape;

        protected override IEnumerable<IPlaced> PlacedThings
        {
            get
            {
                foreach (ShelfInteractable shelf in m_shelves)
                {
                    yield return shelf;
                }

                foreach (OvenInteractable oven in m_ovens)
                {
                    yield return oven;
                }

                foreach (CounterInteractable counter in m_counters)
                {
                    yield return counter;
                }
            }
        }

        // 첫 손님은 광장에서 온다. 웜뱃은 계산대 뒤에서 시작한다
        public BakeryArea(ZooState state, TableSet tables, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            m_config = tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery);
            m_state = state;
            Grid = new BurrowGrid(BurrowGrid.Columns(2, 4), m_config.DigBaseCost, m_config.DigCostGrowth, CellBounds.None, bus);
            bus.Subscribe<Events.Dug>(Bus_Dug);
            Layout = new BakeryLayout(tables);
            m_digs = new DigSet(Row(DigInteractable.k_Id), Grid, Layout.Cells, this);
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);

            // 설계 25: 밭도 price가 있어 빵집이 파는 종류를 적는다(Create와 같은 셋)
            foreach (string id in new[] { ShelfInteractable.k_Id, OvenInteractable.k_Id, CounterInteractable.k_Id })
            {
                m_shopKinds.Add(Row(id));
            }

            // 시작 배치(옛 칸 자리 그대로): 왼쪽 열(−1) 자리 줄에 빈 진열대와 오븐, 계산대 줄 가운데에 계산대. 첫 빵은 해금된 채 시작(설계 17)
            m_unlocked.Add(tables.GetAll<BreadTable>()[0]);
            Create(Row(ShelfInteractable.k_Id), Layout.ShelfBase(new Cell(-1, 1)));
            Create(Row(OvenInteractable.k_Id), Layout.OvenBase(new Cell(-1, 3)));
            Create(Row(CounterInteractable.k_Id), Layout.CounterBase);
            RebuildLayout();
            EnterAt(Layout.WombatHome);
        }

        // 설계 17: 그 빵 재고가 있는 가장 가까운 진열대. 없으면 가장 가까운 진열대(손님은 거기서 기다린다)
        public ShelfInteractable ShelfFor(BreadTable bread, Vector2 from)
        {
            ShelfInteractable best = null;
            bool bestHas = false;
            float bestDistance = float.MaxValue;

            foreach (ShelfInteractable shelf in m_shelves)
            {
                bool has = shelf.Bread == bread && shelf.Stock > 0;
                float distance = shelf.DistanceTo(from);

                if (has && !bestHas || has == bestHas && distance < bestDistance)
                {
                    best = shelf;
                    bestHas = has;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // 그 빵의 재고 합(굽기 시트 칩)
        public int StockOf(BreadTable bread)
        {
            int stock = 0;

            foreach (ShelfInteractable shelf in m_shelves)
            {
                if (shelf.Bread == bread)
                {
                    stock += shelf.Stock;
                }
            }

            return stock;
        }

        // 그 빵을 진열 중인 진열대 어딘가에 자리가 남았다(채우기가 빈 진열대를 쓸지 정한다)
        public bool HasRoomFor(BreadTable bread)
        {
            foreach (ShelfInteractable shelf in m_shelves)
            {
                if (shelf.HasRoomFor(bread))
                {
                    return true;
                }
            }

            return false;
        }

        // 설계 18: 가장 짧은 줄, 같으면 가까운 계산대
        public CounterInteractable ShortestQueue(Vector2 from)
        {
            CounterInteractable best = null;

            foreach (CounterInteractable counter in m_counters)
            {
                if (best == null || counter.Queue.Count < best.Queue.Count
                    || counter.Queue.Count == best.Queue.Count && counter.DistanceTo(from) < best.DistanceTo(from))
                {
                    best = counter;
                }
            }

            return best;
        }

        public override DigInteractable DigAt(Vector2 p)
        {
            return m_digs.At(p);
        }

        // 설계 17: 굽기 시트가 값을 치른 뒤 다음 빵을 연다(BreadTable 행 순서)
        internal void UnlockBread(BreadTable bread)
        {
            m_unlocked.Add(bread);
        }

        protected override void TickArea(double dt)
        {
            TickVisitors(dt);
        }

        // 설계 21: 점원 자리는 오븐 → 계산대
        public override IEnumerable<Interactable> ClerkSlots
        {
            get
            {
                foreach (OvenInteractable oven in m_ovens)
                {
                    yield return oven;
                }

                foreach (CounterInteractable counter in m_counters)
                {
                    yield return counter;
                }
            }
        }

        protected override void OnClerkThingsChanged()
        {
            SyncThings();
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
                case ShelfInteractable.k_Id:
                {
                    ShelfInteractable shelf = new ShelfInteractable(table, at, this);
                    m_shelves.Add(shelf);
                    return shelf;
                }
                case OvenInteractable.k_Id:
                {
                    OvenInteractable oven = new OvenInteractable(table, at, this);
                    m_ovens.Add(oven);
                    return oven;
                }
                case CounterInteractable.k_Id:
                {
                    CounterInteractable counter = new CounterInteractable(table, at, this, m_state);
                    m_counters.Add(counter);
                    return counter;
                }
                default:
                    throw new ArgumentException($"빵집에 놓을 수 없는 종류 '{kind.Id}'.");
            }
        }

        // 보관하면 붙어 있던 점원은 그만둔다(설계 21)
        protected override void Destroy(IPlaced thing)
        {
            Clerk clerk = ClerkOf((Interactable)thing);

            if (clerk != null)
            {
                Fire(clerk, FireReason.Stored);
            }

            switch (thing)
            {
                case ShelfInteractable shelf:
                    m_shelves.Remove(shelf);
                    break;
                case OvenInteractable oven:
                    m_ovens.Remove(oven);
                    break;
                case CounterInteractable counter:
                    m_counters.Remove(counter);
                    break;
            }
        }

        // 계산대는 하나는 남아야 하고, 줄이 선 계산대는 치울 수 없다
        protected override bool CanRemove(IPlaced thing)
        {
            return !(thing is CounterInteractable counter) || m_counters.Count > 1 && counter.Queue.Count == 0;
        }

        protected override void OnPlacementChanged()
        {
            RebuildLayout();
            OnLayoutChanged();
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
            OnLayoutChanged();
        }

        // 배치가 바뀌면 걷는 땅·줄 자리·서는 자리·사물 목록을 다시 맞추고, 걷는 중인 손님·웜뱃은 새 땅에서 길을 다시 찾는다
        private void RebuildLayout()
        {
            Layout.Rebuild(Grid.Cells, m_shelves, m_ovens, m_counters, m_config.MaxCustomers);
            ClearBuriedPoops();
            ApplyPoopObstacles(Layout.Nav);
            SyncThings();
            RepathVisitors();
            RepathClerks();
            Unstick();

            // 대상은 다음 Tick에 고른다. 여기서 고르면 LayoutChanged보다 TargetChanged가 먼저 나가 화면에 없는 사물(새 오븐·진열대)을 가리킨다
        }

        // 목록 순서는 딴짓 점원 → 똥 → 진열대 → 오븐 → 계산대 → 나가기 → 파기(팔 수 있는 칸은 DigSet이 칸 기준으로 맞춘다)
        private void SyncThings()
        {
            m_digs.Sync();
            Placed.Clear();

            // 설계 22: 딴짓 중인 점원이 먼저 — 자리에 선 점원은 그 사물과 거리가 같아 앞에 있어야 대상이 된다(같은 거리면 먼저 것)
            Placed.AddRange(ClerkThings);

            Placed.AddRange(Poops);
            Placed.AddRange(m_shelves);
            Placed.AddRange(m_ovens);
            Placed.AddRange(m_counters);
            Placed.Add(m_exit);
            Placed.AddRange(m_digs.Things);
        }

        // 설계 24: 똥은 사물 목록에 들고, 손님 땅에 둘레를 걸며, 걷는 손님은 새 길을 찾는다(길이 없으면 포기)
        protected override void OnPoopsChanged()
        {
            SyncThings();
            ApplyPoopObstacles(Layout.Nav);
            RepathVisitors();
        }

        private void OnLayoutChanged()
        {
            Bus.Publish(new Events.LayoutChanged(this));
        }
    }
}
