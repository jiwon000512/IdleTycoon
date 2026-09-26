using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 → 설계 13 → 설계 18: 빵집. 사물(진열대·오븐·계산대·나가기·파기)을 조립하고 사물 사이(오븐 → 웜뱃 손 → 진열대 → 손님 → 계산대)를 잇는다.
    // 설계 18: 진열대·오븐·계산대는 자유 배치(밑변 가운데 좌표). 사고 옮기고 보관하는 규칙은 WombatArea.Placement. 굴 파기는 칸 그대로.
    // 손님 동선 설계 v0.2: 매 프레임 돌고, 손님(BakeryVisitor)의 목록·비켜 걷기·서는 자리는 BakeryArea.Visitors.cs.
    // 설계 11: 손님은 광장에서 Admit으로 들어오고, 웜뱃은 구멍 앞 나가기로 광장에 간다(없는 동안 계산이 멈춘다)
    public sealed partial class BakeryArea : WombatArea
    {
        // 웜뱃이 끼인 자리(배치가 바뀜)에서 걷는 땅을 찾는 거리(맨해튼)
        private const float k_UnstuckDistance = 3f;

        private readonly BakeryConfigTable m_config;
        private readonly ZooState m_state;
        private readonly List<BreadTable> m_unlocked = new List<BreadTable>();
        private readonly List<ShelfInteractable> m_shelves = new List<ShelfInteractable>();
        private readonly List<OvenInteractable> m_ovens = new List<OvenInteractable>();
        private readonly List<CounterInteractable> m_counters = new List<CounterInteractable>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly Dictionary<Cell, DigInteractable> m_digs = new Dictionary<Cell, DigInteractable>();
        private readonly PassageInteractable m_exit;

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
        internal IRandom Random { get; }

        protected override BurrowNav WombatNav => Layout.WombatNav;
        protected override Vector2 Entrance => Layout.HoleFloor;
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
        public BakeryArea(ZooState state, TableSet tables, IRandom random, Wombat wombat, EventBus bus) : base(tables, wombat, bus)
        {
            m_config = tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery);
            m_state = state;
            Random = random;
            Grid = new BurrowGrid(m_config, bus);
            bus.Subscribe<Events.Dug>(Bus_Dug);
            Layout = new BakeryLayout(tables);
            InitClerks();
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor);

            foreach (InteractableTable row in tables.GetAll<InteractableTable>())
            {
                if (row.Price != null)
                {
                    m_shopKinds.Add(row);
                }
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

        // 편집 모드에서 누른 점의 팔 수 있는 흙 칸. 없으면 null
        public DigInteractable DigAt(Vector2 p)
        {
            foreach (DigInteractable dig in m_digs.Values)
            {
                if (Layout.DistanceToCell(dig.Cell, p) == 0f)
                {
                    return dig;
                }
            }

            return null;
        }

        // 설계 17: 굽기 시트가 값을 치른 뒤 다음 빵을 연다(BreadTable 행 순서)
        internal void UnlockBread(BreadTable bread)
        {
            m_unlocked.Add(bread);
        }

        protected override void TickArea(double dt)
        {
            TickVisitors(dt);
            TickClerks(dt);
        }

        protected override bool IsStaffed(Interactable thing)
        {
            return ClerkOf(thing) != null;
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
            SyncThings();
            RepathVisitors();
            RepathClerks();

            if (!WombatPresent)
            {
                return;
            }

            // 새 사물이 웜뱃 발밑에 놓이면 가까운 걷는 땅으로 비켜 선다
            BurrowNav nav = Layout.WombatNav;
            Vector2 wombat = Wombat.Mover.Position;

            if (!nav.IsWalkable(wombat) && nav.TryNearestFree(wombat, _ => false, k_UnstuckDistance, out Vector2 free))
            {
                Wombat.Mover.Place(free);
            }

            // 대상은 다음 Tick에 고른다. 여기서 고르면 LayoutChanged보다 TargetChanged가 먼저 나가 화면에 없는 사물(새 오븐·진열대)을 가리킨다
        }

        // 팔 수 있는 칸은 칸 기준으로 맞춘다(있던 칸은 같은 객체를 둬 대상이 흔들리지 않는다). 목록 순서는 딴짓 점원 → 진열대 → 오븐 → 계산대 → 나가기 → 파기
        private void SyncThings()
        {
            List<Cell> digCells = new List<Cell>(Grid.Frontier());

            foreach (Cell cell in new List<Cell>(m_digs.Keys))
            {
                if (!digCells.Contains(cell))
                {
                    m_digs.Remove(cell);
                }
            }

            foreach (Cell cell in digCells)
            {
                if (!m_digs.ContainsKey(cell))
                {
                    m_digs[cell] = new DigInteractable(Row(DigInteractable.k_Id), cell, this);
                }
            }

            Placed.Clear();

            // 설계 22: 딴짓 중인 점원이 먼저 — 자리에 선 점원은 그 사물과 거리가 같아 앞에 있어야 대상이 된다(같은 거리면 먼저 것)
            foreach (ClerkInteractable clerk in m_clerkThings.Values)
            {
                Placed.Add(clerk);
            }

            Placed.AddRange(m_shelves);
            Placed.AddRange(m_ovens);
            Placed.AddRange(m_counters);
            Placed.Add(m_exit);

            foreach (Cell cell in digCells)
            {
                Placed.Add(m_digs[cell]);
            }
        }

        private void OnLayoutChanged()
        {
            Bus.Publish(new Events.LayoutChanged(this));
        }
    }
}
