# -*- coding: utf-8 -*-
# 서 있는 물체 스프라이트: Codex 생성물(흰 배경) → 흰 배경 투명(모서리에서 이어진 부분만) → 팔레트 정규화 → 내용 크롭 → 목표 높이로 최근접 축소
# 사용: make_sprite.py <원본.png> <출력.png> <목표 높이 px> [팔레트 문턱=0.004]
from PIL import Image
import numpy as np, sys
from collections import deque
SRC, OUT, HEIGHT = sys.argv[1], sys.argv[2], int(sys.argv[3])
TH = float(sys.argv[4]) if len(sys.argv) > 4 else 0.004
img = Image.open(SRC).convert('RGBA'); a = np.asarray(img).astype(int); H, W = a.shape[:2]
near = (np.abs(a[..., :3] - 255).sum(2) < 60) & (a[..., 3] > 0)
bg = np.zeros((H, W), bool); q = deque()
for y in range(H):
    for x in (0, W - 1):
        if near[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
for x in range(W):
    for y in (0, H - 1):
        if near[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
while q:
    y, x = q.popleft()
    for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
        if 0 <= ny < H and 0 <= nx < W and near[ny, nx] and not bg[ny, nx]:
            bg[ny, nx] = True; q.append((ny, nx))
alpha = (a[..., 3] >= 128) & ~bg
ys, xs = np.nonzero(alpha)
y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
crop = a[y0:y1, x0:x1]; calpha = alpha[y0:y1, x0:x1]
full = crop[..., :3][calpha] // 12 * 12
cols, counts = np.unique(full, axis=0, return_counts=True)
order = np.argsort(-counts); pal = []
for i in order:
    if counts[i] < full.shape[0] * TH: break
    c = cols[i].astype(int)
    if all(np.abs(c - p).sum() > 30 for p in pal): pal.append(c)
pal = np.array(pal); print('palette', [tuple(int(v) for v in c) for c in pal])
ch, cw = crop.shape[:2]
scale = HEIGHT / ch; tw = max(1, int(round(cw * scale)))
iy = ((np.arange(HEIGHT) + 0.5) / scale).astype(int).clip(0, ch - 1)
ix = ((np.arange(tw) + 0.5) / scale).astype(int).clip(0, cw - 1)
sm = crop[iy][:, ix][..., :3]; sa = calpha[iy][:, ix]
dist = ((sm[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(3)
rgb = pal[dist.argmin(2)].astype(np.uint8)
out = np.zeros((HEIGHT, tw, 4), np.uint8); out[..., :3] = rgb; out[..., 3] = np.where(sa, 255, 0)
# 폭을 짝수로 맞춰 피벗 하단 중앙이 픽셀 경계에 오게
if tw % 2: out = np.concatenate([out, np.zeros((HEIGHT, 1, 4), np.uint8)], axis=1)
Image.fromarray(out).save(OUT); print('ok', out.shape[1], 'x', out.shape[0], '->', OUT)
