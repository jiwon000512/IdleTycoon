# 광장 낚시터 문 장식(2026-10-05 사용자 선택 B 「물고기 간판」, 프롬프트 raw/fishing_door_prompt.txt).
#   빵집 문 차양처럼 아치(../../Shop/arch.png, 그대로 씀) 꼭대기에 얹는 장식 한 장. 나무 물고기 몸통이 이름 판이고 글은 게임이 쓴다.
#   옛 간판(sign.png)은 문 바깥 왼쪽이라 웜뱃이 낚시터 문 앞에 서면 HUD 시간 알약과 겹쳤다 → 이름 판을 문 한가운데로.
#   바탕 raw/fishing_door_template.png: 1536×1024, 한 칸 10px. 실제 웜뱃(크기 기준) + 아치 자리 연보라 유령(왼쪽 칸 66 · 밑변 줄 92) + 장식 상자 · 이름 칸 안내선.
#   격자만 snap_codex로 찾고 칸 색은 직접 뽑는다(snap은 밝은 유령 가장자리를 벗기고 둘레에 외곽선을 그어서). 흰 칸 · 유령 · 웜뱃을 빼고 가장 큰 덩어리 상자 안을 남긴다.
#   가장자리 흐림에서 생긴 회색 칸: 어두우면 외곽선, 밝은데 바깥에 닿으면 투명.
#   카메라 위 경계가 아치 꼭대기 위 8칸이라, 아치 꼭대기 위로 6칸까지만 솟게 내린다(그려진 자리는 17칸 솟음).
# 출력: ../fishing_door.png(한 칸 2px · PPU 80, 피벗 아래 가운데). 자리 · 이름 판 가운데를 칸과 유닛으로 출력한다.
# 사용: python make_fishing_door.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

P = 10                                         # 바탕 한 칸 px
GHOST, GHOST_IN = (226, 216, 244), (204, 192, 232)
ARCH_X, BASE, ARCH_W, ARCH_H = 66, 92, 60, 49  # 바탕의 아치 자리(칸)
LINE = (52, 32, 32)
MAX_RISE = 6


def near(c, ref, d):
    return np.abs(c - np.array(ref)).sum(-1) < d


def sample(path):
    _, (px, phx, py, phy) = snap_codex.snap(path, 48, square=True, min_hole=40)
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    h, w = a.shape[:2]
    xs, ys = np.arange(phx + 1, w, px), np.arange(phy + 1, h, py)
    c = np.zeros((len(ys) - 1, len(xs) - 1, 3))
    for j in range(len(ys) - 1):
        for i in range(len(xs) - 1):
            x0, x1, y0, y1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            blk = a[int(y0 + (y1 - y0) * 0.25):int(y1 - (y1 - y0) * 0.25) + 1, int(x0 + (x1 - x0) * 0.25):int(x1 - (x1 - x0) * 0.25) + 1]
            c[j, i] = np.median(blk.reshape(-1, 3), 0)
    return c.round().astype(int), (xs[0] + px / 2) / P - 0.5, (ys[0] + py / 2) / P - 0.5


def cut(path):
    c, col0, row0 = sample(path)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    ghost = near(c, GHOST, 36) | near(c, GHOST_IN, 36) | ((b > r) & (r > g) & (b - g > 25) & (c.sum(-1) > 450))
    m = (c.min(-1) <= 238) & ~ghost
    m[:, :int(ARCH_X - 6 - col0)] = False                  # 왼쪽 웜뱃
    big = max(blobs(m, diag=True), key=len)
    ys = [y for y, _ in big]; xs = [x for _, x in big]
    y0, y1, x0, x1 = min(ys), max(ys) + 1, min(xs), max(xs) + 1
    keep = m[y0:y1, x0:x1].copy()
    p = np.zeros((y1 - y0, x1 - x0, 4), np.int64)
    p[keep, :3] = c[y0:y1, x0:x1][keep]
    # 가장자리 흐림 회색
    pad = np.pad(keep, 1)
    edge = keep & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    grey = keep & (p[..., :3].max(-1) - p[..., :3].min(-1) < 30)
    bright = p[..., :3].sum(-1) >= 480
    p[grey & ~bright, :3] = LINE
    keep &= ~(grey & bright & edge)
    p[keep, 3] = 255
    p[~keep] = 0
    p = snap_codex.merge_colors(p, keep, 12)
    cols = np.unique(p[keep][:, :3], axis=0)
    p[keep & (p[..., :3] == cols[cols.sum(1).argmin()]).all(-1), :3] = LINE
    # 자리(칸): 장식 밑변 가운데가 아치 밑변 가운데에서 옆 dx · 위 dy
    dx = (col0 + (x0 + x1) / 2) - (ARCH_X + ARCH_W / 2)
    dy = BASE - (row0 + y1)
    rise = (BASE - ARCH_H) - (row0 + y0)
    dy -= max(0.0, rise - MAX_RISE)
    return p.astype(np.uint8), dx, dy


def board(p):
    """이름 판: 한 색으로 이어진 칸 중 직사각형(85% 넘게 참)이고 폭 20칸 넘는 가장 큰 것 → 판 크기 · 가운데(장식 밑변 가운데 기준)"""
    best = []
    for col in np.unique(p[p[..., 3] > 0][:, :3], axis=0):
        for gr in blobs((p[..., 3] > 0) & (p[..., :3] == col).all(-1), diag=False):
            ys = [y for y, _ in gr]; xs = [x for _, x in gr]
            w, h = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
            if w >= 20 and len(gr) > 0.85 * w * h and len(gr) > len(best):
                best = gr
    ys = [y for y, _ in best]; xs = [x for _, x in best]
    return (max(xs) - min(xs) + 1, max(ys) - min(ys) + 1,
            (min(xs) + max(xs) + 1) / 2 - p.shape[1] / 2, p.shape[0] - (min(ys) + max(ys) + 1) / 2)


if __name__ == '__main__':
    p, dx, dy = cut(os.path.join(RAW, 'fishing_door_b.png'))
    assert p.shape[1] <= 68 and len(np.unique(p[p[..., 3] > 0][:, :3], axis=0)) <= 12
    Image.fromarray(np.repeat(np.repeat(p, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', 'fishing_door.png'))
    bw, bh, bx, by = board(p)
    print('fishing_door.png %d x %d 칸' % (p.shape[1], p.shape[0]))
    print('자리: 피벗(아래 가운데)이 아치 밑변 가운데에서 옆 %.1f칸 · 위 %.1f칸 = (%.4f, %.4f) 유닛' % (dx, dy, dx / 40, dy / 40))
    print('이름 판 %d x %d 칸, 가운데 = 아치 밑변 가운데에서 (%.1f, %.1f)칸 = (%.4f, %.4f) 유닛' % (bw, bh, dx + bx, dy + by, (dx + bx) / 40, (dy + by) / 40))
