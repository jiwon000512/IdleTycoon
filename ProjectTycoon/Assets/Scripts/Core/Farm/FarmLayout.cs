using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 농장 배치의 단일 출처. 판 칸(FarmArea.Grid)으로 굴 마스크·걷는 땅을 만든다. 밭은 걷는 바닥이라 막는 사각형이 없다.
    // 구멍(광장으로)은 첫 줄 가운데, 빵집 구멍과 같은 자리. 크기(카메라 경계)는 층 범위(FarmConfigTable floorCols · floorRows)로 고정.
    // 좌표는 농장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위
    public sealed class FarmLayout
    {
        private static readonly NavRect[] s_noBlocked = new NavRect[0];

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        // 층 크기(유닛): 가로는 가운데 정렬, 세로는 원점부터 아래로
        public float Width { get; }
        public float Height { get; }

        public FarmLayout(TableSet tables, FarmConfigTable config)
        {
            Cells = new CellMetrics(tables);
            Width = config.FloorCols * Cells.CellWidth;
            Height = Cells.RowTop(config.FloorRows);
        }

        public void Rebuild(IReadOnlyCollection<Cell> cells)
        {
            Shape = Cells.Build(cells);
            Nav = new BurrowNav(Shape, s_noBlocked);
        }
    }
}
