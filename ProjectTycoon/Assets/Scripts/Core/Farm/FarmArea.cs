using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 농장 굴. 광장 계단 오른쪽 문과 이 굴 구멍이 이어진다. 빵집처럼 판다(BurrowGrid, 층 범위·비용은 FarmFloorTable).
    // 판 칸(입구 줄 제외)마다 밭 사물(PlotInteractable) 하나: 흙 → 갈기 → 밭 → 심기 → 자람 → 밟고 거두기. 밭은 걷는 바닥이라 길을 막지 않는다.
    // 놓는 사물은 없다(편집 모드 카드 없음, 편집은 파기 표식 보기). 손님은 없고, 거둔 재료는 들고 다니지 않고 바로 창고(ZooState)로.
    // 설계 38: 점원 하나가 입구 작업대(Barn) 자리에서 밭을 돈다(명부는 곳 공용 WombatArea.Clerks).
    // 설계 39: 층 하나 = FarmArea 하나(FarmFloorTable 행). 1층이 아래층을 차례로 만들어 잇는다. 층을 다 파면 맨 아래 가운데 계단 자리에서
    // 아래층 값을 치르고, 그 자리에 계단 굴과 아래로 가는 통로가 선다. 위 구멍은 위층(1층은 광장)으로. 연 작물은 모든 층이 같은 목록을 쓴다
    public sealed class FarmArea : WombatArea
    {
        public const string k_Id = "farm";
        // 시작 판 칸은 가운데 두 열(줄 수는 FarmFloorTable startRows)
        private const int k_StartCols = 2;

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];

        private readonly List<PlotInteractable> m_plots = new List<PlotInteractable>();
        private readonly List<CropTable> m_unlocked;
        private readonly DigSet m_digs;
        private readonly PassageInteractable m_exit;
        private readonly StairInteractable m_stair;
        private readonly PassageInteractable m_down;

        public FarmConfigTable Config { get; }
        // 설계 39: 이 층의 표 행 · 몇 층(1부터) · 위층 · 아래층(없으면 null) · 열렸나(1층은 처음부터)
        public FarmFloorTable Floor { get; }
        public int Number { get; }
        public FarmArea Upper { get; }
        public FarmArea Lower { get; }
        public bool IsOpen { get; private set; }
        // 층 범위를 다 팠나(파기 대상이 없다)
        public bool IsFull => !Grid.Frontier().Any();
        // 설계 38: 농장 점원 자리
        public BarnInteractable Barn { get; }
        public BurrowGrid Grid { get; }
        public FarmLayout Layout { get; }
        // 판 순서(시작 칸은 줄 → 열 순)
        public IReadOnlyList<PlotInteractable> Plots => m_plots;
        // 설계 35: 밭 시트에 칩이 뜨는 작물(CropTable 행 순서, 시작은 첫 행)과 다음 해금 작물(다 열었으면 null)
        public IReadOnlyList<CropTable> UnlockedCrops => m_unlocked;
        public CropTable NextCrop => m_unlocked.Count < Tables.GetAll<CropTable>().Count ? Tables.GetAll<CropTable>()[m_unlocked.Count] : null;
        public override IReadOnlyList<IPlacedKind> ShopKinds => s_noKinds;
        public override string Id => Floor.Id;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.HoleFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;

        protected override IEnumerable<IPlaced> PlacedThings
        {
            get { yield break; }
        }

        // 1층(아래층은 1층이 만든다)
        public FarmArea(TableSet tables, IRandom random, Wombat wombat, EventBus bus) : this(tables, 0, null, random, wombat, bus)
        {
        }

        private FarmArea(TableSet tables, int index, FarmArea upper, IRandom random, Wombat wombat, EventBus bus) : base(tables, random, wombat, bus)
        {
            IReadOnlyList<FarmFloorTable> floors = tables.GetAll<FarmFloorTable>();
            Config = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            Floor = floors[index];
            Number = index + 1;
            Upper = upper;
            IsOpen = upper == null;
            Layout = new FarmLayout(tables, Floor.FloorRows);
            m_unlocked = upper != null ? upper.m_unlocked : new List<CropTable> { tables.GetAll<CropTable>()[0] };
            Grid = new BurrowGrid(BurrowGrid.Columns(k_StartCols, Floor.StartRows), Floor.DigBaseCost, Floor.DigCostGrowth,
                new CellBounds(Floor.FloorCols, Floor.FloorRows), bus);
            m_digs = new DigSet(Row(DigInteractable.k_Id), Grid, Layout.Cells, this);
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, upper != null ? upper.Id : PlazaArea.k_Id);
            m_stair = new StairInteractable(Row(StairInteractable.k_Id), this);
            Barn = new BarnInteractable(Row(BarnInteractable.k_Id), this, new Vector2((float)Config.BarnX, (float)Config.BarnY));

            // 시작 밭은 앞에서부터 갈아 둔다(사건 없이)
            foreach (Cell cell in BurrowGrid.Columns(k_StartCols, Floor.StartRows))
            {
                AddPlot(cell, m_plots.Count < Floor.StartFields);
            }

            if (index + 1 < floors.Count)
            {
                Lower = new FarmArea(tables, index + 1, this, random, wombat, bus);
                m_down = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.StairFloor, Lower.Id, true);
            }

            bus.Subscribe<Events.Dug>(Bus_Dug);
            Rebuild();
        }

        public override DigInteractable DigAt(Vector2 p)
        {
            return m_digs.At(p);
        }

        // 설계 39: 열리지 않은 층은 점원 자리를 내지 않는다(점원 팝업 탭도 없다)
        public override IEnumerable<Interactable> ClerkSlots
        {
            get
            {
                if (IsOpen)
                {
                    yield return Barn;
                }
            }
        }

        // 설계 35: 밭 시트가 값을 치른 뒤 다음 작물을 연다(CropTable 행 순서)
        internal void UnlockCrop(CropTable crop)
        {
            m_unlocked.Add(crop);
        }

        // 설계 39: 계단 파기. 이 층을 다 팠고 아래층이 닫혀 있으면 아래층 openCost를 치르고 연다
        internal bool TryOpenLower(Worker worker)
        {
            if (Lower == null || Lower.IsOpen || !IsFull || !worker.Wallet.TrySpendCoins(Lower.Floor.OpenCost))
            {
                return false;
            }

            Lower.IsOpen = true;
            Rebuild();
            Bus.Publish(new Events.FloorOpened(Lower));
            Bus.Publish(new Events.LayoutChanged(this));
            return true;
        }

        // 설계 43: 저장에서 열린 층(값 · 사건 없이). 위층에 내려가는 통로가 선다
        internal void RestoreOpen()
        {
            IsOpen = true;
            Upper?.Rebuild();
        }

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }

        public override string PlazaGate => Upper?.PlazaGate ?? Id;
        // 놓는 사물은 없지만 편집 모드에서 파기 표식을 본다
        public override bool Editable => true;
        internal override Interactable FixedClerkSlot => Barn;

        // 설계 39: 아래층에서 올라오면 계단 굴 안에 선다
        protected override Vector2 EntranceFrom(WombatArea from)
        {
            return from != null && from == Lower ? Layout.StairFloor : Entrance;
        }

        protected override IPlacedKind KindOf(string kindId)
        {
            throw new InvalidOperationException("농장에는 놓는 사물이 없다.");
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            throw new InvalidOperationException("농장에는 놓는 사물이 없다.");
        }

        protected override void Destroy(IPlaced thing)
        {
            throw new InvalidOperationException("농장에는 놓는 사물이 없다.");
        }

        protected override void OnPlacementChanged()
        {
            Rebuild();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        // 판 칸(입구 줄 제외)에 밭 사물을 둔다
        private void AddPlot(Cell cell, bool tilled)
        {
            if (cell.Row > BurrowGrid.k_EntranceRow)
            {
                m_plots.Add(new PlotInteractable(Row(PlotInteractable.k_Id), cell, this, tilled));
            }
        }

        private void Bus_Dug(Events.Dug e)
        {
            if (e.Grid != Grid)
            {
                return;
            }

            AddPlot(e.Cell, false);
            Rebuild();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        // 걷는 땅(판 칸 전부 + 열린 계단 굴)과 사물 목록을 다시 맞추고, 걷는 점원은 새 땅에서 길을 다시 찾는다
        private void Rebuild()
        {
            Layout.Rebuild(Grid.Cells, Lower != null && Lower.IsOpen);
            m_digs.Sync();
            SyncThings();
            RepathClerks();
            Unstick();
        }

        // 설계 37: 농장엔 손님이 없어 똥은 사물 목록에만 든다(밭 위에도 떨어진다)
        protected override void OnPoopsChanged()
        {
            SyncThings();
        }

        protected override void OnClerkThingsChanged()
        {
            SyncThings();
        }

        // 사물 목록: 딴짓 점원 → 똥 → 밭 → 나가기 → 계단 자리 또는 아래로 가는 통로 → 파기(밭은 곁의 사물에 대상을 양보한다). 작업대는 대상이 아니다
        private void SyncThings()
        {
            Placed.Clear();
            Placed.AddRange(ClerkThings);
            Placed.AddRange(Poops);
            Placed.AddRange(m_plots);
            Placed.Add(m_exit);

            if (Lower != null && Lower.IsOpen)
            {
                Placed.Add(m_down);
            }
            else if (Lower != null && IsFull)
            {
                Placed.Add(m_stair);
            }

            Placed.AddRange(m_digs.Things);
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }
    }
}
