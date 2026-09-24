using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 3장: 파낸 칸의 합집합에서 굴 바닥 마스크를 만든다(한 칸 = 1픽셀, y는 아래로).
    // 격자 꼭짓점마다 둘레 네 칸을 보고 바깥 모서리(파낸 칸 1개)는 반지름 r로 깎고, 안쪽 모서리(안 판 칸 1개)는 r로 메운다.
    // 굴 환경 A2(2026-09-24): 윗변이 드러난 칸(위 칸을 안 팠음, 입구 줄은 늘)은 바닥 윗변 위에 흙벽 띠 k_WallHeight칸이 선다.
    // 띠는 방 윤곽 안이지만 걷는 바닥이 아니다(WallRow). 바닥 윗변은 그대로라 길 찾기·자리는 바뀌지 않는다
    public static class BurrowShape
    {
        // 입구 줄 바닥 윗변(칸, 줄 윗변 기준) = 띠 밑변 = 아치 구멍 밑변
        public const int k_EntranceFloorTop = 57;
        // 윗벽 띠 높이(칸) = wall_face.png 높이. 아트 사실이라 상수
        public const int k_WallHeight = 40;

        public sealed class Result
        {
            // [x, y]. OriginX·OriginY는 왼쪽 위 픽셀의 칸 좌표(굴 원점 기준, y는 아래로 양수)
            public bool[,] Mask { get; }
            // 띠 안이면 wall_face 텍스처 행 + 1(1 = 띠 윗변), 아니면 0
            public int[,] WallRow { get; }
            public int OriginX { get; }
            public int OriginY { get; }
            public int Width => Mask.GetLength(0);
            public int Height => Mask.GetLength(1);

            public Result(bool[,] mask, int originX, int originY) : this(mask, new int[mask.GetLength(0), mask.GetLength(1)], originX, originY)
            {
            }

            public Result(bool[,] mask, int[,] wallRow, int originX, int originY)
            {
                Mask = mask;
                WallRow = wallRow;
                OriginX = originX;
                OriginY = originY;
            }

            // 걷는 바닥 = 방 안이면서 띠가 아닌 곳
            public bool IsFloor(int x, int y)
            {
                return Mask[x, y] && WallRow[x, y] == 0;
            }
        }

        public static Result Build(IReadOnlyCollection<Cell> cells, int cellWidth, int cellHeight, int entranceHeight, int radius)
        {
            int margin = radius + 2;
            int minCol = int.MaxValue, maxCol = int.MinValue, maxRow = 0;

            foreach (Cell cell in cells)
            {
                minCol = Math.Min(minCol, cell.Col);
                maxCol = Math.Max(maxCol, cell.Col);
                maxRow = Math.Max(maxRow, cell.Row);
            }

            int originX = minCol * cellWidth - margin;
            int originY = -margin;
            int width = (maxCol - minCol + 1) * cellWidth + margin * 2;
            int height = RowTop(maxRow + 1, cellHeight, entranceHeight) + margin * 2;
            bool[,] mask = new bool[width, height];
            int[,] wallRow = new int[width, height];
            HashSet<Cell> set = cells as HashSet<Cell> ?? new HashSet<Cell>(cells);

            foreach (Cell cell in cells)
            {
                int x0 = cell.Col * cellWidth - originX;
                int top = (cell.Row == 0 ? k_EntranceFloorTop : RowTop(cell.Row, cellHeight, entranceHeight)) - originY;
                int bottom = RowTop(cell.Row + 1, cellHeight, entranceHeight) - originY;
                bool exposed = !set.Contains(cell.Offset(0, -1));

                for (int y = exposed ? top - k_WallHeight : top; y < bottom; y++)
                {
                    for (int x = x0; x < x0 + cellWidth; x++)
                    {
                        mask[x, y] = true;
                        wallRow[x, y] = y < top ? y - (top - k_WallHeight) + 1 : 0;
                    }
                }
            }

            RoundCorners(mask, radius);
            return new Result(mask, wallRow, originX, originY);
        }

        // 줄 윗변 y(칸). row 0 = 입구 줄
        public static int RowTop(int row, int cellHeight, int entranceHeight)
        {
            return row <= 0 ? 0 : entranceHeight + (row - 1) * cellHeight;
        }

        // 격자점(픽셀 사이 꼭짓점)마다 둘레 네 픽셀을 본다. 1개만 방이면 바깥 모서리를 깎고, 3개면 빠진 쪽 안쪽 모서리를 메운다.
        // 칸·띠가 모두 축 정렬 사각형이라 모서리는 사각형 변이 만나는 곳에만 생긴다
        private static void RoundCorners(bool[,] mask, int radius)
        {
            bool[,] raw = (bool[,])mask.Clone();

            for (int vy = 1; vy < raw.GetLength(1); vy++)
            {
                for (int vx = 1; vx < raw.GetLength(0); vx++)
                {
                    int dug = 0;
                    int missingX = 0, missingY = 0, dugX = 0, dugY = 0;

                    foreach ((int dx, int dy) in new[] { (-1, -1), (0, -1), (-1, 0), (0, 0) })
                    {
                        if (raw[vx + dx, vy + dy])
                        {
                            dug++;
                            dugX = dx;
                            dugY = dy;
                        }
                        else
                        {
                            missingX = dx;
                            missingY = dy;
                        }
                    }

                    if (dug == 1)
                    {
                        Corner(mask, vx, vy, dugX, dugY, radius, false);
                    }
                    else if (dug == 3)
                    {
                        Corner(mask, vx, vy, missingX, missingY, radius, true);
                    }
                }
            }
        }

        // 꼭짓점에서 (dx, dy) 쪽 칸의 r×r 모서리 사각형: fill이면 원 밖을 채우고(안쪽 메움), 아니면 원 밖을 지운다(바깥 깎음)
        private static void Corner(bool[,] mask, int vx, int vy, int dx, int dy, int radius, bool fill)
        {
            int sx = dx < 0 ? -1 : 1;
            int sy = dy < 0 ? -1 : 1;
            double cx = vx + sx * radius;
            double cy = vy + sy * radius;
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);

            for (int i = 0; i < radius; i++)
            {
                for (int j = 0; j < radius; j++)
                {
                    int x = dx < 0 ? vx - 1 - i : vx + i;
                    int y = dy < 0 ? vy - 1 - j : vy + j;

                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    double px = x + 0.5 - cx;
                    double py = y + 0.5 - cy;
                    bool inside = px * px + py * py <= radius * radius;

                    if (fill && !inside)
                    {
                        mask[x, y] = true;
                    }
                    else if (!fill && !inside)
                    {
                        mask[x, y] = false;
                    }
                }
            }
        }
    }
}
