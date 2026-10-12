# -*- coding: utf-8 -*-
# 공용 레드닷(설계 57, 2026-10-12 사용자 시안 A 「납작」): 단추 오른쪽 위 빨간 점. 기하 도형이라 칸 무늬로 그린다(Codex 없음).
# red_dot(점만) 7×7칸 = 28 · red_dot_count(수가 든 점, 굵은 11 글자 44) 14×14칸 = 56. 팔레트 「부족」 #A64B3C 바탕 + 외곽선 #342020, 9-slice 없음.
# 버린 시안: B 빛 한 점(왼쪽 위 밝은 점 + 오른쪽 아래 그늘) · C 크림 테.
# 사용: make_red_dot.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
INK, RED = (0x34, 0x20, 0x20), (0xA6, 0x4B, 0x3C)


def dot(n):
    # 칸 가운데가 반지름(n/2 - 0.3) 안이면 점, 바깥과 맞닿은 칸이 외곽선
    c = (n - 1) / 2
    m = np.array([[(x - c) ** 2 + (y - c) ** 2 <= (n / 2 - 0.3) ** 2 for x in range(n)] for y in range(n)])
    p = np.pad(m, 1)
    edge = m & ~(p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:])
    a = np.zeros((n, n, 4), np.uint8)
    a[m] = RED + (255,)
    a[edge] = INK + (255,)
    return a


slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
for name, n in (('red_dot', 7), ('red_dot_count', 14)):
    Image.fromarray(dot(n)).save(f'{OUT}{name}.png')
    slices[name] = {'w': n, 'h': n, 'border': None}
    print(name, n, 'x', n)
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
