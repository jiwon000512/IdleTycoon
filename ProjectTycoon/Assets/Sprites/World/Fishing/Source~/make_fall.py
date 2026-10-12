# -*- coding: utf-8 -*-
# 층 사이 폭포 띠(설계 58, 2026-10-12 사용자 시안 A 「물 커튼」): 아래 층이 열린 층의 물 아래 절벽(높이 1유닛 = 40텍셀, PPU 40)에
# 물 폭만큼 가로 반복(Tiled)하는 떨어지는 물. 칸 무늬로 그린다(재질). 물빛 · 거품은 지금 물 타일(make_stream.py)과 같은 색.
#   열마다 고정 씨앗으로 고른 주기(12 · 14 · 16)와 시작점으로 긴 밝은 결(7칸) · 짙은 결(6칸)이 내려가고, 네 장이 두 칸씩 아래로 움직인다.
#   맨 위 선(진갈색) · 거품 줄 · 밝은 줄 = 위 층 물이 넘는 턱, 맨 아래 2~5칸 물보라(열마다 높이가 다름).
#   가로 32텍셀(물 타일과 같은 폭) 이음새 없음. 버린 시안: B 흙 절벽 + 물줄기 · C 돌 계단.
# 출력: ../fall_0~3.png(32×40, PPU 40, 피벗 가운데, FullRect). 사용: python make_fall.py
import os
import random
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
H, W = 40, 32
WATER, DEEP, LIGHT, SHINE, LINE = (60, 138, 126), (48, 120, 114), (110, 182, 170), (204, 234, 222), (52, 32, 32)
FOAM = (226, 242, 236)


def frame(f, seed=3):
    rnd = random.Random(seed)
    a = np.zeros((H, W, 3), np.uint8)
    a[:] = WATER
    for x in range(W):
        period, phase, kind = rnd.choice((12, 14, 16)), rnd.randrange(16), rnd.random()
        for y in range(H):
            t = (y - f * 2 + phase) % period
            if kind < 0.45 and t < 7:
                a[y, x] = LIGHT
            elif kind > 0.7 and t < 6:
                a[y, x] = DEEP
    a[0] = LINE
    a[1] = FOAM
    a[2] = LIGHT
    for x in range(W):
        h = 2 + (x * 7 + f * 3) % 4
        for y in range(H - h, H):
            a[y, x] = FOAM if (x + y + f) % 3 else SHINE
    out = np.zeros((H, W, 4), np.uint8)
    out[..., :3] = a
    out[..., 3] = 255
    return out


if __name__ == '__main__':
    for f in range(4):
        Image.fromarray(frame(f)).save(os.path.join(HERE, '..', 'fall_%d.png' % f))
    print('fall_0~3.png %d x %d' % (W, H))
