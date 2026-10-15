# -*- coding: utf-8 -*-
# 다리 디벨롭(2026-10-14 아트방, 사용자 「가장자리가 끊기는 느낌을 없애기」): 통나무 뗏목(C)을 바탕으로 좌우 난간과 양 끝 걸침 장식을 9-slice 사방 테로 넣는다.
#   한 벌 = 두 장: bridge.png(세로 다리, 통나무가 가로로 누움) · bridge_side.png(가로 다리, 통나무가 세로로 섬). 둘 다 48×48칸(96×96px), PPU 80, 피벗 가운데.
#   9-slice(px): 세로 bridge.png (left 12, bottom 16, right 12, top 16) — 좌우 6칸 난간, 위아래 8칸 끝 장식(뭍에 0.3유닛 = 12칸 걸치는 자리), 가운데 36×32칸 통나무 반복.
#               가로 bridge_side.png (left 16, bottom 20, right 16, top 12) — 좌우 8칸 끝 장식, 위 6칸 뒤 난간, 아래 10칸 앞 난간 + 앞면, 가운데 32×32칸 통나무 반복.
#   시안: A 밧줄 난간 + 말뚝 묶음 · B 널 테두리 보 + 끝 가로 널 · C 돌 둑 난간 + 돌 끝받침. 통나무 · 밧줄 색은 make_bridge C와 같다.
# 출력: ../bridge.png · ../bridge_side.png (고른 시안 PICK)
# 사용: python make_bridge2.py <폴더>   세 시안을 <폴더>/set_<a|b|c>/bridge.png · bridge_side.png + 1x
#       python make_bridge2.py          고른 시안을 ../bridge.png · ../bridge_side.png로
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PICK = 'b'
N = 48
LINE = (52, 32, 32)
HI, MID, LO, DK = (196, 140, 94), (153, 100, 66), (118, 72, 48), (84, 52, 36)      # 통나무(make_bridge C)
ROPE, ROPE_LO = (240, 184, 112), (197, 132, 85)
PLANK_HI, PLANK, PLANK_LO = (210, 158, 109), (160, 111, 81), (130, 87, 63)          # 좌대 색 널
STONE_HI, STONE, STONE_LO = (200, 192, 184), (172, 164, 156), (136, 124, 116)
MOSS = (120, 136, 64)
LOG = [HI, MID, MID, MID, LO, LO, DK, LINE]                                        # 8칸 통나무(밝음 → 틈)

# 테(칸): 세로 = (left, bottom, right, top)
BORDER_V = (6, 8, 6, 8)
BORDER_H = (8, 10, 8, 6)


def blank():
    return np.zeros((N, N, 4), np.uint8)


def put(s, y, x, c):
    if 0 <= y < N and 0 <= x < N:
        s[y, x, :3] = c
        s[y, x, 3] = 255


def rect(s, y0, y1, x0, x1, c):
    for y in range(y0, y1):
        for x in range(x0, x1):
            put(s, y, x, c)


def logs_across(s, y0, y1, x0, x1):
    """가로로 누운 통나무(8칸 주기, 위부터)"""
    for y in range(y0, y1):
        rect(s, y, y + 1, x0, x1, LOG[(y - y0) % 8])


def logs_upright(s, y0, y1, x0, x1):
    """세로로 선 통나무(8칸 주기, 왼쪽부터)"""
    for x in range(x0, x1):
        rect(s, y0, y1, x, x + 1, LOG[(x - x0) % 8])


def face(s, y0, x0, x1):
    """앞면 6줄: 통나무 밑동 · 선 · 받침 보(밝음 · 몸 · 그늘) · 선"""
    for k, c in enumerate((LO, LINE, PLANK_HI, PLANK, PLANK_LO, LINE)):
        rect(s, y0 + k, y0 + k + 1, x0, x1, c)


# ---- 난간(세로 다리의 좌우 6칸 · 가로 다리의 위 6칸 / 아래 4칸) ----
def rail_rope_v(s, x0, y0, y1):
    """밧줄 난간(세로): 바깥 선 · 밧줄 2칸(두 줄마다 밝기 바꿔 꼬임) · 안쪽 선 · 통나무 끝 2칸"""
    for y in range(y0, y1):
        tw = (y // 2) % 2 == 0
        row = [LINE, ROPE if tw else ROPE_LO, ROPE_LO if tw else ROPE, LINE, LOG[(y - y0) % 8], LOG[(y - y0) % 8]]
        for k, c in enumerate(row):
            put(s, y, x0 + k, c)


def rail_rope_h(s, y0, x0, x1, thick):
    for x in range(x0, x1):
        tw = (x // 2) % 2 == 0
        col = [LINE, ROPE if tw else ROPE_LO, ROPE_LO if tw else ROPE, LINE] + [LOG[(x - x0) % 8]] * (thick - 4)
        for k, c in enumerate(col):
            put(s, y0 + k, x, c)


def rail_plank_v(s, x0, y0, y1):
    """널 테두리 보(세로): 선 · 밝은 널 · 널 · 널 · 그늘 · 선"""
    for k, c in enumerate((LINE, PLANK_HI, PLANK, PLANK, PLANK_LO, LINE)):
        rect(s, y0, y1, x0 + k, x0 + k + 1, c)


def rail_plank_h(s, y0, x0, x1, thick):
    cols = (LINE, PLANK_HI, PLANK, PLANK, PLANK_LO, LINE) if thick >= 6 else (LINE, PLANK_HI, PLANK_LO, LINE)
    for k, c in enumerate(cols):
        rect(s, y0 + k, y0 + k + 1, x0, x1, c)


def rail_stone_v(s, x0, y0, y1):
    """돌 둑 난간(세로): 4칸마다 돌 하나(윗줄 밝음 · 몸 · 그늘, 사이 선), 안쪽 선"""
    for y in range(y0, y1):
        k = (y - y0) % 4
        c = LINE if k == 3 else STONE_HI if k == 0 else STONE if k == 1 else STONE_LO
        for dx in range(5):
            put(s, y, x0 + dx, LINE if dx in (0, 4) and k != 3 else c)
        put(s, y, x0 + 5, LINE if k == 3 else STONE_LO)
    for y in range(y0 + 1, y1, 9):
        put(s, y, x0 + 2, MOSS)


def rail_stone_h(s, y0, x0, x1, thick):
    for x in range(x0, x1):
        k = (x - x0) % 4
        c = LINE if k == 3 else STONE_HI if k == 0 else STONE if k == 1 else STONE_LO
        for dy in range(thick):
            edge = dy in (0, thick - 1)
            put(s, y0 + dy, x, LINE if (edge and k != 3) else (STONE_HI if dy == 1 and k != 3 else c))
    for x in range(x0 + 1, x1, 9):
        put(s, y0 + 1, x, MOSS)


# ---- 끝 장식 ----
def post(s, y_bottom, x, h, cap=True):
    """말뚝: 폭 3(선 · 몸 · 그늘선), 위에 밝은 머리"""
    for dy in range(h):
        put(s, y_bottom - dy, x, LINE); put(s, y_bottom - dy, x + 1, PLANK if dy else PLANK_LO); put(s, y_bottom - dy, x + 2, LINE)
    if cap:
        put(s, y_bottom - h, x, LINE); put(s, y_bottom - h, x + 1, PLANK_HI); put(s, y_bottom - h, x + 2, LINE)
        put(s, y_bottom - h - 1, x + 1, LINE)


def cross_log_rows(s, y0, x0, x1):
    """끝 가로 통나무 5줄(굵고 어둡게: 밝음 · 몸 · 몸 · 그늘 · 선)"""
    for k, c in enumerate((HI, MID, MID, LO, LINE)):
        rect(s, y0 + k, y0 + k + 1, x0, x1, c)
    rect(s, y0, y0 + 5, x0, x0 + 1, LINE); rect(s, y0, y0 + 5, x1 - 1, x1, LINE)


def cross_log_cols(s, x0, y0, y1):
    for k, c in enumerate((LINE, HI, MID, MID, LO)):
        rect(s, y0, y1, x0 + k, x0 + k + 1, c)
    rect(s, y0, y0 + 1, x0, x0 + 5, LINE); rect(s, y1 - 1, y1, x0, x0 + 5, LINE)


def stone_pier_rows(s, y0, x0, x1):
    """돌 끝받침 가로 띠 6줄: 돌 두 줄(4칸 돌, 어긋남)"""
    for y in range(y0, y0 + 6):
        r = (y - y0) // 3
        k = (y - y0) % 3
        for x in range(x0, x1):
            m = (x - x0 + (2 if r else 0)) % 5
            c = LINE if m == 4 or k == 2 else STONE_HI if k == 0 else STONE
            put(s, y, x, c)


def stone_pier_cols(s, x0, y0, y1):
    for x in range(x0, x0 + 6):
        r = (x - x0) // 3
        k = (x - x0) % 3
        for y in range(y0, y1):
            m = (y - y0 + (2 if r else 0)) % 5
            c = LINE if m == 4 or k == 2 else STONE_HI if k == 0 else STONE
            put(s, y, x, c)


# ---- 시안 ----
def vertical(kind):
    s = blank()
    L, B, R, T = BORDER_V
    logs_across(s, T, N - B, L, N - R)
    rail_v = {'a': rail_rope_v, 'b': rail_plank_v, 'c': rail_stone_v}[kind]
    rail_v(s, 0, T, N - B)
    # 오른쪽 난간은 왼쪽을 거울로
    left = s[T:N - B, 0:L].copy()
    s[T:N - B, N - R:N] = left[:, ::-1]
    # 위 끝(먼 끝, 뭍에 걸침): 끝 통나무 + 말뚝 · 돌
    if kind == 'c':
        stone_pier_rows(s, 2, 0, N)
        rect(s, 0, 2, 0, N, (0, 0, 0)); s[0:2, :, 3] = 0
    else:
        cross_log_rows(s, 3, 1, N - 1)
        post(s, 7, 0, 5); post(s, 7, N - 3, 5)
        if kind == 'a':
            post(s, 7, 6, 4); post(s, 7, N - 9, 4)
    # 아래 끝(가까운 끝): 끝 통나무 윗면 2줄 + 앞면 6줄
    y0 = N - B
    if kind == 'c':
        stone_pier_rows(s, y0, 0, N)
        rect(s, y0 + 6, y0 + 7, 0, N, STONE_LO); rect(s, y0 + 7, y0 + 8, 0, N, LINE)
    else:
        rect(s, y0, y0 + 1, 1, N - 1, HI); rect(s, y0 + 1, y0 + 2, 1, N - 1, MID)
        face(s, y0 + 2, 1, N - 1)
        rect(s, y0, y0 + 8, 0, 1, LINE); rect(s, y0, y0 + 8, N - 1, N, LINE)
        post(s, y0 + 5, 0, 6); post(s, y0 + 5, N - 3, 6)
        if kind == 'a':
            post(s, y0 + 5, 6, 5); post(s, y0 + 5, N - 9, 5)
    return s


def horizontal(kind):
    s = blank()
    L, B, R, T = BORDER_H
    logs_upright(s, T, N - B, L, N - R)
    rail_h = {'a': rail_rope_h, 'b': rail_plank_h, 'c': rail_stone_h}[kind]
    rail_h(s, 0, L, N - R, T)                      # 뒤 난간 6줄
    rail_h(s, N - B, L, N - R, 4)                  # 앞 난간 4줄
    face(s, N - B + 4, L, N - R)                   # 앞면 6줄
    # 왼쪽 끝(뭍에 걸침): 끝 세로 통나무 · 말뚝 / 돌
    if kind == 'c':
        stone_pier_cols(s, 1, 0, N - 4)
        rect(s, N - 4, N - 3, 1, 7, STONE_LO); rect(s, N - 3, N - 2, 1, 7, LINE)
        rect(s, 0, N - 2, 0, 1, LINE)
    else:
        cross_log_cols(s, 2, 1, N - B + 4)
        face(s, N - B + 4, 2, 7)
        post(s, 6, 0, 6); post(s, N - 5, 0, 7)
        if kind == 'a':
            post(s, 6, 4, 5); post(s, N - 5, 4, 6)
    left = s[:, 0:L].copy()
    s[:, N - R:N] = left[:, ::-1]
    return s


def check(s, border, across):
    L, B, R, T = border
    assert s.shape == (N, N, 4)
    c = s[T:N - B, L:N - R]
    if across:
        assert (c[8:16] == c[0:8]).all(), '세로 반복'
    else:
        assert (c[:, 8:16] == c[:, 0:8]).all(), '가로 반복'


def save(s, path, scale=2):
    img = Image.fromarray(s, 'RGBA')
    if scale != 1:
        img = img.resize((N * scale, N * scale), Image.NEAREST)
    img.save(path)


if __name__ == '__main__':
    if len(sys.argv) > 1:
        for k in 'abc':
            out = os.path.join(sys.argv[1], 'set_' + k)
            os.makedirs(out, exist_ok=True)
            v, h = vertical(k), horizontal(k)
            check(v, BORDER_V, True); check(h, BORDER_H, False)
            save(v, os.path.join(out, 'bridge.png')); save(h, os.path.join(out, 'bridge_side.png'))
            save(v, os.path.join(out, 'bridge_1x.png'), 1); save(h, os.path.join(out, 'bridge_side_1x.png'), 1)
        print(sys.argv[1])
    else:
        v, h = vertical(PICK), horizontal(PICK)
        check(v, BORDER_V, True); check(h, BORDER_H, False)
        save(v, os.path.join(HERE, '..', 'bridge.png')); save(h, os.path.join(HERE, '..', 'bridge_side.png'))
        print('bridge.png · bridge_side.png <-', PICK, 'border V(px)', tuple(b * 2 for b in BORDER_V), 'H(px)', tuple(b * 2 for b in BORDER_H))
