# -*- coding: utf-8 -*-
# 가방 단추 아이콘(설계 55, 창고 · 유물 탭을 한 단추로). 18×18px(UI 1px = 화면 4px, 둥근 네모 메뉴 단추 안 72×72).
# 2026-10-09 사용자 선택 A 「가죽 배낭」: 덮개 + 금빛 버클 + 앞주머니 + 손잡이, 왼쪽 밝게 · 오른쪽 어둡게. 버린 시안: B 사업가 서류 가방 · C 보물 자루.
# 칸 무늬 + 바깥 1칸 진갈색 외곽선(창고 · 유물 · 점원 아이콘과 같은 결). 창고 자루(icon_storage)는 편집 안내 · 가방 창 머리에서 그대로 쓴다.
# 출력 ../icon_bag.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_icon_bag.py → 메뉴 ZooTycoon/Bake/Import UI Sprites
import json
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
INK = (0x34, 0x20, 0x20)
PAL = {'L': (0xC0, 0x7E, 0x4E), 'l': (0xDC, 0xA0, 0x6C), 'd': (0x8C, 0x52, 0x34),   # 가죽 · 밝은 면 · 그늘
       'g': (0xE8, 0xC4, 0x68), 'G': (0xB8, 0x8C, 0x40)}                              # 버클(유물 띠와 같은 금빛)
ROWS = [
    "......dddd......",
    ".....d....d.....",
    "....lllLLLLd....",
    "...llLLLLLLLd...",
    "..lLLLLLLLLLLd..",
    "..lLLLLLLLLLLd..",
    "..dddddggddddd..",
    "..lLLLLgGLLLLd..",
    "..lLLLLLLLLLLd..",
    "..lLddddddddLd..",
    "..lLdlllllldLd..",
    "..lLdlllllldLd..",
    "..lLdLLLLLLdLd..",
    "..lLddddddddLd..",
    "..dLLLLLLLLLLd..",
    "...dddddddddd...",
]

a = np.zeros((18, 18, 4), np.uint8)
for y, row in enumerate(ROWS):
    for x, ch in enumerate(row):
        if ch != '.':
            a[y + 1, x + 1] = PAL[ch] + (255,)
# 가운데로(위아래 · 좌우 빈 줄을 고르게) 뒤 바깥 1칸 외곽선
ys, xs = np.nonzero(a[..., 3])
a = np.roll(a, ((18 - (ys.max() + 1 + ys.min())) // 2, (18 - (xs.max() + 1 + xs.min())) // 2), (0, 1))
f = a[..., 3] > 0
g = f.copy()
g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
a[g & ~f] = INK + (255,)
Image.fromarray(a).save(os.path.join(OUT, 'icon_bag.png'))

slices = json.load(open(os.path.join(OUT, 'ui_slices.json'), encoding='utf-8'))
slices['icon_bag'] = {'w': 18, 'h': 18, 'border': None}
json.dump(slices, open(os.path.join(OUT, 'ui_slices.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('icon_bag 18x18')
