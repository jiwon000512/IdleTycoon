# -*- coding: utf-8 -*-
# 흙 텍스처(top-down) → 픽셀 격자 정규화 → 화면 공간 2:1 압축 → 마름모 타일 18장 시트 + 미리보기
from PIL import Image
import numpy as np, sys
SRC = sys.argv[1]; OUT = sys.argv[2]; G = int(sys.argv[3]) if len(sys.argv) > 3 else 96
img = Image.open(SRC).convert('RGB')
W = img.size[0]
# 픽셀 블록 크기 추정: 가로 색 변화 간격의 중앙값
a = np.asarray(img).astype(int)
runs = []
for y in range(0, W, W // 40):
    row = a[y]; d = np.abs(np.diff(row, axis=0)).sum(1) > 40
    idx = np.flatnonzero(d)
    if len(idx) > 1: runs += list(np.diff(idx))
block = int(np.median([r for r in runs if r > 2])) if runs else W // G
print('block px', block, '-> art px', round(W / block))
# 팔레트: 전체 이미지의 색을 빈도순으로 훑어 거리 30 안이면 합치는 탐욕 군집(작은 자갈·외곽선 색이 살아남는다)
full = (np.asarray(img).reshape(-1, 3) // 12 * 12)
cols, counts = np.unique(full, axis=0, return_counts=True)
order = np.argsort(-counts); pal = []
for i in order:
    if counts[i] < full.shape[0] * (float(sys.argv[4]) if len(sys.argv) > 4 else 0.0005): break
    c = cols[i].astype(int)
    if all(np.abs(c - q).sum() > 30 for q in pal): pal.append(c)
pal = np.array(pal)
print('palette', [tuple(int(v) for v in c) for c in pal])
sm = np.asarray(img.resize((G, G), Image.NEAREST)).astype(int)
dist = ((sm[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(3)
small = Image.fromarray(pal[dist.argmin(2)].astype(np.uint8))
s = np.asarray(small).astype(int)
seam = np.abs(s[:, 0] - s[:, -1]).sum(1).mean(); inner = np.abs(s[:, 1:] - s[:, :-1]).sum(2).mean()
seamv = np.abs(s[0] - s[-1]).sum(1).mean()
print('seam h/v', round(seam, 1), round(seamv, 1), 'vs inner', round(inner, 1))
pal = sorted({tuple(p) for p in s.reshape(-1, 3)}); print('palette', pal)
# 화면 공간 텍스처: 텍셀 2×1 px, 폭 2G × 높이 G
tex = small.resize((2 * G, G), Image.NEAREST)
t = np.asarray(tex)
TW, TH = 2 * G, G
def tile(d, s_):
    cx, cy = 32 * d, 16 * s_
    im = np.zeros((32, 64, 4), np.uint8)
    for y in range(32):
        hw = (y + 1) * 2 if y < 16 else (32 - y) * 2
        for x in range(32 - hw, 32 + hw):
            gx = cx + (x - 32); gy = cy + (16 - y)
            u = gx % TW; v = (-gy) % TH
            im[y, x, :3] = t[v, u]; im[y, x, 3] = 255
    return im
P = TW // 32  # 6
tiles = []
for d in range(P):
    for k in range(3):
        s_ = (d % 2) + 2 * k
        tiles.append(tile(d, s_))
sheet = np.concatenate(tiles, axis=1)
Image.fromarray(sheet).save(OUT + '/dirt.png')
# 미리보기: 존 6×8 + 주변 1칸, 2배
ZX, ZZ = 8, 10
cw = (ZX + ZZ) * 32; ch = (ZX + ZZ) * 16 + 16
prev = np.zeros((ch, cw, 4), np.uint8); prev[..., :3] = (110, 173, 86); prev[..., 3] = 255
for z in range(ZZ):
    for x in range(ZX):
        d = (x - z) % P; sm = (x + z) % P
        idx = d * 3 + (sm - d % 2) // 2
        im = tiles[idx]
        sx = (x - z) * 32 + (ZZ - 1) * 32; sy = ch - 32 - (x + z) * 16
        m = im[..., 3] > 0
        prev[sy:sy + 32, sx:sx + 64][m] = im[m]
Image.fromarray(prev).resize((cw * 2, ch * 2), Image.NEAREST).save(OUT + '/preview.png')
print('tiles', len(tiles))
