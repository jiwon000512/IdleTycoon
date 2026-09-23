# -*- coding: utf-8 -*-
# 가게 구역 배경 4장(entrance·shelf_row·counter_row·oven_row). 한 칸 2px, 폭 270칸(6.75유닛), PPU 80.
# 굴 안 "둥근 방": 양옆 벽 안쪽 선이 구역 가운데에서 부풀고 위·아래 끝에서는 같은 x(EDGE)로 돌아와 어떤 순서로 쌓아도 이어진다.
# 바닥 = floor 타일, 벽 = 같은 타일을 어둡게, 경계에 외곽선 1칸 + 밝은 테두리 1칸(굴 둔덕과 같은 결). 구역 이음새 근처는 자갈을 지워 티가 안 나게.
import math, numpy as np
from PIL import Image
W, EDGE, BULGE = 270, 16, 9
OUT, RIM = (52, 32, 32), (192, 144, 120)
tile = np.asarray(Image.open('shop_raw/floor_b_px.png').convert('RGBA'))[::2, ::2]
wall = (tile.astype(int) * [0.62, 0.56, 0.54, 1]).clip(0, 255).astype(np.uint8)
from collections import Counter
base = Counter(map(tuple, tile[..., :3].reshape(-1, 3).tolist())).most_common(1)[0][0]

def field(t, h, w=W, oy=0):
    reps = np.tile(t, (h // t.shape[0] + 2, w // t.shape[1] + 1, 1)); return reps[oy:oy + h, :w].copy()

def profile(h, phase):
    # y마다 벽 안쪽 x: 끝은 EDGE, 가운데로 갈수록 BULGE만큼 바깥으로. 작은 흔들림으로 손그림 느낌
    xs = []
    for y in range(h):
        t = y / (h - 1)
        bump = math.sin(math.pi * t) ** 1.4
        wob = 0.9 * math.sin(y * 0.55 + phase) + 0.5 * math.sin(y * 1.7 + phase * 2.1)
        xs.append(int(round(EDGE - BULGE * bump + wob * bump)))
    return xs

def room(h, oy, mask=None):
    g = field(wall, h, oy=oy); floor = field(tile, h, oy=oy)
    inside = np.zeros((h, W), bool)
    L, R = profile(h, 0.3), profile(h, 2.4)
    for y in range(h):
        inside[y, L[y]:W - R[y]] = True
    if mask is not None:
        inside &= mask
    g[inside] = floor[inside]
    # 이음새(위·아래 3칸) 자갈 지우기
    for rows in (slice(0, 3), slice(h - 3, h)):
        band = g[rows]; sel = inside[rows] & (band[..., :3] != base).any(2); band[sel] = (*base, 255); g[rows] = band
    pad = np.pad(inside, 1)
    edge = inside & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])  # 바닥 쪽 가장자리
    outer = ~inside & (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:])  # 벽 쪽 첫 칸
    p2 = np.pad(outer, 1)
    rim = ~inside & ~outer & (p2[:-2, 1:-1] | p2[2:, 1:-1] | p2[1:-1, :-2] | p2[1:-1, 2:])
    g[rim] = (*RIM, 255); g[outer] = (*OUT, 255)
    # 위·아래 끝 행은 외곽선을 세로 방향만 남긴다(이음새에서 가로선이 생기지 않게)
    return g

def save(g, name):
    Image.fromarray(np.repeat(np.repeat(g, 2, 0), 2, 1)).save(f'../{name}.png'); print(name, g.shape[1], 'x', g.shape[0])

oy = 0
for name, units in (('shelf_row', 2.4), ('counter_row', 2.6), ('oven_row', 2.0)):
    h = int(units * 40); save(room(h, 0), name)

# 입구: 잔디 20칸, 벽, 구멍. 구멍 아래로 둥근 천장의 방이 시작해 아래 구역과 이어진다
h = 80; grass = 20
arch = np.asarray(Image.open('shop_raw/arch_hole.png').convert('RGBA'))[::2, ::2]
ah, aw = arch.shape[:2]; ax = (W - aw) // 2; ay = h - ah
mask = np.zeros((h, W), bool)
top = ay + ah * 0.55  # 방 천장 = 구멍 중간부터
for y in range(int(top), h):
    t = (y - top) / (h - 1 - top)
    # 4분의 1 타원: 천장은 수평, 아래 끝에서는 수직으로 옆벽(EDGE)과 이어진다
    half = aw * 0.42 + (W / 2 - EDGE - aw * 0.42) * math.sqrt(max(0.0, 1 - (1 - t) ** 2))
    mask[y, int(W / 2 - half):int(W / 2 + half)] = True
g = room(h, 0, mask)
g[:grass] = (122, 168, 82, 255); g[grass, :, :3] = (90, 130, 60)
m = arch[..., 3] > 0; g[ay:ay + ah, ax:ax + aw][m] = arch[m]
save(g, 'entrance')
