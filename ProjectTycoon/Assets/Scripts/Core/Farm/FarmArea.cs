using System;
using System.Collections.Generic;
using System.Numerics;
using GameKit.Events;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25: 농장 굴. 광장 계단 오른쪽 문과 이 굴 구멍이 이어진다. 사물은 밭(편집 모드에서 사서 놓는다)과 나가기, 손님·점원은 없다.
    // 거둔 재료는 들고 다니지 않고 바로 창고(ZooState)로 간다(PlotInteractable.Harvest)
    public sealed class FarmArea : WombatArea
    {
        public const string k_Id = "farm";

        private readonly List<PlotInteractable> m_plots = new List<PlotInteractable>();
        private readonly List<IPlacedKind> m_shopKinds = new List<IPlacedKind>();
        private readonly PassageInteractable m_exit;

        public FarmLayout Layout { get; }
        public IReadOnlyList<PlotInteractable> Plots => m_plots;
        // 심기가 심는 작물(CropTable 첫 행. 작물이 늘면 시트로 고른다)
        public CropTable Crop { get; }
        public override IReadOnlyList<IPlacedKind> ShopKinds => m_shopKinds;
        public override string Id => k_Id;

        protected override BurrowNav WombatNav => Layout.Nav;
        protected override Vector2 Entrance => Layout.HoleFloor;
        protected override BurrowShape.Result Shape => Layout.Shape;
        protected override IEnumerable<IPlaced> PlacedThings => m_plots;

        // 시작 밭은 입구 줄 아래 칸 가운데에 왼쪽부터(FarmConfigTable startPlots)
        public FarmArea(TableSet tables, Wombat wombat, EventBus bus) : base(tables, wombat, bus)
        {
            FarmConfigTable config = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            Layout = new FarmLayout(tables);
            Crop = tables.GetAll<CropTable>()[0];
            m_exit = new PassageInteractable(Row(PassageInteractable.k_Exit), this, Layout.HoleFloor, PlazaArea.k_Id);
            m_shopKinds.Add(Row(PlotInteractable.k_Id));
            int firstCol = -config.Cols / 2;

            for (int i = 0; i < config.StartPlots; i++)
            {
                Create(Row(PlotInteractable.k_Id), Layout.PlotBase(new Cell(firstCol + i % config.Cols, 1 + i / config.Cols)));
            }

            Rebuild();
        }

        internal override bool IsPassage(Vector2 p)
        {
            return Vector2.DistanceSquared(p, Layout.HoleFloor) < 1e-4f;
        }

        protected override IPlacedKind KindOf(string kindId)
        {
            return Row(kindId);
        }

        protected override IPlaced Create(IPlacedKind kind, Vector2 at)
        {
            if (kind.Id != PlotInteractable.k_Id)
            {
                throw new ArgumentException($"농장에 놓을 수 없는 종류 '{kind.Id}'.");
            }

            PlotInteractable plot = new PlotInteractable((InteractableTable)kind, at, this);
            m_plots.Add(plot);
            return plot;
        }

        protected override void Destroy(IPlaced thing)
        {
            m_plots.Remove((PlotInteractable)thing);
        }

        protected override void OnPlacementChanged()
        {
            Rebuild();
            Bus.Publish(new Events.LayoutChanged(this));
        }

        // 걷는 땅과 사물 목록(밭 → 나가기)을 다시 맞춘다
        private void Rebuild()
        {
            Layout.Rebuild(m_plots);
            Placed.Clear();
            Placed.AddRange(m_plots);
            Placed.Add(m_exit);
            Unstick();
        }

        private InteractableTable Row(string interactableId)
        {
            return Tables.Get<InteractableTable>(interactableId);
        }
    }
}
