# 설계 39 농장 층별 굴 재료(2026-10-02 사용자 선택 C 「붉은 흙 + 뿌리 + 점토 결」, 시안 A 색만 · B 뿌리만은 버림). 흙 색은 5층마다 한 번 바뀐다.
# 1층 재료 세 장(Shop/floor_tile 32×32 · wall_tile 32×32 · wall_face 64×40, 한 칸 = 1px · PPU 40)의 결과 칸은 그대로 두고
#   색을 층 색으로 옮긴 뒤(HSV: 색상 11° · 채도 ×1.2 · 명도 ×0.92), 뿌리와 점토 무늬를 더한다. 무늬는 타일 둘레를 넘으면 반대편으로 이어 그려 이음매가 없다.
#   뿌리: 지층 띠에 늘어진 뿌리 넷(곁뿌리 하나씩) · 바닥에 짧은 조각 둘(타일마다 되풀이돼 격자로 보이지 않게 바닥보다 조금만 밝게) · 바깥 흙에 짧은 조각 둘
#   점토: 바닥에 밝은 덩이 둘 · 마른 금 둘, 지층 띠에 가는 금 한 줄, 바깥 흙에 덩이 둘
# 출력: ../floor_tile_red.png · ../wall_tile_red.png · ../wall_face_red.png. 실행: Windows Python(Pillow · numpy) make_layer_earth.py
import colorsys
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..', '..', 'Shop')
OUT = os.path.join(HERE, '..')
NAMES = ['floor_tile', 'wall_tile', 'wall_face']
RED = dict(hue=0.03, sat=1.2, val=0.92)
ROOT = (222, 178, 128)      # 뿌리 밝은 쪽
ROOT_D = (164, 112, 76)     # 뿌리 그늘
ROOT_F = (190, 132, 100)    # 바닥 뿌리


def shift(rgb, hue, sat, val):
    h, s, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    r, g, b = colorsys.hsv_to_rgb(hue, min(1, s * sat), min(1, v * val))
    return tuple(int(round(c * 255)) for c in (r, g, b))


def recolor(a, **tone):
    out = a.copy()
    for c in np.unique(a[a[..., 3] > 0][:, :3], axis=0):
        out[(a[..., :3] == c).all(-1), :3] = shift(tuple(int(x) for x in c), **tone)
    return out


def put(a, x, y, col):
    h, w = a.shape[:2]
    a[y % h, x % w, :3] = col


def line(a, pts, col):
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for k in range(n + 1):
            put(a, round(x0 + (x1 - x0) * k / n), round(y0 + (y1 - y0) * k / n), col)


def roots(face, floor, earth):
    for x, length, bend in ((6, 11, 1), (23, 7, -1), (38, 14, 1), (53, 9, -1)):
        pts = [(x, 6), (x, 6 + length // 2), (x + bend, 6 + length)]
        line(face, pts, ROOT)
        line(face, [(p[0] + 1, p[1]) for p in pts], ROOT_D)
        line(face, [(x, 6 + length // 2), (x - 2 * bend, 6 + length // 2 + 3)], ROOT)
    for pts in (((4, 9), (6, 8), (9, 9), (10, 11)), ((20, 24), (22, 22), (25, 22))):
        line(floor, pts, ROOT_F)
    line(earth, [(3, 6), (6, 5), (9, 7)], ROOT_D)
    line(earth, [(19, 21), (21, 24), (24, 25)], ROOT_D)


def clay(face, floor, earth, dark, light):
    for cx, cy in ((8, 20), (24, 6)):
        for dx, dy in ((0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (3, 1), (1, 2), (2, 2)):
            put(floor, cx + dx, cy + dy, light)
    line(floor, [(14, 2), (15, 6), (13, 10), (16, 14)], dark)
    line(floor, [(28, 16), (30, 19), (29, 23)], dark)
    for x0 in (0, 32):
        line(face, [(x0 + 2, 24), (x0 + 9, 24), (x0 + 12, 25), (x0 + 20, 25)], dark)
    for cx, cy in ((5, 5), (21, 26)):
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (1, 2)):
            put(earth, cx + dx, cy + dy, light)


def main():
    t = {n: recolor(np.asarray(Image.open(os.path.join(SHOP, n + '.png')).convert('RGBA')), **RED) for n in NAMES}
    # 점토 색은 붉은 바닥의 가장 어두운 색(금) · 두 번째로 밝은 색(덩이)
    fl = np.unique(t['floor_tile'][..., :3].reshape(-1, 3), axis=0)
    order = np.argsort(fl.sum(1))
    dark, light = tuple(fl[order[0]]), tuple(fl[order[-2]])
    roots(t['wall_face'], t['floor_tile'], t['wall_tile'])
    clay(t['wall_face'], t['floor_tile'], t['wall_tile'], dark, light)
    for n in NAMES:
        Image.fromarray(t[n]).save(os.path.join(OUT, n + '_red.png'))
        print(n + '_red', t[n].shape[1], 'x', t[n].shape[0], 'colors', len(np.unique(t[n][..., :3].reshape(-1, 3), axis=0)))


if __name__ == '__main__':
    main()
