# -*- coding: utf-8 -*-
# 가게 유닛 걷기 프레임(칸 편집, AI 없음. 웜뱃 2026-09-18 확정, 토끼 손님 2026-09-21). 캔버스 좌우 1칸 여백(88px 폭, 가운데 정렬 그대로).
#   C 딛기 = 몸 1칸 눌림(발 바로 위 줄을 뺌), 귀는 아직 위(늘어남)
#   L 왼발 들기 = 몸 제자리로 올라오고 왼발만 1칸 들림, 귀는 제자리(약하게: 딛기 때만 늘어남), 귀만 디딘 발 쪽으로 1칸 갸웃(머리는 몸통과 함께)
#   R 오른발 들기 = 반대
# 재생 C → L → C → R. 위아래는 1칸 안에서만 움직인다
import os
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
D = '../'
# low: 딛기 때 뺄 줄 · lean: 기울일 윗몸 마지막 행(얼굴 아래) · foot: 발 시작 행 · left/right: 발 열 범위(여백 전 칸)
# tip: 갸웃할 귀 마지막 행(없으면 귀 전체). 긴 귀는 전체를 밀면 머리와 만나는 곳 외곽선이 끊겨 끝만 민다
SPEC = {
    'wombat_front': {'low': 35, 'lean': 9, 'foot': 38, 'left': (8, 16), 'right': (25, 33), 'ear': 4, 'ears': [(5, 13), (28, 36)]},
    'wombat_back': {'low': 36, 'lean': 9, 'foot': 42, 'left': (8, 16), 'right': (25, 33), 'ear': 5, 'ears': [(4, 12), (29, 37)]},
    'rabbit_front': {'dir': '', 'low': 36, 'foot': 39, 'left': (7, 12), 'right': (15, 20), 'ear': 12, 'tip': 6, 'ears': [(2, 12), (15, 25)]},
    'rabbit_back': {'dir': '', 'low': 23, 'foot': 36, 'left': (7, 12), 'right': (15, 20), 'ear': 11, 'tip': 5, 'ears': [(3, 12), (15, 24)]},
    'penguin_front': {'dir': '', 'low': 27, 'foot': 32, 'left': (10, 15), 'right': (21, 26), 'ear': 0, 'ears': []},
    'penguin_back': {'dir': '', 'low': 27, 'foot': 32, 'left': (10, 15), 'right': (21, 26), 'ear': 0, 'ears': []},
    'fox_front': {'dir': '', 'low': 33, 'foot': 42, 'left': (18, 23), 'right': (28, 33), 'ear': 5, 'tip': 3, 'ears': [(11, 22), (29, 39)]},
    'hedgehog_front': {'dir': '', 'low': 22, 'foot': 35, 'left': (10, 15), 'right': (21, 26), 'ear': 0, 'ears': []},
    'fox_back': {'dir': '', 'low': 33, 'foot': 42, 'left': (18, 23), 'right': (28, 33), 'ear': 5, 'tip': 3, 'ears': [(11, 22), (29, 39)]},
    'hedgehog_back': {'dir': '', 'low': 22, 'foot': 35, 'left': (10, 15), 'right': (21, 26), 'ear': 0, 'ears': []},
}


def ears_at(dst, src, s, dy):
    # pad된 배열: dst의 귀 영역을 src 귀를 dy칸 내린 것으로
    o = dst.copy()
    e = s['ear']
    for x0, x1 in s['ears']:
        x0, x1 = x0 + 1, x1 + 1
        o[0:e + 1 + dy, x0:x1 + 1] = 0 if dy else o[0:e + 1, x0:x1 + 1]
        o[dy:e + 1 + dy, x0:x1 + 1] = src[0:e + 1, x0:x1 + 1]
    return o


def pad(c):
    o = np.zeros((c.shape[0], c.shape[1] + 2, 4), np.uint8)
    o[:, 1:-1] = c
    return o


def remove_row(c, row):
    keep = [y for y in range(c.shape[0]) if y != row]
    o = np.zeros_like(c)
    o[1:] = c[keep]
    return o


def lift(c, s, side):
    # pad된 배열: side 발만 1칸 위로. 귀만 디딘 발 쪽으로 1칸 갸웃(머리는 몸통과 함께 제자리)
    o = c.copy()
    x0, x1 = s[side]
    x0, x1 = x0 + 1, x1 + 1
    y0 = s['foot']
    region = c[y0:, x0:x1 + 1].copy()
    o[y0:, x0:x1 + 1] = 0
    o[y0 - 1:c.shape[0] - 1, x0:x1 + 1] = np.where((region[..., 3] > 0)[..., None], region, o[y0 - 1:c.shape[0] - 1, x0:x1 + 1])
    dx = 1 if side == 'left' else -1
    e = s.get('tip', s['ear'])
    for ex0, ex1 in s['ears']:
        ex0, ex1 = ex0 + 1, ex1 + 1
        ear = c[0:e + 1, ex0:ex1 + 1].copy()
        o[0:e + 1, ex0:ex1 + 1] = 0
        # 머리 윗선은 원래대로 두고 귀만 옆으로
        o[0:e + 1, ex0 + dx:ex1 + 1 + dx] = np.where((ear[..., 3] > 0)[..., None], ear, o[0:e + 1, ex0 + dx:ex1 + 1 + dx])
    return o


for name, s in SPEC.items():
    base = pad(np.asarray(Image.open(s.get('dir', D) + f'{name}.png').convert('RGBA'))[::2, ::2])
    down = remove_row(base, s['low'])
    contact = ears_at(down, base, s, 0)
    frames = [contact, lift(base, s, 'left'), contact, lift(base, s, 'right')]
    for i, f in enumerate(frames):
        Image.fromarray(np.repeat(np.repeat(f, 2, 0), 2, 1)).save(s.get('dir', D) + f'{name}_walk_{i}.png')
    print(name, 'walk', len(frames))
