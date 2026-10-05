using System.Collections.Generic;
using NUnit.Framework;
using ZooTycoon.Core;

namespace ZooTycoon.Tests
{
    // 굴 격자 설계 v0.5 검증 2: 마스크의 둥글림·이음·입구 구멍. 칸 135×96, 입구 줄 80, 반지름 12(실제 값).
    // 굴 환경 A2: 윗변이 드러난 칸 위에 띠 40칸. 둥근 모서리는 띠 윗모서리(방 윤곽)에, 띠와 바닥 경계는 곧은 선
    public sealed class BurrowShapeTests
    {
        private const int k_W = 135;
        private const int k_H = 96;
        private const int k_E = 80;
        private const int k_R = BurrowShape.k_RoundRadius;
        private const int k_FloorTop = BurrowShape.k_EntranceFloorTop;
        private const int k_K = BurrowShape.k_WallHeight;

        private static BurrowShape.Result Build(params Cell[] cells)
        {
            return BurrowShape.Build(new HashSet<Cell>(cells), k_W, k_H, k_E);
        }

        // 굴 원점 기준 칸 좌표(x, y 아래로)의 방 안 여부·걷는 바닥 여부·띠 행
        private static bool At(BurrowShape.Result r, int x, int y)
        {
            return r.Mask[x - r.OriginX, y - r.OriginY];
        }

        private static bool Floor(BurrowShape.Result r, int x, int y)
        {
            return r.IsFloor(x - r.OriginX, y - r.OriginY);
        }

        private static int Wall(BurrowShape.Result r, int x, int y)
        {
            return r.WallRow[x - r.OriginX, y - r.OriginY];
        }

        [Test]
        public void SingleCell_WallBandAbove_CornersRounded()
        {
            BurrowShape.Result r = Build(new Cell(0, 1));
            int top = k_E;

            Assert.That(Floor(r, 67, top + 48), Is.True);
            Assert.That(At(r, 0, top - k_K), Is.False);
            Assert.That(At(r, k_W - 1, top + k_H - 1), Is.False);
            Assert.That(Floor(r, 0, top), Is.True);
            Assert.That(At(r, -1, top + 48), Is.False);
            Assert.That(Wall(r, 67, top - k_K), Is.EqualTo(1));
            Assert.That(Wall(r, 67, top - 1), Is.EqualTo(k_K));
            Assert.That(Floor(r, 67, top - 1), Is.False);
            Assert.That(At(r, 67, top - k_K - 1), Is.False);
        }

        [Test]
        public void AdjacentCells_NoWallBetween()
        {
            BurrowShape.Result r = Build(new Cell(-1, 1), new Cell(0, 1));
            int y = k_E + 48;

            Assert.That(Floor(r, -1, y), Is.True);
            Assert.That(Floor(r, 0, y), Is.True);
            Assert.That(Floor(r, 0, k_E), Is.True);
            Assert.That(Floor(r, -1, k_E + k_H - 1), Is.True);
        }

        // 2026-10-05 사용자 「벽 모서리가 다듬어지지 않아서 이상해」: 띠 끝이 방으로 드러나면 그 끝 한 열은 방 밖(외곽선)이고,
        // 흙 덩어리 아래 모서리는 둥글리지 않아 외곽선이 덩어리 옆에서 띠 밑까지 곧게 내려간다
        [Test]
        public void SideCell_BandOpenEnd_OutlineRunsStraightDown()
        {
            BurrowShape.Result r = Build(new Cell(0, 1), new Cell(0, 2), new Cell(1, 2));
            // (1,2)는 위 칸을 안 파 띠가 (1,1) 아래 40칸에 선다. 띠 왼쪽 끝은 (0,1) 바닥으로 드러난다
            int vx = k_W;
            int vy = k_E + k_H - k_K;

            Assert.That(Wall(r, vx + 60, vy), Is.EqualTo(1));
            Assert.That(Floor(r, vx + 60, k_E + k_H), Is.True);
            Assert.That(At(r, vx, vy), Is.False);
            Assert.That(At(r, vx, vy + k_K - 1), Is.False);
            Assert.That(Wall(r, vx + 1, vy), Is.EqualTo(1));
            Assert.That(Wall(r, vx + 1, vy + k_K - 1), Is.EqualTo(k_K));
            Assert.That(At(r, vx + 1, vy - 2), Is.False);
            Assert.That(Floor(r, vx - 1, vy + 10), Is.True);
        }

        [Test]
        public void EntranceRow_BandAboveFloorLine_BandCornersRounded()
        {
            BurrowShape.Result r = Build(new Cell(-1, 0), new Cell(0, 0), new Cell(-1, 1), new Cell(0, 1));
            int bandTop = k_FloorTop - k_K;

            Assert.That(At(r, 0, bandTop - 1), Is.False);
            Assert.That(Wall(r, 0, bandTop), Is.EqualTo(1));
            Assert.That(Floor(r, 0, k_FloorTop - 1), Is.False);
            Assert.That(Floor(r, 0, k_FloorTop), Is.True);
            Assert.That(At(r, -k_W, bandTop), Is.False);
            Assert.That(At(r, k_W - 1, bandTop), Is.False);
            Assert.That(Floor(r, -k_W, k_FloorTop), Is.True);
            Assert.That(Floor(r, k_W - 1, k_FloorTop), Is.True);
            Assert.That(Floor(r, -130, k_E - 1), Is.True);
            Assert.That(Floor(r, 0, k_E), Is.True);
        }
    }
}
