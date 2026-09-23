# -*- coding: utf-8 -*-
# 오븐 굽기 진행 표시 시안(칸 편집, AI 없음). 한 칸 = 2px(PPU 80). 진행 N단계 프레임을 미리 그려 가로 시트로 낸다.
# 사용: make_bar.py <출력 폴더> [단계 수=16] → bar_{a,b,c}.png (프레임 가로 1행, 0% … 100%)
import sys
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else '.'
N = int(sys.argv[2]) if len(sys.argv) > 2 else 16
H = lambda h: np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)] + [255], np.uint8)
LINE, TROUGH, TROUGH_D = H('#342020'), H('#5C4C42'), H('#46382F')
PANEL, PANEL_HI, PANEL_LO = H('#F0E4D8'), H('#FBF4E6'), H('#D9C8B4')
OR, OR_HI, OR_LO = H('#D87848'), H('#F0A070'), H('#B85E38')
GOLD, GOLD_HI, GOLD_LO = H('#F2C14E'), H('#FBE3A0'), H('#D0973A')
DOUGH, DOUGH_LO = H('#F4E6CC'), H('#E0CBA8')
CRUST, CRUST_HI, CRUST_LO = H('#C8803C'), H('#E8A858'), H('#9C5A2C')


def outline(mask):
    # 마스크 바깥 1칸 테두리
    m = np.pad(mask, 1)
    grown = m.copy()
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        grown |= np.roll(np.roll(m, dy, 0), dx, 1)
    return (grown & ~m)[1:-1, 1:-1]


def paint(c, mask, col):
    c[mask] = col


def loaf_mask(w, h):
    # 식빵 옆모습: 윗면이 둥근 덩어리
    y, x = np.mgrid[0:h, 0:w]
    top = h * 0.42
    body = (y >= top) & (x >= 0) & (x < w)
    cx, rx, ry = (w - 1) / 2, w / 2, top + 0.5
    dome = ((x - cx) / rx) ** 2 + ((y - top) / ry) ** 2 <= 1.0
    m = body | dome
    m[-1, 0] = m[-1, -1] = False
    return m


def draft_a(p):
    # 굵은 캡슐 + 왼쪽 빵 배지. 40×12칸
    W, Hh = 40, 12
    c = np.zeros((Hh, W, 4), np.uint8)
    bar = np.zeros((Hh, W), bool); bar[2:10, 6:40] = True
    for (yy, xx) in ((2, 39), (9, 39), (2, 6), (9, 6)):
        bar[yy, xx] = False
    paint(c, bar, LINE)
    inner = np.zeros_like(bar); inner[3:9, 7:39] = True
    inner[3, 38] = inner[8, 38] = False
    paint(c, inner, TROUGH); c[3, 7:38] = TROUGH_D
    fw = round(31 * p)
    if fw:
        f = np.zeros_like(bar); f[3:9, 7:7 + fw] = True; f &= inner
        paint(c, f, OR); c[4, 7:7 + fw][f[4, 7:7 + fw]] = OR_HI; c[3, 7:7 + fw][f[3, 7:7 + fw]] = OR_HI; c[8, 7:7 + fw][f[8, 7:7 + fw]] = OR_LO
    # 배지: 원 12×12
    y, x = np.mgrid[0:Hh, 0:12]
    disk = ((x - 5.5) ** 2 + (y - 5.5) ** 2) <= 5.6 ** 2
    ring = outline(disk) | (disk & ~(((x - 5.5) ** 2 + (y - 5.5) ** 2) <= 4.6 ** 2))
    cb = c[:, 0:12]
    paint(cb, disk, PANEL); paint(cb, ring & disk, LINE)
    cb[3, 4:8] = PANEL_HI
    lm = np.zeros((Hh, 12), bool); lm[4:9, 3:9] = loaf_mask(6, 5)
    paint(cb, lm, CRUST); paint(cb, outline(lm) & disk & ~ring, LINE)
    cb[5, 4:8][lm[5, 4:8]] = CRUST_HI
    return c


def draft_b(p):
    # 원형 타이머 20×20칸: 크림 판 위 주황 부채꼴이 12시부터 시계 방향으로
    S = 20
    c = np.zeros((S, S, 4), np.uint8)
    y, x = np.mgrid[0:S, 0:S]
    d2 = (x - 9.5) ** 2 + (y - 9.5) ** 2
    face = d2 <= 8.6 ** 2
    paint(c, outline(face), LINE); paint(c, face, PANEL)
    ang = (np.degrees(np.arctan2(x - 9.5, -(y - 9.5))) + 360) % 360
    wedge = face & (d2 <= 6.9 ** 2) & (ang < 360 * p + (0.01 if p >= 1 else 0))
    rim = face & ~(d2 <= 6.9 ** 2)
    paint(c, rim, PANEL_LO); c[2, 7:13][rim[2, 7:13]] = PANEL_HI
    paint(c, wedge, OR)
    edge = wedge & (d2 > 5.4 ** 2) & (ang < 180)
    paint(c, edge & (ang < 360 * p), OR_HI)
    # 가운데 꼭지 + 12시 눈금
    c[9:11, 9:11] = LINE
    c[1, 9:11] = LINE
    return c


def draft_c(p):
    # 식빵 26×16칸: 반죽색에서 왼쪽부터 노릇한 껍질색으로 구워진다
    W, Hh = 28, 18
    c = np.zeros((Hh, W, 4), np.uint8)
    m = np.zeros((Hh, W), bool); m[1:17, 1:27] = loaf_mask(26, 16)
    paint(c, outline(m), LINE)
    paint(c, m, DOUGH)
    y, x = np.mgrid[0:Hh, 0:W]
    lo = m & ~np.roll(m, -1, 0)  # 맨 아랫줄
    paint(c, lo, DOUGH_LO)
    baked = m & (x < 1 + round(26 * p))
    paint(c, baked, CRUST)
    paint(c, baked & lo, CRUST_LO)
    top = m & ~np.roll(m, 1, 0)  # 맨 윗줄
    paint(c, baked & np.roll(top, 1, 0), CRUST_HI)
    # 칼집 세 줄(구워진 쪽은 밝게)
    for cx in (8, 14, 20):
        for k in range(4):
            yy, xx = 3 + k, cx + k - 1
            if m[yy, xx]:
                c[yy, xx] = CRUST_HI if baked[yy, xx] else DOUGH_LO
    return c


for k, fn in (('a', draft_a), ('b', draft_b), ('c', draft_c)):
    frames = [fn(i / (N - 1)) for i in range(N)]
    h, w = frames[0].shape[:2]
    sheet = np.concatenate(frames, 1)
    Image.fromarray(np.repeat(np.repeat(sheet, 2, 0), 2, 1)).save(f'{OUT}/bar_{k}.png')
    print(k, w, 'x', h, 'cells,', N, 'frames')
