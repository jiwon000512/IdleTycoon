namespace ZooTycoon.Core
{
    // 설계 27: 굴 한 층의 칸 범위. 가로 cols칸(가운데 정렬, 첫 열 = −cols/2) × 세로 rows줄(0 = 입구 줄). 빵집은 None(상한 없음)
    public readonly struct CellBounds
    {
        public static readonly CellBounds None = new CellBounds(0, 0);

        public int Cols { get; }
        public int Rows { get; }
        public int FirstCol => -Cols / 2;
        public bool IsNone => Cols == 0;

        public CellBounds(int cols, int rows)
        {
            Cols = cols;
            Rows = rows;
        }

        public bool Contains(Cell cell)
        {
            return IsNone || cell.Col >= FirstCol && cell.Col < FirstCol + Cols && cell.Row >= 0 && cell.Row < Rows;
        }
    }
}
