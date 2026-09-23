using System.Collections.Generic;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 2: 마스크의 둥글림·이음·입구 구멍. 칸 135×96, 입구 줄 80, 반지름 12(실제 값)
    public sealed class BurrowShapeTests
    {
        private const int k_W = 135;
        private const int k_H = 96;
        private const int k_E = 80;
        private const int k_R = 12;

        private static BurrowShape.Result Build(params Cell[] cells)
        {
            return BurrowShape.Build(new HashSet<Cell>(cells), k_W, k_H, k_E, k_R);
        }

        // 굴 원점 기준 칸 좌표(x, y 아래로)의 마스크 값
        private static bool At(BurrowShape.Result r, int x, int y)
        {
            return r.Mask[x - r.OriginX, y - r.OriginY];
        }

        [Test]
        public void SingleCell_CenterFloor_CornersRounded()
        {
            BurrowShape.Result r = Build(new Cell(0, 1));
            int top = k_E;

            Assert.That(At(r, 67, top + 48), Is.True);
            Assert.That(At(r, 0, top), Is.False);
            Assert.That(At(r, k_W - 1, top + k_H - 1), Is.False);
            Assert.That(At(r, 0, top + 48), Is.True);
            Assert.That(At(r, 67, top), Is.True);
            Assert.That(At(r, -1, top + 48), Is.False);
        }

        [Test]
        public void AdjacentCells_NoWallBetween()
        {
            BurrowShape.Result r = Build(new Cell(-1, 1), new Cell(0, 1));
            int y = k_E + 48;

            Assert.That(At(r, -1, y), Is.True);
            Assert.That(At(r, 0, y), Is.True);
            Assert.That(At(r, 0, k_E), Is.True);
            Assert.That(At(r, -1, k_E + k_H - 1), Is.True);
        }

        [Test]
        public void LShape_InnerCornerFilled()
        {
            BurrowShape.Result r = Build(new Cell(0, 1), new Cell(0, 2), new Cell(1, 2));
            // 꼭짓점 (1, row 2) 오른쪽 위 = (1,1)이 빠진 칸. 그 모서리 사각형의 꼭짓점 쪽은 메워진다
            int vx = k_W;
            int vy = k_E + k_H;

            Assert.That(At(r, vx + 1, vy - 2), Is.True);
            Assert.That(At(r, vx + k_R - 1, vy - k_R), Is.False);
        }

        [Test]
        public void EntranceRow_OnlyFunnelBelowHoleIsFloor()
        {
            BurrowShape.Result r = Build(new Cell(-1, 0), new Cell(0, 0), new Cell(-1, 1), new Cell(0, 1));

            Assert.That(At(r, 0, 10), Is.False);
            Assert.That(At(r, 0, 60), Is.True);
            Assert.That(At(r, -130, 60), Is.False);
            Assert.That(At(r, -130, k_E - 1), Is.True);
            Assert.That(At(r, 0, k_E), Is.True);
        }
    }
}
