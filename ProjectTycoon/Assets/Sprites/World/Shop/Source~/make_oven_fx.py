# -*- coding: utf-8 -*-
# 오븐 연출(2026-09-23 사용자 선택: 시안 C 불빛 일렁임 + 굴뚝 연기, 빵 아이콘 없음). AI 없이 칸 단위(1칸 = 2px)로 그린다.
# 입력: oven_lit.png·oven_2_lit.png(아궁이에 불이 그려진 원본). 출력(../):
#   oven.png·oven_2.png        아궁이를 식힌 몸통(빈 오븐·다 구움 공용). 피벗 하단 가운데
#   oven_fire_0~3·oven_2_fire_0~3  굽는 중 아궁이 불빛 + 불똥, 몸통과 같은 캔버스(몸통 위에 겹침), 8fps
#   smoke_gray_00~11           굽는 중 굴뚝 회색 연기, 40×28칸, 피벗 하단 가운데 = 굴뚝 입구, 먼저 오른쪽으로 빠져 타이머를 비켜 오름, 8fps
#   smoke_white_00~11          다 구움 굴뚝 흰 연기(불은 꺼짐), 같은 캔버스, 더 크고 많음, 8fps
import math, os, random
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
R, O, Y, W = (0xB8, 0x40, 0x28), (0xF0, 0x84, 0x38), (0xF8, 0xC8, 0x50), (0xFF, 0xEC, 0xA8)
GLOW = (0x6C, 0x30, 0x24)
GRAY, GRAY_D, GRAY_LINE = (0xB8, 0xB0, 0xAC), (0x98, 0x90, 0x8C), (0x70, 0x66, 0x62)
WHITE, WHITE_D, WHITE_LINE = (0xF8, 0xF2, 0xEA), (0xDC, 0xD2, 0xC8), (0x9C, 0x8C, 0x84)
# 이름, 아궁이 상자(x0,x1,y0,y1 칸), 불 색, 아궁이 안 어두운 색
OVENS = [('oven', (20, 39, 29, 43), [(252, 144, 72)], (48, 24, 24)),
         ('oven_2', (20, 40, 35, 48), [(252, 156, 60), (192, 96, 36)], (48, 36, 36))]


def up(a):
    return Image.fromarray(a).resize((a.shape[1] * 2, a.shape[0] * 2), Image.NEAREST)


def mouth(name, box, fire, dark):
    a = np.asarray(Image.open(name + '_lit.png').convert('RGBA'))[::2, ::2].copy()
    x0, x1, y0, y1 = box
    reg = np.zeros(a.shape[:2], bool)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = tuple(int(v) for v in a[y, x, :3])
            reg[y, x] = a[y, x, 3] > 0 and (c in fire or c == dark)
    fill = reg.copy()    # 가장자리 1칸은 아궁이 외곽선으로 남긴다
    fill[1:] &= reg[:-1]; fill[:-1] &= reg[1:]; fill[:, 1:] &= reg[:, :-1]; fill[:, :-1] &= reg[:, 1:]
    cold = a.copy()
    cold[reg, :3] = dark
    return cold, fill


def fire(fill, t):
    # 아래가 뜨겁고 위로 식는 띠가 열마다 출렁인다 + 불똥 3개가 올라감
    L = np.zeros(fill.shape + (4,), np.uint8)
    ys, xs = np.nonzero(fill)
    floor, top, xl, xr = ys.max(), ys.min(), xs.min(), xs.max()
    h = floor - top + 1
    for y, x in zip(ys, xs):
        lvl = 1 - ((floor - y) / h + 0.12 * math.sin(x * .9 + t * math.pi / 2))
        col = Y if lvl > .8 else O if lvl > .55 else R if lvl > .3 else GLOW if lvl > .05 else None
        if col:
            L[y, x] = col + (255,)
    rnd = random.Random(3)
    for i in range(3):
        x = rnd.randint(xl + 2, xr - 2)
        y = floor - 3 - ((t * 2 + i * 3) % 9)
        if fill[y, x]:
            L[y, x] = W + (255,)
    return L


def smoke(t, count, rmax, col, col_d, line, early=1.0):
    # 12프레임 한 바퀴. 퍼프가 굴뚝 입구에서 오른쪽 위로 커지며 올라간다
    L = np.zeros((28, 40, 4), np.uint8)
    for i in range(count):
        p = ((t + i * 12 / count) % 12) / 12
        y = 26 - p * 24
        x = 19.5 + 15 * p ** early + math.sin(p * 5)    # early < 1이면 먼저 옆으로 빠진 뒤 오른다
        r = 1 + p * (rmax - 1)
        for yy in range(28):
            for xx in range(40):
                if (yy - y) ** 2 + (xx - x) ** 2 <= r * r + .5:
                    L[yy, xx] = (col if yy < y + r / 3 else col_d) + (255,)
    f = L[..., 3] > 0
    g = f.copy(); g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    L[g & ~f] = line + (255,)
    return L


for name, box, fire_cols, dark in OVENS:
    cold, fill = mouth(name, box, fire_cols, dark)
    up(cold).save(f'../{name}.png')
    for t in range(4):
        up(fire(fill, t)).save(f'../{name}_fire_{t}.png')
for t in range(12):
    # 회색은 타이머(굴뚝 위 3~23칸, 좌우 10칸)를 비켜 가도록 먼저 옆으로 빠진다
    up(smoke(t, 2, 3.5, GRAY, GRAY_D, GRAY_LINE, .45)).save(f'../smoke_gray_{t:02d}.png')
    up(smoke(t, 3, 4.5, WHITE, WHITE_D, WHITE_LINE)).save(f'../smoke_white_{t:02d}.png')
