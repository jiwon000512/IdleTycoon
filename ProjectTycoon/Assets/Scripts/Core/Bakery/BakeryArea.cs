using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 08 v0.5 → 설계 13: 빵집. 사물(진열대·오븐·계산대·나가기·빈 자리·파기)을 조립하고 사물 사이(오븐 → 웜뱃 손 → 진열대 → 손님 → 계산)를 잇는다.
    // 굴 격자 설계 v0.5: 진열대·오븐은 굴 칸의 자리에 놓인다. 손님 동선 설계 v0.2: 매 프레임 돌고, 손님(행동 트리, BakeryArea.Customers.cs)의 위치도 여기서 정한다.
    // 설계 11: 손님은 광장에서 Admit으로 들어오고, 웜뱃은 구멍 앞 나가기로 광장에 간다(없는 동안 계산이 멈춘다)
    public sealed partial class BakeryArea : WombatArea
    {
        // 웜뱃이 끼인 자리(배치가 바뀜)에서 걷는 땅을 찾는 거리(맨해튼)
        private const float k_UnstuckDistance = 3f;

        private readonly ZooState m_state;
        private readonly BakeryConfigTable m_config;
        private readonly double m_walkSpeed;
        private readonly double m_hopSeconds;
        private readonly IRandom m_random;
        private readonly List<BreadTable> m_unlocked = new List<BreadTable>();
        private readonly Dictionary<Cell, ShelfInteractable> m_shelves = new Dictionary<Cell, ShelfInteractable>();
        private readonly List<OvenInteractable> m_ovens = new List<OvenInteractable>();
        private readonly Dictionary<Cell, SlotInteractable> m_slots = new Dictionary<Cell, SlotInteractable>();
        private readonly Dictionary<Cell, DigInteractable> m_digs = new Dictionary<Cell, DigInteractable>();
        private readonly CounterInteractable m_counter;
        private readonly ExitInteractable m_exit;

        public BakeryConfigTable Config => m_config;
        public BurrowGrid Grid { get; }
        public BakeryLayout Layout { get; }
        public IReadOnlyList<BreadTable> UnlockedBreads => m_unlocked;
        public IReadOnlyDictionary<Cell, ShelfInteractable> Shelves => m_shelves;
        public IReadOnlyList<OvenInteractable> Ovens => m_ovens;
        // 진열대도 오븐도 없는 자리
        public IReadOnlyDictionary<Cell, SlotInteractable> Slots => m_slots;
        public BreadTable NextBread => m_unlocked.Count < Tables.GetAll<BreadTable>().Count ? Tables.GetAll<BreadTable>()[m_unlocked.Count] : null;
        // 설계 09 v0.4: 웜뱃이 계산대 range 안에 있나(serve가 auto면 있는 동안만 계산이 흐른다)
        public bool WombatAtCounter => WombatPresent && m_counter.DistanceTo(WombatPosition) <= (float)m_counter.Table.Range;

        // 오븐·진열대 하나가 바뀌었다(여러 사물을 보는 시트·소리·웜뱃 연출이 한 곳에서 듣는다. 사물 하나만 보는 뷰는 그 사물의 Changed)
        public event Action<Interactable> ThingChanged;
        // 굴을 팠거나 자리에 진열대·오븐이 놓였다
        public event Action LayoutChanged;
        // 구멍 앞에서 나가기 버튼(Mall이 웜뱃을 광장으로 옮긴다)
        public event Action ExitRequested;

        protected override BurrowNav WombatNav => Layout.WombatNav;
        protected override Vector2 Entrance => Layout.HoleFloor;

        // serve가 auto인가(설계 09 v0.4: auto면 계산대 range 안에서 타이머, manual이면 버튼)
        internal bool ServeAuto => Tables.Get<ActionTable>(ActionTable.k_Serve).IsAuto;

        // 첫 손님은 광장에서 온다. 웜뱃은 계산대 뒤에서 시작한다
        public BakeryArea(ZooState state, TableSet tables, IRandom random, Wombat wombat) : base(tables, wombat)
        {
            m_state = state;
            m_config = tables.Get<BakeryConfigTable>(BakeryConfigTable.k_Bakery);
            m_walkSpeed = tables.Get<ConfigTable>(ConfigTable.k_WalkSpeed).Value;
            m_hopSeconds = tables.Get<ConfigTable>(ConfigTable.k_HopSeconds).Value;
            m_random = random;
            Grid = new BurrowGrid(state, m_config);
            Grid.Dug += Grid_Dug;
            Layout = new BakeryLayout(tables);
            m_counter = new CounterInteractable(Row(CounterInteractable.k_Id), this);
            m_exit = new ExitInteractable(Row(ExitInteractable.k_Id), this, Layout.HoleFloor, OnExitRequested);

            // 시작 배치: 왼쪽 열(−1)의 자리 줄 두 곳에 첫 빵과 오븐. 오른쪽 열(0)은 빈 자리
            AddShelf(tables.GetAll<BreadTable>()[0], new Cell(-1, 1));
            AddOven(new Cell(-1, 3));
            RebuildLayout();
            EnterAt(Layout.WombatHome);
        }

        public bool IsEmptySlot(Cell cell)
        {
            return Grid.HasSlot(cell) && !m_shelves.ContainsKey(cell) && OvenAt(cell) == null;
        }

        // 그 칸의 오븐. 없으면 null
        public OvenInteractable OvenAt(Cell cell)
        {
            return m_ovens.Find(oven => oven.Cell.Equals(cell));
        }

        public ShelfInteractable ShelfOf(string breadId)
        {
            foreach (ShelfInteractable shelf in m_shelves.Values)
            {
                if (shelf.Bread.Id == breadId)
                {
                    return shelf;
                }
            }

            throw new KeyNotFoundException($"빵 '{breadId}'의 진열대가 없다.");
        }

        // 설계 13 v0.5: 시트 행동(UnlockAction·PlaceOvenAction)이 값을 치른 뒤 빈 자리에 놓는다
        internal void PlaceShelf(BreadTable bread, Cell cell)
        {
            AddShelf(bread, cell);
            RebuildLayout();
            OnLayoutChanged();
        }

        internal void PlaceOven(Cell cell)
        {
            AddOven(cell);
            RebuildLayout();
            OnLayoutChanged();
        }

        protected override void TickArea(double dt)
        {
            TickCustomers(dt);
            TickCheckout(dt);
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }

        private void AddShelf(BreadTable bread, Cell cell)
        {
            ShelfInteractable shelf = new ShelfInteractable(Row(ShelfInteractable.k_Id), cell, bread, this);
            shelf.Changed += Thing_Changed;
            m_unlocked.Add(bread);
            m_shelves[cell] = shelf;
        }

        private void AddOven(Cell cell)
        {
            OvenInteractable oven = new OvenInteractable(Row(OvenInteractable.k_Id), cell, this);
            oven.Changed += Thing_Changed;
            m_ovens.Add(oven);
        }

        private void Grid_Dug(Cell cell)
        {
            RebuildLayout();
            OnLayoutChanged();
        }

        private void Thing_Changed(Interactable thing)
        {
            ThingChanged?.Invoke(thing);
        }

        // 배치가 바뀌면 걷는 땅·줄 자리·서는 자리·사물 목록을 다시 맞추고, 걷는 중인 손님·웜뱃은 새 땅에서 길을 다시 찾는다
        private void RebuildLayout()
        {
            List<Cell> ovenCells = new List<Cell>();

            foreach (OvenInteractable oven in m_ovens)
            {
                ovenCells.Add(oven.Cell);
            }

            Layout.Rebuild(Grid.Cells, m_shelves.Keys, ovenCells, m_config.MaxCustomers);
            SyncThings();
            RepathCustomers();

            if (!WombatPresent)
            {
                return;
            }

            // 새 사물이 웜뱃 발밑에 놓이면 가까운 걷는 땅으로 비켜 선다
            BurrowNav nav = Layout.WombatNav;

            if (!nav.IsWalkable(WombatPosition) && nav.TryNearestFree(WombatPosition, _ => false, k_UnstuckDistance, out Vector2 free))
            {
                Wombat.Mover.Place(free);
            }

            // 대상은 다음 Tick에 고른다. 여기서 고르면 LayoutChanged보다 TargetChanged가 먼저 나가 화면에 없는 사물(새 오븐·진열대)을 가리킨다
        }

        // 빈 자리·팔 수 있는 칸은 칸 기준으로 맞춘다(있던 칸은 같은 객체를 둬 대상이 흔들리지 않는다). 목록 순서는 진열대 → 오븐 → 계산대 → 나가기 → 빈 자리 → 파기
        private void SyncThings()
        {
            List<Cell> slotCells = new List<Cell>();

            foreach (Cell cell in Grid.Cells)
            {
                if (IsEmptySlot(cell))
                {
                    slotCells.Add(cell);
                }
            }

            List<Cell> digCells = new List<Cell>(Grid.Frontier());
            Sync(m_slots, slotCells, cell => new SlotInteractable(Row(SlotInteractable.k_Id), cell, this));
            Sync(m_digs, digCells, cell => new DigInteractable(Row(DigInteractable.k_Id), cell, this));

            Placed.Clear();
            Placed.AddRange(m_shelves.Values);
            Placed.AddRange(m_ovens);
            Placed.Add(m_counter);
            Placed.Add(m_exit);

            foreach (Cell cell in slotCells)
            {
                Placed.Add(m_slots[cell]);
            }

            foreach (Cell cell in digCells)
            {
                Placed.Add(m_digs[cell]);
            }
        }

        private static void Sync<T>(Dictionary<Cell, T> things, List<Cell> cells, Func<Cell, T> create)
        {
            foreach (Cell cell in new List<Cell>(things.Keys))
            {
                if (!cells.Contains(cell))
                {
                    things.Remove(cell);
                }
            }

            foreach (Cell cell in cells)
            {
                if (!things.ContainsKey(cell))
                {
                    things[cell] = create(cell);
                }
            }
        }

        private void OnLayoutChanged()
        {
            LayoutChanged?.Invoke();
        }

        private void OnExitRequested()
        {
            ExitRequested?.Invoke();
        }
    }
}
