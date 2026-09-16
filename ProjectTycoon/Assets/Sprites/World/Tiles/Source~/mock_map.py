# -*- coding: utf-8 -*-
# 실제 타일·스프라이트로 1호점 일부를 합성해 바닥 구성 시안을 만든다
# 사용: mock_map.py <A|B> <path시트.png> <출력.png> [scale=2]
from PIL import Image
import numpy as np, sys
OPT, PATH_SHEET, OUTF = sys.argv[1], sys.argv[2], sys.argv[3]
SCALE = int(sys.argv[4]) if len(sys.argv) > 4 else 2
A = 'C:/project/Tycoon/ProjectTycoon/Assets/'
def load(p): return np.asarray(Image.open(p).convert('RGBA'))
grass = load(A + 'Sprites/World/Tiles/grass.png'); dirt = load(A + 'Sprites/World/Tiles/dirt.png'); path = load(PATH_SHEET)
edge = load(A + 'Sprites/World/edge.png'); fence = load(A + 'Sprites/World/fence.png'); post = load(A + 'Sprites/World/fence_post.png')
wom = Image.open(A + 'Resources/Sprites/Animals/Wombat/Wombat.png').convert('RGBA'); wom = wom.crop(wom.getbbox())
wom = np.asarray(wom.resize((int(wom.size[0] * 34 / wom.size[1]), 34), Image.NEAREST))
def variant(sheet, x, z):
    d = (x - z) % 6; s = (x + z) % 6; i = d * 3 + (s - d % 2) // 2
    return sheet[:, i * 64:(i + 1) * 64]
X0, X1, Z0, Z1 = -3, 21, -3, 22          # 보이는 논리 범위(존 두 줄 + 길 + 잔디 테두리)
W = (X1 - X0 + Z1 - Z0) * 32 + 64; H = (X1 - X0 + Z1 - Z0) * 16 + 80
canvas = np.zeros((H, W, 4), np.uint8); canvas[..., :3] = (110, 173, 86); canvas[..., 3] = 255
ox, oy = (Z1 - X0) * 32, H - 40
def blit(img, pcol, prow, wx, wy):
    h, w = img.shape[:2]; left = ox + wx - pcol; top = oy - wy - (h - 1 - prow)
    x0, y0 = max(left, 0), max(top, 0); x1, y1 = min(left + w, W), min(top + h, H)
    if x1 <= x0 or y1 <= y0: return
    src = img[y0 - top:y1 - top, x0 - left:x1 - left]; m = src[..., 3] > 0
    canvas[y0:y1, x0:x1][m] = src[m]
def sx(x, z): return 32 * (x - z)
def sy(x, z): return 16 * (x + z)
zones = [(2 + c * 8, 2 + r * 10) for r in range(5) for c in range(2)]
visible_zones = [(zx, zz) for zx, zz in zones if zz + 8 <= Z1]
def in_zone(x, z): return any(zx <= x < zx + 6 and zz <= z < zz + 8 for zx, zz in zones)
def in_map(x, z): return 0 <= x < 18 and 0 <= z < 52
# 바닥(먼 것부터)
cells = sorted([(x, z) for x in range(X0, X1) for z in range(Z0, Z1)], key=lambda c: -(c[0] + c[1]))
for x, z in cells:
    ground = dirt if (OPT == 'B' and in_map(x, z)) or in_zone(x, z) else grass
    blit(variant(ground, x, z), 32, 0, sx(x, z), sy(x, z))
for x, z in cells:
    if in_map(x, z) and not in_zone(x, z): blit(variant(path, x, z), 32, 0, sx(x, z), sy(x, z))
# 경계선: A = 존 둘레, B = 맵 둘레
def ring(x0, z0, w, h):
    vis = lambda x, z: X0 <= x < X1 and Z0 <= z < Z1
    for x in range(x0, x0 + w):
        if vis(x, z0): blit(edge, 0, 0, sx(x, z0), sy(x, z0))
        if vis(x, z0 + h): blit(edge, 0, 0, sx(x, z0 + h), sy(x, z0 + h))
    ef = edge[:, ::-1]
    for z in range(z0, z0 + h):
        if vis(x0, z): blit(ef, 31, 0, sx(x0, z), sy(x0, z))
        if vis(x0 + w, z): blit(ef, 31, 0, sx(x0 + w, z), sy(x0 + w, z))
if OPT == 'A':
    for zx, zz in visible_zones: ring(zx, zz, 6, 8)
else:
    ring(0, 0, 18, 52)
# 서 있는 것: y 큰 것(먼 것)부터
items = []
ff = fence[:, ::-1]
for zx, zz in visible_zones:
    for x in range(zx, zx + 6):
        items.append((sy(x, zz), fence, 4, sx(x, zz))); items.append((sy(x, zz + 8), fence, 4, sx(x, zz + 8)))
    for z in range(zz, zz + 8):
        items.append((sy(zx, z), ff, 33, sx(zx, z))); items.append((sy(zx + 6, z), ff, 33, sx(zx + 6, z)))
    items.append((sy(zx + 6, zz + 8), post, 4, sx(zx + 6, zz + 8)))
for (x, z) in ((4.0, 5.5), (6.5, 7.5), (3.5, 8.5)):
    wx, wy = int(32 * (x - z)), int(16 * (x + z))
    items.append((wy, wom, wom.shape[1] // 2, wx))
for wy, img, pc, wx in sorted(items, key=lambda t: -t[0]):
    blit(img, pc, 0, wx, wy)
out = Image.fromarray(canvas)
out = out.crop((ox + sx(X0 + 2, Z1 - 1), 0, ox + sx(X1 - 1, Z0 + 2) + 64, H))
out.resize((out.size[0] * SCALE, out.size[1] * SCALE), Image.NEAREST).save(OUTF); print('ok', out.size)
