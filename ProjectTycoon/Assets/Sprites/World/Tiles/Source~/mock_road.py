# -*- coding: utf-8 -*-
# 복도보다 좁은 연속 돌길 시안. 바닥(흙·잔디)은 이웃 지형이 복도 아래까지 이어지고, 돌길은 복도 중앙에 폭 ratio×w로 가장자리가 흔들리며 놓인다
# 사용: mock_road.py <w 복도폭> <ratio 돌길폭/복도폭> <apron 존 둘레 흙 폭(타일)> <amp 가장자리 흔들림(타일)> <출력.png>
from PIL import Image
import numpy as np, sys, math
w = int(sys.argv[1]); ratio = float(sys.argv[2]); apron = float(sys.argv[3]); amp = float(sys.argv[4]); OUTF = sys.argv[5]
A = 'C:/project/Tycoon/ProjectTycoon/Assets/'
def load(p): return np.asarray(Image.open(p).convert('RGBA'))
grass = load(A + 'Sprites/World/Tiles/grass.png'); dirt = load(A + 'Sprites/World/Tiles/dirt.png')
stone = load('C:/Users/jiwon/AppData/Local/Temp/claude/C--project-Tycoon/fa162f9e-8d3f-47f0-9626-25c6a5841ff4/scratchpad/path/path_dense.png')
fence = load(A + 'Sprites/World/fence.png'); post = load(A + 'Sprites/World/fence_post.png')
wom = Image.open(A + 'Resources/Sprites/Animals/Wombat/Wombat.png').convert('RGBA'); wom = wom.crop(wom.getbbox())
wom = np.asarray(wom.resize((int(wom.size[0] * 34 / wom.size[1]), 34), Image.NEAREST))
ROWS, COLS = 2, 2
zones = [(w + c * (6 + w), w + r * (8 + w)) for r in range(ROWS) for c in range(COLS)]
MW, MH = COLS * 6 + (COLS + 1) * w, ROWS * 8 + (ROWS + 1) * w
X0, X1, Z0, Z1 = -3, MW + 3, -3, MH + 3
W = (X1 - X0 + Z1 - Z0) * 32 + 64; H = (X1 - X0 + Z1 - Z0) * 16 + 80
ox, oy = (Z1 - X0) * 32, H - 40
def variant(sheet, x, z):
    d = (x - z) % 6; s = (x + z) % 6; i = d * 3 + (s - d % 2) // 2
    return sheet[:, i * 64:(i + 1) * 64]
def sx(x, z): return 32 * (x - z)
def sy(x, z): return 16 * (x + z)
def layer(sheet):
    cv = np.zeros((H, W, 4), np.uint8)
    for x in range(X0, X1):
        for z in range(Z0, Z1):
            img = variant(sheet, x, z); left = ox + sx(x, z) - 32; top = oy - sy(x, z) - 31
            x0, y0 = max(left, 0), max(top, 0); x1, y1 = min(left + 64, W), min(top + 32, H)
            if x1 <= x0 or y1 <= y0: continue
            src = img[y0 - top:y1 - top, x0 - left:x1 - left]; m = src[..., 3] > 0
            cv[y0:y1, x0:x1][m] = src[m]
    return cv
G, D, S = layer(grass), layer(dirt), layer(stone)
# 픽셀마다 논리 좌표
py, px = np.mgrid[0:H, 0:W]
wx = px - ox; wy = oy - py
lx = (wx / 32.0 + wy / 16.0) / 2.0; lz = (wy / 16.0 - wx / 32.0) / 2.0
in_map = (lx >= 0) & (lx < MW) & (lz >= 0) & (lz < MH)
zdist = np.full((H, W), 99.0); in_zone = np.zeros((H, W), bool)
for zx, zz in zones:
    dx = np.maximum(np.maximum(zx - lx, lx - (zx + 6)), 0); dz = np.maximum(np.maximum(zz - lz, lz - (zz + 8)), 0)
    d = np.maximum(dx, dz); zdist = np.minimum(zdist, d); in_zone |= (d == 0)
dirt_mask = zdist <= apron
def noise(t, seed):
    return amp * (math.sin(seed) * 0 + np.sin(t * 1.9 + seed) + 0.5 * np.sin(t * 3.7 + seed * 2.3) + 0.25 * np.sin(t * 7.1 + seed * 0.7))
hw = ratio * w / 2.0
road = np.zeros((H, W), bool)
for k in range(ROWS + 1):                       # X 방향 복도(가로 줄)
    zc = w / 2.0 + k * (8 + w)
    road |= (lz > zc - hw + noise(lx, 1.0 + k)) & (lz < zc + hw + noise(lx, 4.0 + k))
for k in range(COLS + 1):                       # Z 방향 복도(세로 줄)
    xc = w / 2.0 + k * (6 + w)
    road |= (lx > xc - hw + noise(lz, 7.0 + k)) & (lx < xc + hw + noise(lz, 10.0 + k))
road &= in_map & ~in_zone
canvas = np.where(dirt_mask[..., None], D, G)
sm = road & (S[..., 3] > 0)
canvas[sm] = S[sm]
# 흙↔잔디 경계선 1px: 흙 마스크의 바깥 테두리
er = dirt_mask.copy()
er[1:, :] &= dirt_mask[:-1, :]; er[:-1, :] &= dirt_mask[1:, :]; er[:, 1:] &= dirt_mask[:, :-1]; er[:, :-1] &= dirt_mask[:, 1:]
line = dirt_mask & ~er & ~sm
canvas[line] = (52, 32, 32, 255)
# 서 있는 것
def blit(img, pcol, prow, wx_, wy_):
    h, w_ = img.shape[:2]; left = ox + wx_ - pcol; top = oy - wy_ - (h - 1 - prow)
    x0, y0 = max(left, 0), max(top, 0); x1, y1 = min(left + w_, W), min(top + h, H)
    if x1 <= x0 or y1 <= y0: return
    src = img[y0 - top:y1 - top, x0 - left:x1 - left]; m = src[..., 3] > 0
    canvas[y0:y1, x0:x1][m] = src[m]
items = []; ff = fence[:, ::-1]
for zx, zz in zones:
    for x in range(zx, zx + 6):
        items.append((sy(x, zz), fence, 4, sx(x, zz))); items.append((sy(x, zz + 8), fence, 4, sx(x, zz + 8)))
    for z in range(zz, zz + 8):
        items.append((sy(zx, z), ff, 33, sx(zx, z))); items.append((sy(zx + 6, z), ff, 33, sx(zx + 6, z)))
    items.append((sy(zx + 6, zz + 8), post, 4, sx(zx + 6, zz + 8)))
zx, zz = zones[0]
for (x, z) in ((zx + 2.0, zz + 3.5), (zx + 4.5, zz + 5.5), (zx + 1.5, zz + 6.5)):
    items.append((int(16 * (x + z)), wom, wom.shape[1] // 2, int(32 * (x - z))))
for wy_, img, pc, wx_ in sorted(items, key=lambda t: -t[0]):
    blit(img, pc, 0, wx_, wy_)
out = Image.fromarray(canvas)
out = out.crop((ox + sx(X0 + 2, Z1 - 1), 0, ox + sx(X1 - 1, Z0 + 2) + 64, H))
out.save(OUTF)
# 확대: 첫 존 앞쪽(바깥 복도 + 안쪽 복도가 만나는 모서리)
cx = ox + sx(zx + 6, zz) - (ox + sx(X0 + 2, Z1 - 1)); cy = oy - sy(zx + 6, zz)
out.crop((cx - 300, cy - 200, cx + 220, cy + 160)).resize((1040, 720), Image.NEAREST).save(OUTF.replace('.png', '_zoom.png'))
print('ok', out.size, 'map', MW, 'x', MH)
