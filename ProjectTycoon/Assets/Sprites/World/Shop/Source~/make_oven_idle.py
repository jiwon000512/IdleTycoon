# -*- coding: utf-8 -*-
# 오븐 상태 표시(2026-09-23, 연출 점검 2순위 1). AI 없이 칸 단위(1칸 = 2px)로 그린다. 둘 다 타이머 자리(가운데 피벗, PPU 80)
#   ../oven_empty_mark.png  빈 오븐 = 아래 화살표 11×10칸(시안 B). 코드가 오르내림
#   ../oven_ready_mark.png  다 구움 = 빈 타이머 원(oven_timer_00, 20×20칸) 안에 식빵(시안 A)
# 시안 C(아궁이 속 점선 식빵)는 버림.
import os
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
LINE, CREAM, CREAM_D, DIAL = (0x34, 0x20, 0x20), (0xFB, 0xF4, 0xE6), (0xD9, 0xC8, 0xB4), (0xF0, 0xE4, 0xD8)
BREAD = {'#': (0x30, 0x18, 0x18), 'h': (0xF0, 0xB4, 0x84), 'c': (0xCC, 0x90, 0x60), 'd': (0xA8, 0x6C, 0x3C),
         'e': (0x90, 0x54, 0x30), 'w': (0xF0, 0xD8, 0xC0), 's': (0xE4, 0xCC, 0xB4)}
# 식빵: Resources/Sprites/Shop/Breads/b01.png(정면·위 30도, 2026-09-23)을 2×2칸씩 줄이고 속살 한 줄을 뺀 13×12칸
LOAF = ['...#######...',
        '..#hccccch#..',
        '.#ccccccccc#.',
        '#cdddddddddc#',
        '#ddeeeeeeedd#',
        '#dwsssswsswd#',
        '.#wsssssssw#.',
        '.#sswssssws#.',
        '.#sssssssss#.',
        '.#wssssssss#.',
        '.#ddsssssdd#.',
        '..#########..']
ARROW = ['...#####...',
         '...#ooo#...',
         '...#ooo#...',
         '####ooo####',
         '#ooooooooo#',
         '.#ooooooo#.',
         '..#oosoo#..',
         '...#sss#...',
         '....#s#....',
         '.....#.....']


def up(a):
    return Image.fromarray(a).resize((a.shape[1] * 2, a.shape[0] * 2), Image.NEAREST)


def stamp(a, rows, x0, y0, pal):
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in pal:
                a[y0 + y, x0 + x] = pal[ch] + (255,)


# A: 빈 타이머 원(가운데 점은 식빵이 덮는다)
dial = np.asarray(Image.open('../oven_timer_00.png').convert('RGBA'))[::2, ::2].copy()
stamp(dial, LOAF, 3, 4, BREAD)
up(dial).save('../oven_ready_mark.png')

# B: 아래 화살표
b = np.zeros((10, 11, 4), np.uint8)
stamp(b, ARROW, 0, 0, {'#': LINE, 'o': CREAM, 's': CREAM_D})
up(b).save('../oven_empty_mark.png')

