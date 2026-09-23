# -*- coding: utf-8 -*-
# 조이스틱 실제 아트(시안 C 굴 구멍 + 웜뱃 발바닥, 2026-09-23 사용자 선택). AI 없이 칸 단위로 그린다.
# 1 px = 1 UI px(PPU 25). 받침 44: 밝은 흙 테 + 어두운 구멍. 손잡이 20: 웜뱃 털 + 발바닥. 칠한 뒤 바깥 1칸 진갈색 외곽선.
# 사용: make_joystick.py (이 폴더에서) → ../joystick_base.png·joystick_knob.png → 메뉴 ZooTycoon/Bake/Import UI Sprites → Bake/UI
import os
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
INK = (0x34, 0x20, 0x20)
RIM, RIM_L = (0xC8, 0xAA, 0x96), (0xE2, 0xCC, 0xB8)
HOLE, HOLE_D = (0x5A, 0x3A, 0x32), (0x44, 0x2A, 0x26)
FUR, FUR_D = (0xC0, 0xA8, 0x90), (0x9C, 0x78, 0x6C)
PAD, PAD_L = (0x84, 0x60, 0x54), (0xA8, 0x6C, 0x60)


def disc(n, r, dx=0, dy=0):
    y, x = np.mgrid[0:n, 0:n] - (n - 1) / 2
    return (x - dx) ** 2 + (y - dy) ** 2 <= (r + 0.3) ** 2


def paint(a, m, c):
    a[m] = c + (255,)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def base():
    a = np.zeros((44, 44, 4), np.uint8)
    paint(a, disc(44, 20.5), RIM)
    paint(a, disc(44, 20.5) & ~disc(44, 20.5, 1, 1), RIM_L)    # 위·왼쪽 밝은 테
    paint(a, disc(44, 14), HOLE)
    paint(a, disc(44, 14) & ~disc(44, 14, 0, 2), HOLE_D)       # 구멍 위 가장자리 그늘
    for x, y in [(-16, -8), (15, 5), (-6, 16), (9, -15), (17, -6), (-14, 10)]:  # 자갈
        a[21 + y, 21 + x] = RIM_L + (255,)
    return outline(a)


def knob():
    a = np.zeros((20, 20, 4), np.uint8)
    paint(a, disc(20, 8.5), FUR)
    paint(a, disc(20, 8.5) & ~disc(20, 8.5, -1, -1), FUR_D)
    paint(a, disc(20, 3.6, 0, 2), PAD)                          # 큰 발바닥
    for x, y in [(-4.5, -3), (-1.5, -5), (1.5, -5), (4.5, -3)]:   # 발가락 넷
        paint(a, disc(20, 1.2, x, y), PAD)
    paint(a, disc(20, 1, -1, 1), PAD_L)
    return outline(a)


Image.fromarray(base()).save('../joystick_base.png')
Image.fromarray(knob()).save('../joystick_knob.png')
