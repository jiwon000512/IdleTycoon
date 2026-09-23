# -*- coding: utf-8 -*-
# 상호작용 버튼 + 행동 아이콘 실제 아트(시안 B 흙 테 + 크림 판, 2026-09-23 사용자 선택). AI 없이 칸 단위로 그린다.
# 1 px = 1 UI px(PPU 25). 버튼 44: 조이스틱 받침과 같은 흙 테 + 크림 판. 아이콘 18: 버튼 행동(actions.json manual)만 — 열기·파기(꺼내기는 2026-09-23 auto로 바뀌어 아이콘 삭제).
# 비활성은 코드 틴트(버튼 × 0.65, 아이콘 40%)라 따로 그리지 않는다. 칠한 뒤 바깥 1칸 진갈색 외곽선.
# 사용: make_act_button.py (이 폴더에서) → ../btn_act.png, Resources/Sprites/Actions/<id>.png → 메뉴 ZooTycoon/Bake/Import UI Sprites → Bake/UI
import os
import numpy as np
from PIL import Image, ImageDraw

os.chdir(os.path.dirname(os.path.abspath(__file__)))
INK = (0x34, 0x20, 0x20)
CREAM, LIGHT, TAN = (0xFB, 0xF4, 0xE6), (0xF0, 0xE4, 0xD8), (0xD9, 0xC8, 0xB4)
RIM, RIM_L = (0xC8, 0xAA, 0x96), (0xE2, 0xCC, 0xB8)
WOOD, WOOD_D = (0xC9, 0x9A, 0x6B), (0xA0, 0x72, 0x4C)
STEEL, STEEL_D = (0xD8, 0xD2, 0xCC), (0xA8, 0xA0, 0x98)

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


def button():
    a = np.zeros((44, 44, 4), np.uint8)
    paint(a, disc(44, 20.5), RIM)
    paint(a, disc(44, 20.5) & ~disc(44, 20.5, 1, 1), RIM_L)    # 위·왼쪽 밝은 테
    paint(a, disc(44, 14.5), LIGHT)
    paint(a, disc(44, 14.5) & ~disc(44, 14.5, -1, -1), TAN)    # 판 아래·오른쪽 그늘
    return outline(a)


def open_():  # 돋보기
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.line([(10, 10), (15, 15)], fill=WOOD_D + (255,), width=3)
    a = np.asarray(im).copy()
    paint(a, disc(18, 5.5, -2, -2), WOOD_D)
    paint(a, disc(18, 3.8, -2, -2), LIGHT)
    paint(a, disc(18, 1.2, -4, -4), CREAM)
    return outline(a)


def dig():  # 삽
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.line([(8, 9), (14, 3)], fill=WOOD + (255,), width=2)
    d.line([(13, 4), (15, 2)], fill=WOOD_D + (255,), width=2)    # 손잡이 끝
    d.polygon([(2, 11), (6, 7), (10, 11), (6, 15), (3, 15), (2, 14)], fill=STEEL + (255,))
    d.polygon([(3, 15), (6, 15), (10, 11), (9, 10)], fill=STEEL_D + (255,))
    return outline(np.asarray(im).copy())


Image.fromarray(button()).save('../btn_act.png')
for action_id, draw in [('open', open_), ('dig', dig)]:
    Image.fromarray(draw()).save(f'../../../Resources/Sprites/Actions/{action_id}.png')
