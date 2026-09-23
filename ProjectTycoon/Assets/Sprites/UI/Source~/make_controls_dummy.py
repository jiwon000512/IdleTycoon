# -*- coding: utf-8 -*-
# 설계 09 더미: 상호작용 버튼 바탕, 행동 아이콘 4종(꺼내기·채우기·파기·열기).
# v0.4: 행동 아이콘은 actions.json icon 경로 = Resources/Sprites/Actions/<행동 id>.png(ui_slices.json에 넣지 않는다)
# 1 px = 1 UI px(화면 4px, PPU 25), UI 규칙 팔레트, 칠한 뒤 바깥 1칸 진갈색 외곽선. 실제 아트는 시안 절차로 같은 경로를 덮어쓴다.
# 사용: make_controls_dummy.py (이 폴더에서) → ../<이름>.png·ui_slices.json, 아이콘은 Resources/Sprites/Actions → 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image, ImageDraw

os.chdir(os.path.dirname(os.path.abspath(__file__)))
INK = (0x34, 0x20, 0x20)
CREAM = (0xFB, 0xF4, 0xE6)
LIGHT = (0xF0, 0xE4, 0xD8)
TAN = (0xD9, 0xC8, 0xB4)
ORANGE = (0xD8, 0x78, 0x48)
DARK = (0xB8, 0x5E, 0x38)
YELLOW = (0xF2, 0xC1, 0x4E)
WOOD = (0xC9, 0x9A, 0x6B)


def canvas(size):
    return Image.new('RGBA', (size, size), (0, 0, 0, 0))


def outline(img):
    a = np.asarray(img).copy()
    filled = a[..., 3] > 0
    grow = filled.copy()
    grow[1:] |= filled[:-1]; grow[:-1] |= filled[1:]
    grow[:, 1:] |= filled[:, :-1]; grow[:, :-1] |= filled[:, 1:]
    edge = grow & ~filled
    a[edge] = INK + (255,)
    return Image.fromarray(a)


def save(name, img, slices):
    img.save(f'../{name}.png')
    slices[name] = {'w': img.width, 'h': img.height, 'border': None}
    print(name, img.size)


def save_action(action_id, img):
    img.save(f'../../../Resources/Sprites/Actions/{action_id}.png')
    print(action_id, img.size)


def icon_take_out():
    img = canvas(18); d = ImageDraw.Draw(img)
    d.rectangle([2, 10, 15, 16], fill=DARK + (255,))
    d.rectangle([4, 12, 13, 14], fill=ORANGE + (255,))
    d.ellipse([4, 1, 13, 6], fill=YELLOW + (255,))
    d.polygon([(8, 5), (9, 5), (11, 8), (6, 8)], fill=CREAM + (255,))
    return outline(img)


def icon_fill():
    img = canvas(18); d = ImageDraw.Draw(img)
    d.rectangle([1, 13, 16, 15], fill=WOOD + (255,))
    d.rectangle([7, 1, 10, 6], fill=CREAM + (255,))
    d.polygon([(3, 7), (14, 7), (9, 11), (8, 11)], fill=CREAM + (255,))
    return outline(img)


def icon_dig():
    img = canvas(18); d = ImageDraw.Draw(img)
    d.line([(2, 2), (9, 9)], fill=WOOD + (255,), width=2)
    d.polygon([(8, 11), (11, 8), (16, 13), (13, 16)], fill=LIGHT + (255,))
    return outline(img)


def icon_open():
    img = canvas(18); d = ImageDraw.Draw(img)
    d.line([(10, 10), (15, 15)], fill=WOOD + (255,), width=3)
    d.ellipse([1, 1, 11, 11], fill=CREAM + (255,))
    d.ellipse([3, 3, 9, 9], fill=LIGHT + (255,))
    return outline(img)


def circle(size, fill, inner=None):
    img = canvas(size); d = ImageDraw.Draw(img)
    d.ellipse([1, 1, size - 2, size - 2], fill=fill + (255,))
    if inner:
        m = size // 5
        d.ellipse([m, m, size - 1 - m, size - 1 - m], fill=inner + (255,))
    return outline(img)


slices = json.load(open('../ui_slices.json', encoding='utf-8'))
save_action('take_out', icon_take_out())
save_action('fill', icon_fill())
save_action('dig', icon_dig())
save_action('open', icon_open())
save('btn_act', circle(44, ORANGE, DARK), slices)  # 조이스틱 받침과 같은 크기
json.dump(slices, open('../ui_slices.json', 'w', encoding='utf-8', newline='\n'), ensure_ascii=False, indent=1)
