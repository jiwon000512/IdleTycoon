# -*- coding: utf-8 -*-
# 할 일 카드 틀(설계 54, 2026-10-09 사용자 테두리 시안 B 「주황 한 칸」): 팝업 틀 panel(바깥선 · 밝은 주황 · 주황 2칸 · 안쪽 선 = 5칸 + 징)이
# 「너무 두꺼운 느낌」이라 진갈색 바깥선 1칸 + 주황 띠 1칸 + 크림, 둥근 모서리 3칸으로 칸 무늬를 직접 그린다. 버린 시안: A 공용 row · C 금빛 띠.
# 16×16칸, 9-slice 6칸(모서리 6×6칸 안에 둥근 끝 · 선 · 띠가 다 들어간다). 안내 중 빛줄기는 이 띠(가장자리에서 1칸) 위를 돈다.
# 사용: make_quest_card.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
N = 16
INK, BAND, CREAM = (52, 32, 32), (219, 119, 70), (240, 228, 216)
# 왼쪽 위 모서리 6×6칸(나머지 셋은 뒤집기): k 바깥선 · o 띠 · c 크림 · . 투명
CORNER = ['...kkk',
          '..kooo',
          '.kocc c'.replace(' ', ''),
          'kocccc',
          'kocccc',
          'kocccc']
pal = {'k': INK, 'o': BAND, 'c': CREAM}

a = np.zeros((N, N, 4), np.uint8)
a[:, :] = CREAM + (255,)
a[0, :] = a[-1, :] = a[:, 0] = a[:, -1] = INK + (255,)
a[1, 1:-1] = a[-2, 1:-1] = a[1:-1, 1] = a[1:-1, -2] = BAND + (255,)
for fy in (False, True):
    for fx in (False, True):
        for y, row in enumerate(CORNER):
            for x, ch in enumerate(row):
                a[N - 1 - y if fy else y, N - 1 - x if fx else x] = (0, 0, 0, 0) if ch == '.' else pal[ch] + (255,)
Image.fromarray(a).save(f'{OUT}quest_card.png')

slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
slices['quest_card'] = {'w': N, 'h': N, 'border': [6, 6, 6, 6]}
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('quest_card', N, 'x', N)
