using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25: 농장 배치의 단일 출처. 광장처럼 파지 않는 방(FarmConfigTable cols × rows)이고 구멍(광장으로)은 첫 줄 가운데, 빵집 구멍과 같은 자리.
    // 밭 바닥이 걷는 땅을 막는다. 좌표는 농장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class FarmLayout
    {
        // 시작 밭 밑변: 칸 가운데에서 이만큼 아래
        private const float k_PlotDrop = 0.3f;

        private readonly float m_cellWidth;
        private readonly float m_cellHeight;
        private readonly float m_entranceHeight;

        public BurrowShape.Result Shape { get; }
        public BurrowNav Nav { get; private set; }
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        // 농장 크기(유닛): 가로는 가운데 정렬, 세로는 원점부터 아래로
        public float Width { get; }
        public float Height { get; }

        public FarmLayout(TableSet tables)
        {
            FarmConfigTable farm = tables.Get<FarmConfigTable>(FarmConfigTable.k_Main);
            m_cellWidth = (float)tables.Get<ConfigTable>(ConfigTable.k_CellWidth).Value;
            m_cellHeight = (float)tables.Get<ConfigTable>(ConfigTable.k_CellHeight).Value;
            m_entranceHeight = (float)tables.Get<ConfigTable>(ConfigTable.k_EntranceHeight).Value;
            Shape = BurrowShape.Room(tables, farm.Cols, farm.Rows);
            Width = farm.Cols * m_cellWidth;
            Height = m_entranceHeight + (farm.Rows - 1) * m_cellHeight;
            Rebuild(new List<PlotInteractable>());
        }

        // 시작 밭 밑변(입구 줄 아래 칸 가운데에서 조금 아래)
        public Vector2 PlotBase(Cell cell)
        {
            return new Vector2((cell.Col + 0.5f) * m_cellWidth, -(m_entranceHeight + (cell.Row - 0.5f) * m_cellHeight) - k_PlotDrop);
        }

        public void Rebuild(IReadOnlyList<PlotInteractable> plots)
        {
            List<NavRect> blocked = new List<NavRect>();

            foreach (PlotInteractable plot in plots)
            {
                blocked.Add(Placement.Rect(plot));
            }

            Nav = new BurrowNav(Shape, blocked);
        }
    }
}
