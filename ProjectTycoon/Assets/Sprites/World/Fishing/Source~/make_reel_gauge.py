# -*- coding: utf-8 -*-
# 낚시 감기 게이지(칸 편집, AI 없음): 대가 감는 물고기 머리 위. 한 칸 = 2px(PPU 80). 진행 16단계(0% … 100%)를 미리 그려 끼운다(늘이지 않음, 오븐 타이머와 같은 방식)
# 2026-10-06 사용자 선택 B 「릴」(시안 A 물빛 막대 · C 물방울 셋 중). 크림 판 테에 물빛이 12시부터 시계 방향으로 차고, 오른쪽에 손잡이
# 색: UI 외곽선 · 크림 판(오븐 · 농장 타이머와 같음) + 낚시터 물빛(water_tile · whirl과 같은 색)
# 20×15칸(40×30px), 피벗 = 릴 가운데(왼쪽에서 15px · 아래에서 15px → (0.375, 0.5))
# 사용: make_reel_gauge.py [<출력 폴더>=..] → reel_gauge_00~15.png
import os, sys
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
N = 16
H = lambda h: np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)] + [255], np.uint8)
LINE = H('#342020')
PANEL, PANEL_HI, PANEL_LO = H('#F0E4D8'), H('#FBF4E6'), H('#D9C8B4')
AQUA, AQUA_HI, AQUA_LO = H('#6EB6AA'), H('#CCEADE'), H('#3C8A7E')


def outline(mask):
    # 마스크 바깥 1칸 테두리(4방향)
    m = np.pad(mask, 1)
    grown = m.copy()
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        grown |= np.roll(np.roll(m, dy, 0), dx, 1)
    return (grown & ~m)[1:-1, 1:-1]


def reel(p):
    S, W = 15, 20
    c = np.zeros((S, W, 4), np.uint8)
    y, x = np.mgrid[0:S, 0:W]
    d2 = (x - 7) ** 2 + (y - 7) ** 2
    face = d2 <= 6.3 ** 2
    hub = d2 <= 1.5 ** 2
    hub_line = outline(hub)
    ring = face & ~hub & ~hub_line
    ang = (np.degrees(np.arctan2(x - 7, -(y - 7))) + 360) % 360
    # 손잡이: 축 높이에서 오른쪽으로 팔 + 둥근 손잡이 알
    arm = np.zeros((S, W), bool); arm[7, 14:17] = True
    knob = np.zeros((S, W), bool); knob[5:10, 16:19] = True
    knob[5, 16] = knob[5, 18] = knob[9, 16] = knob[9, 18] = False
    c[outline(face | arm | knob)] = LINE
    c[arm] = PANEL_LO; c[knob] = PANEL; c[6, 17] = PANEL_HI
    c[ring] = PANEL
    c[ring & (y >= 10)] = PANEL_LO
    c[2, 5:9][ring[2, 5:9]] = PANEL_HI
    fill = ring & (ang < 360 * p + (0.01 if p >= 1 else 0))
    c[fill] = AQUA
    c[fill & (d2 > 5.0 ** 2) & (ang < 90)] = AQUA_HI
    c[fill & (d2 <= 3.6 ** 2)] = AQUA_LO
    c[hub_line] = LINE
    c[hub] = PANEL_LO
    c[7, 7] = LINE
    return c


if __name__ == '__main__':
    for i in range(N):
        c = reel(i / (N - 1))
        Image.fromarray(np.repeat(np.repeat(c, 2, 0), 2, 1), 'RGBA').save(os.path.join(OUT, 'reel_gauge_%02d.png' % i))
