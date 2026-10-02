using System.Collections.Generic;
using System.Numerics;
using GameKit.Tables;

namespace ZooTycoon.Core
{
    // 설계 25 → 설계 27: 농장 배치의 단일 출처. 판 칸(FarmArea.Grid)으로 굴 마스크·걷는 땅을 만든다. 밭은 걷는 바닥이라 막는 사각형이 없다.
    // 구멍(광장으로)은 첫 줄 가운데, 빵집 구멍과 같은 자리. 좌표는 농장 원점(첫 줄 윗변 가운데) 기준 유닛, y 위. 카메라 경계는 화면(FarmView)이 판 칸으로 잰다.
    // 설계 39: 아래층이 열린 층은 층 맨 아래 줄 밑 가운데(가운데 통로 끝)에 계단 굴을 더 판다
    public sealed class FarmLayout
    {
        // 계단 굴 반폭 · 깊이(유닛, 계단 그림 1.3 × 0.775 + 둘레 바닥)와 아래층에서 올라와 서는 곳(굴 윗변에서 아래로, 첫 디딤판 위).
        // 서는 곳 아래 0.25부터 통로 띠라 걷는 땅 밑변(깊이 − 몸 반 폭 0.3)보다 위여야 한다
        private const float k_StairHalfWidth = 0.75f;
        private const float k_StairDepth = 1.0f;
        private const float k_StairStand = 0.3f;

        private static readonly NavRect[] s_noBlocked = new NavRect[0];

        public CellMetrics Cells { get; }
        public BurrowShape.Result Shape { get; private set; }
        public BurrowNav Nav { get; private set; }
        public Vector2 HoleFloor => new Vector2(0f, -2.2f);
        // 설계 39: 층 맨 아래 줄 밑변 가운데(계단 표식 · 계단 파기 기준점)와 계단 굴, 계단 굴 안에 서는 곳(아래로 가는 통로의 기준점)
        public Vector2 StairSpot { get; }
        public NavRect StairRect => new NavRect(-k_StairHalfWidth, StairSpot.Y - k_StairDepth, k_StairHalfWidth, StairSpot.Y);
        public Vector2 StairFloor => new Vector2(0f, StairSpot.Y - k_StairStand);

        public FarmLayout(TableSet tables, int floorRows)
        {
            Cells = new CellMetrics(tables);
            StairSpot = new Vector2(0f, -Cells.RowTop(floorRows));
        }

        public void Rebuild(IReadOnlyCollection<Cell> cells, bool stair)
        {
            Shape = Cells.Build(cells, stair ? StairRect : (NavRect?)null);
            Nav = new BurrowNav(Shape, s_noBlocked);
        }
    }
}
