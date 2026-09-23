using System;
using System.Collections.Generic;

namespace ZooTycoon.Core
{
    // 굴 격자 설계 v0.5 3장: 파낸 칸의 합집합에서 굴 바닥 마스크를 만든다(한 칸 = 1픽셀, y는 아래로).
    // 격자 꼭짓점마다 둘레 네 칸을 보고 바깥 모서리(파낸 칸 1개)는 반지름 r로 깎고, 안쪽 모서리(안 판 칸 1개)는 r로 메운다.
    // 입구 줄은 구멍 중간부터 아래로 벌어지는 4분의 1 타원(옛 entrance.png와 같은 식)
    public static class BurrowShape
    {
        // 입구 아치 그림(arch.png) 크기(칸). 아트 사실이라 상수
        private const int k_ArchWidth = 64;
        private const int k_ArchHeight = 53;

        public sealed class Result
        {
            // [x, y]. OriginX·OriginY는 왼쪽 위 픽셀의 칸 좌표(굴 원점 기준, y는 아래로 양수)
            public bool[,] Mask { get; }
            public int OriginX { get; }
            public int OriginY { get; }
            public int Width => Mask.GetLength(0);
            public int Height => Mask.GetLength(1);

            public Result(bool[,] mask, int originX, int originY)
            {
                Mask = mask;
                OriginX = originX;
                OriginY = originY;
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

            foreach (Cell cell in cells)
            {
                int x0 = cell.Col * cellWidth - originX;
                int y0 = RowTop(cell.Row, cellHeight, entranceHeight) - originY;
                int h = cell.Row == 0 ? entranceHeight : cellHeight;

                for (int y = y0; y < y0 + h; y++)
                {
                    for (int x = x0; x < x0 + cellWidth; x++)
                    {
                        mask[x, y] = cell.Row != 0 || Funnel(x + originX, y + originY, cellWidth, entranceHeight);
                    }
                }
            }

            HashSet<Cell> set = cells as HashSet<Cell> ?? new HashSet<Cell>(cells);

            for (int row = 0; row <= maxRow + 1; row++)
            {
                for (int col = minCol; col <= maxCol + 1; col++)
                {
                    RoundVertex(mask, set, col, row, cellWidth, cellHeight, entranceHeight, radius, originX, originY);
                }
            }

            return new Result(mask, originX, originY);
        }

        // 줄 윗변 y(칸). row 0 = 입구 줄
        public static int RowTop(int row, int cellHeight, int entranceHeight)
        {
            return row <= 0 ? 0 : entranceHeight + (row - 1) * cellHeight;
        }

        // 입구 줄 안 픽셀이 굴 바닥인가. 구멍 중간(top)부터 아래로 벌어져 줄 밑변에서 두 칸 폭이 된다
        private static bool Funnel(int x, int y, int cellWidth, int entranceHeight)
        {
            double top = entranceHeight - k_ArchHeight + k_ArchHeight * 0.55;

            if (y < top)
            {
                return false;
            }

            double t = (y - top) / (entranceHeight - 1 - top);
            double inner = k_ArchWidth * 0.42;
            double half = inner + (cellWidth - inner) * Math.Sqrt(Math.Max(0d, 1d - (1d - t) * (1d - t)));
            return Math.Abs(x + 0.5) < half;
        }

        // 꼭짓점 (col, row)의 왼쪽 위 = (col−1, row−1) … 오른쪽 아래 = (col, row). 파낸 칸이 1개면 그 칸의 모서리를 깎고, 3개면 빠진 칸 모서리를 메운다
        private static void RoundVertex(bool[,] mask, HashSet<Cell> cells, int col, int row, int cellWidth, int cellHeight, int entranceHeight, int radius, int originX, int originY)
        {
            int vx = col * cellWidth - originX;
            int vy = RowTop(row, cellHeight, entranceHeight) - originY;
            int dug = 0;
            int missingX = 0, missingY = 0, dugX = 0, dugY = 0;

            foreach ((int dx, int dy) in new[] { (-1, -1), (0, -1), (-1, 0), (0, 0) })
            {
                Cell cell = new Cell(col + dx, row + dy);

                if (cell.Row >= 0 && cells.Contains(cell))
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
            else if (dug == 3 && row > 0)
            {
                Corner(mask, vx, vy, missingX, missingY, radius, true);
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
