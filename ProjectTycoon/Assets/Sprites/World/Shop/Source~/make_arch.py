# -*- coding: utf-8 -*-
# 입구 아치: arch_a(흙 둔덕 + 구멍)에서 구멍만 남기고 키운다. 구멍 마스크를 SCALE배로 키우고 테두리 RIM칸 + 외곽선 1칸을 다시 그린다
import numpy as np, sys
from PIL import Image
SCALE = float(sys.argv[1]) if len(sys.argv) > 1 else 1.6
RIM = 3
src = np.asarray(Image.open('arch_a_px.png').convert('RGBA'))[::2, ::2]
cols = src[..., :3].astype(int)
dark = (src[..., 3] > 0) & (cols.sum(1 if False else 2) < 48 * 3 + 40)
# 어두운 칸 중 바깥 외곽선과 이어지지 않은 가장 큰 덩어리 = 구멍
from collections import deque
lab = np.full(dark.shape, -1); comps = []
for sy, sx in zip(*np.nonzero(dark)):
    if lab[sy, sx] >= 0: continue
    k = len(comps); q = deque([(sy, sx)]); lab[sy, sx] = k; cells = []
    while q:
        y, x = q.popleft(); cells.append((y, x))
        for ny, nx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
            if 0 <= ny < dark.shape[0] and 0 <= nx < dark.shape[1] and dark[ny, nx] and lab[ny, nx] < 0:
                lab[ny, nx] = k; q.append((ny, nx))
    comps.append(cells)
opaque = src[..., 3] > 0
def touches_edge(cells):
    return any(y == 0 or x == 0 or y == dark.shape[0]-1 or x == dark.shape[1]-1 or not opaque[max(y-1,0):y+2, max(x-1,0):x+2].all() for y, x in cells)
inner_comps = [c for c in comps if not touches_edge(c)]
cells = max(inner_comps, key=len)
ys = np.array([c[0] for c in cells]); xs = np.array([c[1] for c in cells])
hole = np.zeros(dark.shape, bool); hole[ys, xs] = True
hole = hole[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
print('hole cells', hole.shape[1], 'x', hole.shape[0])
h, w = hole.shape; H, W = int(round(h * SCALE)), int(round(w * SCALE))
big = np.asarray(Image.fromarray(hole.astype(np.uint8) * 255).resize((W, H), Image.NEAREST)) > 0
pad = RIM + 1
g = np.zeros((H + 2 * pad, W + 2 * pad), bool); g[pad:pad + H, pad:pad + W] = big
def dil(m, n):
    o = m.copy()
    for _ in range(n):
        p = np.pad(o, 1); o = o | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return o
rim = dil(g, RIM) & ~g; line = dil(g, RIM + 1) & ~dil(g, RIM)
out = np.zeros(g.shape + (4,), np.uint8)
out[line] = (48, 24, 24, 255)
# 테두리: 위쪽 밝은 흙, 아래쪽 어두운 흙(arch_a 색)
yy = np.arange(g.shape[0])[:, None] * np.ones(g.shape, int)
out[rim & (yy < g.shape[0] * 0.55)] = (192, 144, 120, 255)
out[rim & (yy >= g.shape[0] * 0.55)] = (156, 108, 96, 255)
out[g] = (48, 24, 24, 255)
# 구멍 안쪽 위에 한 톤 밝은 띠(터널 깊이)
inner = g & ~dil(~g, 2)
out[inner & (yy < g.shape[0] * 0.35)] = (72, 44, 44, 255)
# 바닥에 닿는 아랫줄은 외곽선 없이 평평하게: 구멍 아래 테두리·선을 지운다
floor_y = np.nonzero(g.any(1))[0].max()
out[floor_y + 1:] = 0
Image.fromarray(np.repeat(np.repeat(out[:floor_y + 1], 2, 0), 2, 1)).save('arch_hole.png')
print('arch_hole', out[:floor_y + 1].shape[1], 'x', floor_y + 1, 'cells (hole', W, 'x', H, ')')
