# -*- coding: utf-8 -*-
# 결제 코인 시안(2026-09-23). AI 없이 칸 단위(1칸 = 2px, 가게 PPU 80)로 그린다.
# 앞면 18칸 원 + 아래 두께 2칸(위 30도에서 본 동전). 가운데 각인만 다르다:
#   coin_a.png 웜뱃 발바닥 · coin_b.png 식빵 · coin_c.png 엽전(가운데 네모 구멍)
# 선택 A(사용자 「A인데 발바닥이 좀 더 크게」 → 9×8칸에서 11×10칸) = ../coin.png.
import os
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
LINE = (0x34, 0x20, 0x20)
HI, LIGHT, GOLD, DARK, EDGE = (0xFF, 0xF4, 0xC8), (0xF8, 0xD8, 0x78), (0xE8, 0xB0, 0x48), (0xC0, 0x80, 0x38), (0x8C, 0x54, 0x28)
D, T = 18, 2          # 앞면 지름, 두께(칸)
W, H = D + 2, D + T + 2
# 각인은 어두운 금색(DARK) 윤곽 + 밝은 아래쪽 한 줄(눌린 자국)
PAW = ['....x.x....',
       '.x.xx.xx.x.',
       'xx.xx.xx.xx',
       'xx.......xx',
       '...xxxxx...',
       '..xxxxxxx..',
       '.xxxxxxxxx.',
       '.xxxxxxxxx.',
       '..xxxxxxx..',
       '...xx.xx...']
BREAD = ['..xxxxx..',
         '.xx...xx.',
         'x.......x',
         'x.......x',
         '.x.....x.',
         '.x.....x.',
         '.x.....x.',
         '.xxxxxxx.']


def disc(cy):
    ys, xs = np.mgrid[0:H, 0:W]
    return (xs - (W - 1) / 2) ** 2 + (ys - cy) ** 2 <= (D / 2) ** 2


def coin(mark):
    a = np.zeros((H, W, 4), np.uint8)
    top_c = 1 + (D - 1) / 2
    face = disc(top_c)
    body = face.copy()
    for t in range(1, T + 1):
        body |= disc(top_c + t)
    a[body] = EDGE + (255,)                     # 두께
    a[face] = GOLD + (255,)
    # 앞면 테 1칸: 왼쪽 위 절반은 밝게, 오른쪽 아래 절반은 어둡게(빗면)
    er = face.copy()
    for dy, dx in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        er &= np.roll(face, (dy, dx), (0, 1))
    ys, xs = np.mgrid[0:H, 0:W]
    ring = face & ~er
    upper = (xs - (W - 1) / 2) + (ys - top_c) < 0
    a[ring & upper] = LIGHT + (255,)
    a[ring & ~upper] = DARK + (255,)
    # 왼쪽 위 하이라이트
    for y, x in [(4, 6), (4, 7), (5, 5), (6, 5), (5, 6)]:
        a[y, x] = HI + (255,)
    # 외곽선
    sil = a[..., 3] > 0
    out = np.zeros_like(sil)
    out[1:] |= sil[:-1]; out[:-1] |= sil[1:]; out[:, 1:] |= sil[:, :-1]; out[:, :-1] |= sil[:, 1:]
    a[out & ~sil] = LINE + (255,)
    mark(a, int(round(top_c)))
    return a


def stamp(rows):
    def f(a, cy):
        h, w = len(rows), len(rows[0])
        y0, x0 = cy - h // 2, (W - w) // 2
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch == 'x':
                    a[y0 + y, x0 + x] = DARK + (255,)
                    if all(r[x] != 'x' for r in rows[y + 1:]):
                        a[y0 + y + 1, x0 + x] = LIGHT + (255,)
    return f


def hole(a, cy):
    # 엽전: 가운데 4×4칸 네모 구멍(투명) + 구멍 테두리 어두운 금색, 아래 변은 밝게
    x0, y0 = W // 2 - 2, cy - 2
    a[y0 - 1:y0 + 5, x0 - 1:x0 + 5] = DARK + (255,)
    a[y0 + 4, x0 - 1:x0 + 5] = LIGHT + (255,)
    a[y0:y0 + 4, x0:x0 + 4] = LINE + (255,)
    a[y0 + 1:y0 + 3, x0 + 1:x0 + 3] = 0


def up(a):
    return Image.fromarray(a).resize((a.shape[1] * 2, a.shape[0] * 2), Image.NEAREST)


up(coin(stamp(PAW))).save('coin_a.png')
up(coin(stamp(PAW))).save('../coin.png')
up(coin(stamp(BREAD))).save('coin_b.png')
up(coin(hole)).save('coin_c.png')
