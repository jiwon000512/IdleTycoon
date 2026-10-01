# -*- coding: utf-8 -*-
# 반짝돌 뽑기 돌 깨기 연출 그림(2026-10-01 사용자 선택 B 「산산조각」). 칸 단위로 직접 만든다(1 UI px = 화면 4px).
#   돌(../draw_stone.png, 68×66)의 가운데 조금 아래(CX, CY)에서 사방 일곱 갈래로 들쭉날쭉한 금을 긋고,
#   - draw_crack_0~2: 금이 30 · 65 · 100%까지 간 겹 그림(돌과 같은 크기)
#   - draw_crack_glow_gold · _mint: 다 간 금 옆으로 새는 빛(★2는 민트, 그 밖은 금빛)
#   - draw_piece_0~6: 금을 벽으로 나눈 조각 일곱(각각 돌과 같은 크기, 그 조각 칸만, 가장자리 진갈색). 날아가는 방향은 조각 무게중심 - (CX, CY)
#   - draw_rays_gold_0 · 1, draw_rays_mint_0 · 1: 유물 뒤 빛살 두 장(120×120, 번갈아 보여 돌아 보이게)
#   - draw_spark_gold_0~2, draw_spark_mint_0~2: 반짝임 작은 · 중간 · 큰(3 · 5 · 7칸)
#   - draw_flash: 깨지는 순간 번쩍(지름 61칸 원)
# 출력 ../draw_*.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_draw_fx.py
import math
import os
import random
from collections import deque
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
INK = (52, 32, 32)
LIGHT = {'gold': (255, 236, 170), 'mint': (206, 250, 232)}
RAYS = {'gold': [(244, 224, 160), (232, 196, 104)], 'mint': [(206, 244, 228), (150, 222, 196)]}
SPARK = [['.w.', 'www', '.w.'],
         ['..c..', '..w..', 'cwwwc', '..w..', '..c..'],
         ['...c...', '...c...', '..cwc..', 'ccwwwcc', '..cwc..', '...c...', '...c...']]
CX, CY = 33, 36
ANGLES = [k * 2 * math.pi / 7 + 0.3 for k in range(7)]
SEED = ord('b')      # 시안 B(stone_assets.py)와 같은 금

stone = np.asarray(Image.open(os.path.join(OUT, 'draw_stone.png')).convert('RGBA')).copy()
H, W = stone.shape[:2]
mask = stone[..., 3] > 0


def save(a, name):
    Image.fromarray(a.astype(np.uint8)).save(os.path.join(OUT, name + '.png'))
    print(name, a.shape[1], 'x', a.shape[0])


def walk(angle, seed):
    # 가운데에서 바깥으로 들쭉날쭉한 금 한 줄. 띄엄띄엄한 점을 8방향으로 이어 4방향 흐름을 막는 벽이 되게 한다
    rnd = random.Random(seed)
    pts, x, y, a = [], float(CX), float(CY), angle
    while 0 <= int(round(x)) < W and 0 <= int(round(y)) < H and mask[int(round(y)), int(round(x))]:
        pts.append((int(round(x)), int(round(y))))
        a += rnd.uniform(-0.9, 0.9) + (angle - a) * 0.4
        x += math.cos(a)
        y += math.sin(a)
        if rnd.random() < 0.25:
            x += rnd.choice((-1, 1)) * abs(math.sin(a))
            y += rnd.choice((-1, 1)) * abs(math.cos(a))
    line = [(CX, CY)]
    for x1, y1 in pts:
        x0, y0 = line[-1]
        while (x0, y0) != (x1, y1):
            x0 += (x1 > x0) - (x1 < x0)
            y0 += (y1 > y0) - (y1 < y0)
            if 0 <= x0 < W and 0 <= y0 < H and mask[y0, x0]:
                line.append((x0, y0))
            else:
                break
    return line


def cracks(paths):
    for stage, frac in enumerate((0.3, 0.65, 1.0)):
        a = np.zeros((H, W, 4), np.uint8)
        for p in paths:
            for x, y in p[:int(len(p) * frac)]:
                a[y, x] = INK + (255,)
        save(a, 'draw_crack_%d' % stage)
    for col, lc in LIGHT.items():
        g = np.zeros((H, W, 4), np.uint8)
        for p in paths:
            for x, y in p[2:]:
                for dx, dy in ((1, 0), (0, 1)):
                    if 0 <= x + dx < W and 0 <= y + dy < H and mask[y + dy, x + dx]:
                        g[y + dy, x + dx] = lc + (255,)
        save(g, 'draw_crack_glow_' + col)


def pieces(paths):
    # 금을 벽으로 두고 4방향으로 이어진 덩어리를 조각으로. 30칸보다 작은 덩어리는 가장 큰 조각에, 금 칸은 붙은 조각에
    wall = np.zeros((H, W), bool)
    for p in paths:
        for x, y in p:
            wall[y, x] = True
    label = -np.ones((H, W), int)
    n = 0
    for y0 in range(H):
        for x0 in range(W):
            if mask[y0, x0] and not wall[y0, x0] and label[y0, x0] < 0:
                q = deque([(y0, x0)])
                label[y0, x0] = n
                while q:
                    y, x = q.popleft()
                    for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                        if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not wall[ny, nx] and label[ny, nx] < 0:
                            label[ny, nx] = n
                            q.append((ny, nx))
                n += 1
    keep = [i for i in range(n) if (label == i).sum() >= 30]
    for i in range(n):
        if i not in keep:
            label[label == i] = keep[0]
    for y, x in zip(*np.nonzero(wall & mask)):
        for ny, nx in ((y, x + 1), (y + 1, x), (y, x - 1), (y - 1, x)):
            if 0 <= ny < H and 0 <= nx < W and label[ny, nx] >= 0:
                label[y, x] = label[ny, nx]
                break
    for k, i in enumerate(keep):
        m = label == i
        a = np.zeros_like(stone)
        a[m] = stone[m]
        pad = np.pad(m, 1)
        edge = m & ~(pad[2:, 1:-1] & pad[:-2, 1:-1] & pad[1:-1, 2:] & pad[1:-1, :-2])
        a[edge] = INK + (255,)
        ys, xs = np.nonzero(m)
        save(a, 'draw_piece_%d' % k)
        print('  dir', round(xs.mean() - CX, 1), round(ys.mean() - CY, 1))


def rays():
    n = 120
    c = (n - 1) / 2
    y, x = np.mgrid[0:n, 0:n]
    r = np.hypot(x - c, y - c)
    for col, (ci, co) in RAYS.items():
        for i, ph in enumerate((0, 7.5)):
            ang = (np.degrees(np.arctan2(y - c, x - c)) + 360 + ph) % 30
            wedge = (ang < 15) & (r <= 58) & (r >= 6)
            a = np.zeros((n, n, 4), np.uint8)
            a[wedge & (r < 38)] = ci + (255,)
            a[wedge & (r >= 38)] = co + (255,)
            save(a, 'draw_rays_%s_%d' % (col, i))


def sparks():
    for col, (ci, co) in RAYS.items():
        for i, rows in enumerate(SPARK):
            n = len(rows)
            a = np.zeros((n, n, 4), np.uint8)
            for y, row in enumerate(rows):
                for x, ch in enumerate(row):
                    if ch == 'w':
                        a[y, x] = (255, 255, 250, 255)
                    elif ch == 'c':
                        a[y, x] = co + (255,)
            save(a, 'draw_spark_%s_%d' % (col, i))


def flash():
    n = 61
    yy, xx = np.mgrid[0:n, 0:n] - 30
    a = np.zeros((n, n, 4), np.uint8)
    a[xx ** 2 + yy ** 2 <= 30.3 ** 2] = (255, 253, 246, 255)
    save(a, 'draw_flash')


if __name__ == '__main__':
    paths = [walk(a, 11 * i + SEED) for i, a in enumerate(ANGLES)]
    cracks(paths)
    pieces(paths)
    rays()
    sparks()
    flash()
