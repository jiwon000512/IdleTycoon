# -*- coding: utf-8 -*-
# 굴 격자 설계 v0.5: 굴 그림은 실행 중 BurrowPainter가 칸(1px = 한 칸, PPU 40) 해상도로 그린다. 여기서는 그 재료만 뽑는다.
# floor_tile(바닥 32칸 주기) · slot_empty(빈 자리 점선 44×28칸). wall_tile·wall_face·arch는 make_burrow_a.py(굴 환경 A2, 2026-09-24)
import numpy as np
from PIL import Image
tile = np.asarray(Image.open('shop_raw/floor_b_px.png').convert('RGBA'))[::2, ::2]
Image.fromarray(tile).save('../floor_tile.png')
w, h = 44, 28; g = np.zeros((h, w, 4), np.uint8); OUT = (52, 32, 32, 200)
for x in range(w):
    if (x // 3) % 2 == 0: g[0, x] = OUT; g[h - 1, x] = OUT
for y in range(h):
    if (y // 3) % 2 == 0: g[y, 0] = OUT; g[y, w - 1] = OUT
Image.fromarray(g).save('../slot_empty.png')
print('floor', tile.shape, 'slot', g.shape)
