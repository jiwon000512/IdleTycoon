# -*- coding: utf-8 -*-
# 할 일 카드 받기 때 쾅 찍히는 「완료」 도장의 테(설계 54, 2026-10-09 사용자 시안 A 「동그라미」): 지름 30칸, 바깥 2칸 + 1칸 띄고 안 1칸.
# 글자는 조각에 넣지 않는다(도장 Stamp의 TMP 「완료」 굵은 11 · 66×44px가 안에 들어간다). 색은 글자와 같은 부족 빨강 #A64B3C, Image 색은 흰색 그대로.
# 버린 시안: B 네모(28×20칸) · C 알약 + 잉크 결(28×18칸).
# 사용: make_quest_stamp.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
D = 30
RED = (166, 75, 60)


def disc(r, inset):
    # 칸 가운데(+0.5)에서 잰 원: 가장자리에서 inset칸 들어간 원 안
    ys, xs = np.mgrid[0:D, 0:D] + 0.5
    rr = max(r - inset, 0.5)
    cx = np.clip(xs, inset + rr, D - inset - rr)
    cy = np.clip(ys, inset + rr, D - inset - rr)
    return ((xs - cx) ** 2 + (ys - cy) ** 2 <= rr ** 2) & (xs > inset) & (xs < D - inset) & (ys > inset) & (ys < D - inset)


r = D / 2
ring = (disc(r, 0) & ~disc(r, 2)) | (disc(r, 3) & ~disc(r, 4))
a = np.zeros((D, D, 4), np.uint8)
a[ring] = RED + (255,)
Image.fromarray(a).save(f'{OUT}quest_stamp.png')

slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
slices['quest_stamp'] = {'w': D, 'h': D, 'border': None}
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('quest_stamp', D, 'x', D)
