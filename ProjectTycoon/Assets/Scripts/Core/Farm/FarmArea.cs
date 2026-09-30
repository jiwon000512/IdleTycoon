using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 농장 굴. 광장 계단 오른쪽 문과 이 굴 구멍이 이어진다. 빵집처럼 판다(BurrowGrid, 층 범위·비용은 FarmConfigTable).
    // 판 칸(입구 줄 제외)마다 밭 사물(PlotInteractable) 하나: 흙 → 갈기 → 밭 → 심기 → 자람 → 밟고 거두기. 밭은 걷는 바닥이라 길을 막지 않는다.
    // 놓는 사물은 없다(편집 모드 카드 없음, 편집은 파기 표식 보기). 손님·점원은 없고, 거둔 재료는 들고 다니지 않고 바로 창고(ZooState)로
    public sealed class FarmArea : WombatArea
    {
        public const string k_Id = "farm";
        // 시작 판 칸은 가운데 두 열(줄 수는 FarmConfigTable startRows)
        private const int k_StartCols = 2;

        private static readonly IPlacedKind[] s_noKinds = new IPlacedKind[0];

        private readonly FarmConfigTable m_config;
        private readonly List<PlotInteractable> m_plots = new List<PlotInteractable>();
        private readonly DigSet m_digs;
        private readonly PassageInteractable m_exit;

        public FarmConfigTable Config => m_config;
        // 덤 굴리기(설계 28)
        internal IRandom Random { get; }
        public BurrowGrid Grid { get; }
        public FarmLayout Layout { get; }
        // 판 순서(시작 칸은 줄 → 열 순)
        public IReadOnlyList<PlotInteractable> Plots => m_plots;
        // 심기가 심는 작물(CropTable 첫 행. 작물이 늘면 시트로 고른다)
        public CropTable Crop { get; }
        public override IReadOnlyList<IPlacedKind> ShopKinds => s_noKinds;
        public override string Id => k_Id;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.HoleFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;

        protected override IEnumerable<IPlaced> PlacedThings
        {
            get { yield break; }
        }

        public FarmArea(TableSet tables, IRandom random, Wombat wombat, EventBus bus) : base(tables, wombat, bus)
        {
            m_config = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            Random = random;
            Layout = new FarmLayout(tables);
            Crop = tables.GetAll<CropTable>()[0];
            Grid = new BurrowGrid(BurrowGrid.Columns(k_StartCols, m_config.StartRows), m_config.DigBaseCost, m_config.DigCostGrowth,
                new CellBounds(m_config.FloorCols, m_config.FloorRows), bus);
            m_digs = new DigSet(Row(DigInteractable.k_Id), Grid, Layout.Cells, this);
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);

            // 시작 밭은 앞에서부터 갈아 둔다(사건 없이)
            foreach (Cell cell in BurrowGrid.Columns(k_StartCols, m_config.StartRows))
            {
                AddPlot(cell, m_plots.Count < m_config.StartFields);
            }

            bus.Subscribe<Events.Dug>(Bus_Dug);
            Rebuild();
        }

        public override DigInteractable DigAt(Vector2 p)
        {
            return m_digs.At(p);
        }

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
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

        // 걷는 땅(판 칸 전부)과 사물 목록(밭 → 나가기 → 파기)을 다시 맞춘다
        private void Rebuild()
        {
            Layout.Rebuild(Grid.Cells);
            m_digs.Sync();
            Placed.Clear();
            Placed.AddRange(m_plots);
            Placed.Add(m_exit);
            Placed.AddRange(m_digs.Things);
            Unstick();
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }
    }
}
