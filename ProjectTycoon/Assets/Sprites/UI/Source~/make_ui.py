# -*- coding: utf-8 -*-
# UI 부품 최종본 생성(UI-디자인-규칙 v1.0). 원본은 Codex 시안(raw/*.png, 시트 B·버튼 A·아이콘 A)을 make_pixel.py로 격자 강제한 raw/*_px.png.
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
slices = {}


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


# ---------- 버튼 A: 주·보조 3상태, 닫기, 상단 바 pill, 태그 ----------
btn = snap(load('buttons_a_px'))
names = ['btn_primary', 'btn_primary_pressed', 'btn_primary_disabled', 'btn_secondary', 'btn_secondary_pressed', 'btn_secondary_disabled', 'btn_close', 'pill_topbar', 'tag_cost']
for (x0, y0, x1, y1), n in zip(components(btn), names):
    part = trim(btn[y0:y1, x0:x1])
    if n.startswith('btn_') and n != 'btn_close':
        save(n, part, border=(5, 5, 5, 5))          # 좌·하·우·상 (Unity spriteBorder 순서 x=left y=bottom z=right w=top)
    elif n == 'btn_close':
        save(n, part)
    elif n == 'pill_topbar':
        save(n, part, border=(15, 4, 4, 4))         # 왼쪽 코인 부분 고정, 오른쪽으로 늘림
    else:
        save(n, part, border=(6, 5, 6, 5))

# ---------- 아이콘 A ----------
ic = snap(load('icons_a_px'))
for (x0, y0, x1, y1), n in zip(components(ic, 10), ['icon_coin', 'icon_lock', 'icon_close', 'icon_down', 'icon_star', 'icon_up', 'icon_clock', 'icon_check']):
    save(n, trim(ic[y0:y1, x0:x1]))

# ---------- 시트 B 부품 ----------
frame = trim(snap(load('part_frame_px')))          # 64×40: 나무 틀 4px + 리벳. 9-slice 코너 8
save('sheet_frame', frame, border=(8, 8, 8, 8))
chip = trim(snap(load('part_chip_px')))            # 44×55 → 44×48: 가운데(아이콘 칸) 단색 줄을 줄인다
save('chip', crop_rows(chip, 30, 14, 48), border=(6, 6, 6, 6))
chip_hi = trim(snap(load('part_chip_hi_px')))      # 44×52 → 44×48
save('chip_hi', crop_rows(chip_hi, 30, 14, 48), border=(6, 6, 6, 6))
row = trim(snap(load('part_row_px')))              # 96×18: 행 배경. 높이 24로 9-slice
save('row', row, border=(6, 5, 6, 5))
pill = trim(snap(load('part_pill_px')))            # 30×12: 비용 pill(코인 왼쪽 고정)
save('pill_cost', pill, border=(13, 3, 3, 3))

# 딤(시트 뒤)·단색 사각(상단 바 배경)은 흰 1×1: 색은 Image.color
save('white', np.full((4, 4, 4), 255, np.uint8))

json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ui_slices.json', len(slices))
