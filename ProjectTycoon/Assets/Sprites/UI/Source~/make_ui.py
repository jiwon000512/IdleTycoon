# -*- coding: utf-8 -*-
# UI 부품 최종본 생성(UI 규칙(.claude/rules/ui.md)). 원본은 Codex 시안(raw/*.png, 시트 B·버튼 A·아이콘 A)을 make_pixel.py로 격자 강제한 raw/*_px.png.
# 1 px = 1 UI px(화면 4px, PPU 25). 팔레트를 규칙 12색(+나무 2색)으로 스냅하고, 규칙 크기로 자르거나 9-slice 경계를 정해 ../<이름>.png와 ../ui_slices.json에 쓴다.
# 사용: make_ui.py (이 폴더에서). 슬라이스 임포트는 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image
from collections import deque

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
PAL = [(0x34,0x20,0x20),(0xF0,0xE4,0xD8),(0xFB,0xF4,0xE6),(0xD9,0xC8,0xB4),(0xD8,0x78,0x48),(0xF0,0xA0,0x70),(0xB8,0x5E,0x38),
       (0xF2,0xC1,0x4E),(0x3E,0x7A,0x4C),(0xA6,0x4B,0x3C),(0x2E,0x23,0x20),(0x7A,0x6A,0x60),(0x2F,0x4A,0x3E),(0xC9,0x9A,0x6B),(0x8E,0x6E,0x5C),(0x9A,0x8A,0x7C)]
P = np.array(PAL)
# 다른 스크립트(make_joystick·make_act_button)가 넣은 항목을 지우지 않게 기존 표에 덮어쓴다
slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))


def load(name):
    return np.asarray(Image.open(f'raw/{name}.png').convert('RGBA')).copy()


def snap(a):
    m = a[..., 3] > 0
    px = a[m][:, :3].astype(int)
    d = ((px[:, None, :] - P[None, :, :]) ** 2).sum(2)
    a[m, :3] = P[d.argmin(1)]
    a[..., 3] = np.where(m, 255, 0)
    return a


def trim(a):
    ys, xs = np.nonzero(a[..., 3])
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def save(name, a, border=None):
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': list(border) if border else None}
    print(f'{name:24s} {a.shape[1]:3d} x {a.shape[0]:3d}  border={border}')


def components(a, min_cells=20):
    m = a[..., 3] > 0
    lab = np.full(m.shape, -1); out = []
    for sy, sx in zip(*np.nonzero(m)):
        if lab[sy, sx] >= 0:
            continue
        k = len(out); q = deque([(sy, sx)]); lab[sy, sx] = k; ys = []; xs = []
        while q:
            y, x = q.popleft(); ys.append(y); xs.append(x)
            for ny in (y - 1, y, y + 1):
                for nx in (x - 1, x, x + 1):
                    if 0 <= ny < m.shape[0] and 0 <= nx < m.shape[1] and m[ny, nx] and lab[ny, nx] < 0:
                        lab[ny, nx] = k; q.append((ny, nx))
        if len(ys) >= min_cells:
            out.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    return sorted(out, key=lambda b: (round(b[1] / 10), b[0]))


def crop_rows(a, keep_top, keep_bottom, height):
    # 위 keep_top줄 + 아래 keep_bottom줄을 남기고 가운데를 잘라 height줄로(9-slice 가운데 단색 구간을 줄인다)
    mid = height - keep_top - keep_bottom
    top = a[:keep_top]; bottom = a[-keep_bottom:]
    middle = a[keep_top:keep_top + mid]
    return np.concatenate([top, middle, bottom], axis=0)


# ---------- 버튼 A: 주·보조 3상태, 닫기, (옛 상단 바 pill: 시안 C로 안 씀, 자리만 건너뜀), 태그 ----------
btn = snap(load('buttons_a_px'))
names = ['btn_primary', 'btn_primary_pressed', 'btn_primary_disabled', 'btn_secondary', 'btn_secondary_pressed', 'btn_secondary_disabled', 'btn_close', 'pill_topbar', 'tag_cost']
for (x0, y0, x1, y1), n in zip(components(btn), names):
    part = trim(btn[y0:y1, x0:x1])
    if n.startswith('btn_') and n != 'btn_close':
        save(n, part, border=(5, 5, 5, 5))          # 좌·하·우·상 (Unity spriteBorder 순서 x=left y=bottom z=right w=top)
    elif n == 'btn_close':
        save(n, part)
    elif n == 'pill_topbar':
        continue                                    # 상단 HUD는 공용 알약 pill(make_pill.py)을 쓴다
    else:
        save(n, part, border=(6, 5, 6, 5))

# ---------- 아이콘 A ----------
ic = snap(load('icons_a_px'))
# 코인·닫기·아래 화살표는 공용(World/coin·btn_close·nego_arrow)으로 대체되어 저장하지 않는다(UI 작업 규칙 v1.0). 이름 자리는 시트 순서라 None으로 남긴다
for (x0, y0, x1, y1), n in zip(components(ic, 10), [None, 'icon_lock', None, None, 'icon_star', 'icon_up', 'icon_clock', 'icon_check']):
    if n:
        save(n, trim(ic[y0:y1, x0:x1]))

# ---------- 시트 B 부품 ----------
row = trim(snap(load('part_row_px')))              # 96×18: 행 배경. 높이 24로 9-slice
save('row', row, border=(6, 5, 6, 5))
# 딤(시트 뒤)·단색 사각(상단 바 배경)은 흰 1×1: 색은 Image.color
save('white', np.full((4, 4, 4), 255, np.uint8))

json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ui_slices.json', len(slices))
