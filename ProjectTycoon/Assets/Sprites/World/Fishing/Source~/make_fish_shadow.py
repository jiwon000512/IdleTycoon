# 물속 물고기 그림자(2026-10-05 사용자: 위에서 본 물고기는 아트 규칙(앞쪽 위에서 본 모습)에 어긋나니 동물의 숲처럼 크기만 다른 어두운 그림자로, 시안 A 「타원」).
#   모든 어종이 같은 모양 · 같은 색이고 길이만 다르다(월척은 게임이 2배). 기하 도형이라 칸 무늬로 그린다(한 칸 2px).
# 출력: ../fish_<minnow|crucian|catfish|boss>_swim.png(PPU 80, 가운데 피벗). 낚이면 게임이 창고 아이콘(옆모습, make_fish.py)으로 바꾼다
# 사용: python make_fish_shadow.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
LENGTH = {'minnow': 16, 'crucian': 22, 'catfish': 30, 'boss': 60}   # 칸
SHADOW = (22, 52, 54, 200)


def shadow(L):
    H = max(5, int(round(L * 0.42)))
    y, x = np.mgrid[0:H, 0:L]
    inside = ((x - (L - 1) / 2) / (L / 2)) ** 2 + ((y - (H - 1) / 2) / (H / 2)) ** 2 <= 1.0
    a = np.zeros((H, L, 4), np.uint8)
    a[inside] = SHADOW
    return a


if __name__ == '__main__':
    for name, L in LENGTH.items():
        a = shadow(L)
        Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', 'fish_%s_swim.png' % name))
        print('fish_%s_swim.png %d x %d 칸' % (name, a.shape[1], a.shape[0]))
