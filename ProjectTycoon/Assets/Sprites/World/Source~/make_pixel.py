# -*- coding: utf-8 -*-
# 서 있는 물체 스프라이트(격자 강제): Codex 생성물(흰 배경) → 흰 배경 투명 → 내용 크롭 → 원본의 픽셀 격자를 재서
# 한 칸 = 한 색으로 모으고(칸마다 최빈색) → 팔레트 정규화 → 한 칸 = PX 픽셀로 정수 배 확대.
# 결과: 모든 에셋의 한 칸이 화면에서 같은 크기(아트-프롬프트 0장 "화면별 한 칸 크기").
# 사용: make_pixel.py <원본.png> <출력.png> [--px=4] [--cellsw=N 가로 칸 수 강제 | --cells=N 세로 칸 수 강제] [--th=0.004 팔레트 문턱] [--thin 바깥 외곽선 2칸 → 1칸]
# 격자 자동 측정은 반 주기를 잡기도 한다. 같은 묶음(굴 1·2·3단계 등)은 --cellsw로 같은 칸 수를 준다
from PIL import Image
import numpy as np, sys
from collections import deque, Counter

args = [a for a in sys.argv[1:] if not a.startswith('--')]
opts = dict((a[2:].split('=') + ['1'])[:2] for a in sys.argv[1:] if a.startswith('--'))
SRC, OUT = args[0], args[1]
PX = int(opts.get('px', 4))
TH = float(opts.get('th', 0.004))

img = Image.open(SRC).convert('RGBA')
a = np.asarray(img).astype(int)
H, W = a.shape[:2]

# 흰 배경: 네 변에서 이어진 흰색만 투명(안쪽 흰 하이라이트는 남긴다)
near = (np.abs(a[..., :3] - 255).sum(2) < 60) & (a[..., 3] > 0)
bg = np.zeros((H, W), bool)
q = deque()
for y in range(H):
    for x in (0, W - 1):
        if near[y, x] and not bg[y, x]:
            bg[y, x] = True; q.append((y, x))
for x in range(W):
    for y in (0, H - 1):
        if near[y, x] and not bg[y, x]:
            bg[y, x] = True; q.append((y, x))
while q:
    y, x = q.popleft()
    for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
        if 0 <= ny < H and 0 <= nx < W and near[ny, nx] and not bg[ny, nx]:
            bg[ny, nx] = True; q.append((ny, nx))
alpha = (a[..., 3] >= 128) & ~bg
ys, xs = np.nonzero(alpha)
y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
crop = a[y0:y1, x0:x1, :3]
calpha = alpha[y0:y1, x0:x1]
ch, cw = calpha.shape


# 원본 격자 = 색 경계가 반복되는 간격(가로·세로 FFT 최대 봉우리의 중앙값)
def grid_period(g):
    res = []
    for axis in (0, 1):
        d = np.abs(np.diff(g, axis=axis))
        e = (d > 25).sum(axis=1 - axis).astype(float)
        e -= e.mean()
        f = np.abs(np.fft.rfft(e))
        fr = np.fft.rfftfreq(len(e))
        m = (fr > 1 / 80) & (fr < 1 / 4)
        res.append(1 / fr[m][np.argmax(f[m])])
    return float(np.median(res))


if 'cellsw' in opts:
    period = cw / int(opts['cellsw'])
    cells_h = max(1, int(round(ch / period)))
elif 'cells' in opts:
    cells_h = int(opts['cells'])
    period = ch / cells_h
else:
    period = grid_period(crop.mean(2))
    cells_h = max(1, int(round(ch / period)))
cells_w = max(1, int(round(cw / period)))
print('source period', round(period, 1), 'cells', cells_w, 'x', cells_h)

# 칸마다 최빈색(색은 12 단위로 묶어 센다), 불투명 판정은 과반
qcol = crop // 12 * 12
small = np.zeros((cells_h, cells_w, 3), int)
sa = np.zeros((cells_h, cells_w), bool)
for cy in range(cells_h):
    ya, yb = int(cy * ch / cells_h), max(int((cy + 1) * ch / cells_h), int(cy * ch / cells_h) + 1)
    for cx in range(cells_w):
        xa, xb = int(cx * cw / cells_w), max(int((cx + 1) * cw / cells_w), int(cx * cw / cells_w) + 1)
        m = calpha[ya:yb, xa:xb]
        if m.mean() < 0.5:
            continue
        sa[cy, cx] = True
        block = qcol[ya:yb, xa:xb][m]
        small[cy, cx] = Counter(map(tuple, block)).most_common(1)[0][0]

# 팔레트: 빈도순 탐욕 군집 후 가장 가까운 색으로
cols, counts = np.unique(small[sa], axis=0, return_counts=True)
order = np.argsort(-counts)
pal = []
for i in order:
    if counts[i] < sa.sum() * TH:
        break
    c = cols[i].astype(int)
    if all(np.abs(c - p).sum() > 30 for p in pal):
        pal.append(c)
pal = np.array(pal)
dist = ((small[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(3)
rgb = pal[dist.argmin(2)].astype(np.uint8)
print('palette', len(pal), [tuple(int(v) for v in c) for c in pal])

# --thin: 바깥 외곽선이 2칸 두께면 가장 바깥 칸을 지워 1칸으로(칸이 커지면 원본의 2칸 선이 너무 굵다)
if 'thin' in opts:
    dark = np.argmin(pal.sum(1))
    line = sa & (dist.argmin(2) == dark)
    pad = np.pad(sa, 1)
    outer = line & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    lp = np.pad(line & ~outer, 1)
    inner = lp[:-2, 1:-1] | lp[2:, 1:-1] | lp[1:-1, :-2] | lp[1:-1, 2:]
    drop = outer & inner
    sa = sa & ~drop
    print('thinned', int(drop.sum()), 'cells')

out = np.zeros((cells_h, cells_w, 4), np.uint8)
out[..., :3] = rgb
out[..., 3] = np.where(sa, 255, 0)
out = np.repeat(np.repeat(out, PX, axis=0), PX, axis=1)
# 폭을 짝수로 맞춰 피벗 하단 중앙이 픽셀 경계에 오게
if out.shape[1] % 2:
    out = np.concatenate([out, np.zeros((out.shape[0], 1, 4), np.uint8)], axis=1)
Image.fromarray(out).save(OUT)
print('ok', out.shape[1], 'x', out.shape[0], '->', OUT)
