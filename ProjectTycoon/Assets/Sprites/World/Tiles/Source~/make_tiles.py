# -*- coding: utf-8 -*-
# top-down 텍스처 → 픽셀 격자 정규화 → 화면 공간 2:1 압축 → 마름모 타일 18장 시트(+ 미리보기)
# 사용: make_tiles.py <원본.png> <출력폴더> <시트이름> [G=96] [팔레트문턱=0.0005] [--key-white] [--drop=0.3 --seed=1] [--k=1]
# --k: 한 칸의 화면 크기 배수. 칸은 화면에서 2k×k px(아이소 2:1). 시트 크기를 유지하려면 G = 96 ÷ k. 굴 계열(한 칸 4px 폭)은 --k=2, G=48
# 투명 배경을 살린다(RGBA). RGB 원본은 --key-white로 흰 배경(모서리에서 이어진 부분만)을 투명으로 만든다.
# --drop은 돌 덩어리(연결 성분)를 그 비율만큼 지워 성긴 변형을 만든다
from PIL import Image
import numpy as np, sys
from collections import deque
args = [a for a in sys.argv[1:] if not a.startswith('--')]
opts = dict(a[2:].split('=') if '=' in a else (a[2:], '1') for a in sys.argv[1:] if a.startswith('--'))
SRC, OUT, NAME = args[0], args[1], args[2]
G = int(args[3]) if len(args) > 3 else 96
TH = float(args[4]) if len(args) > 4 else 0.0005
img = Image.open(SRC).convert('RGBA')
a = np.asarray(img).astype(int); W = a.shape[0]
alpha = a[..., 3] >= 128
if 'key-white' in opts:
    near = (np.abs(a[..., :3] - 255).sum(2) < 60)
    bg = np.zeros((W, W), bool); q = deque()
    for y in range(W):
        for x in (0, W - 1):
            if near[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    for x in range(W):
        for y in (0, W - 1):
            if near[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < W and 0 <= nx < W and near[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True; q.append((ny, nx))
    alpha &= ~bg
# 팔레트: 불투명 픽셀만, 빈도순 탐욕 군집
full = a[..., :3][alpha] // 12 * 12
cols, counts = np.unique(full, axis=0, return_counts=True)
order = np.argsort(-counts); pal = []
for i in order:
    if counts[i] < full.shape[0] * TH: break
    c = cols[i].astype(int)
    if all(np.abs(c - q).sum() > 30 for q in pal): pal.append(c)
pal = np.array(pal); print('palette', [tuple(int(v) for v in c) for c in pal])
# 격자 정규화(최근접)
idx = (np.arange(G) + 0.5) * W / G
idx = idx.astype(int)
sm = a[idx][:, idx][..., :3]; sa = alpha[idx][:, idx]
dist = ((sm[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(3)
small = pal[dist.argmin(2)].astype(np.uint8)
if 'drop' in opts:
    rng = np.random.default_rng(int(opts.get('seed', '1')))
    lab = np.full((G, G), -1); n = 0
    for y in range(G):
        for x in range(G):
            if sa[y, x] and lab[y, x] < 0:
                q = deque([(y, x)]); lab[y, x] = n
                while q:
                    cy, cx = q.popleft()
                    for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                        ny %= G; nx %= G   # seamless: 가장자리를 넘어 이어진 돌은 한 덩어리
                        if sa[ny, nx] and lab[ny, nx] < 0: lab[ny, nx] = n; q.append((ny, nx))
                n += 1
    kill = rng.random(n) < float(opts['drop'])
    sa = sa & ~kill[np.clip(lab, 0, None)]
    print('components', n, 'dropped', int(kill.sum()))
print('coverage', round(float(sa.mean()), 3))
# 화면 공간 텍스처(텍셀 2×1)
K = int(opts.get('k', '1'))
tex = np.repeat(np.repeat(small, 2 * K, axis=1), K, axis=0); ta = np.repeat(np.repeat(sa, 2 * K, axis=1), K, axis=0)
TW, THh = 2 * K * G, K * G
def tile(d, s_):
    cx, cy = 32 * d, 16 * s_
    im = np.zeros((32, 64, 4), np.uint8)
    for y in range(32):
        hw = (y + 1) * 2 if y < 16 else (32 - y) * 2
        for x in range(32 - hw, 32 + hw):
            gx = cx + (x - 32); gy = cy + (16 - y)
            u = gx % TW; v = (-gy) % THh
            if ta[v, u]: im[y, x, :3] = tex[v, u]; im[y, x, 3] = 255
    return im
P = TW // 32
tiles = [tile(d, (d % 2) + 2 * k) for d in range(P) for k in range(3)]
Image.fromarray(np.concatenate(tiles, axis=1)).save(OUT + '/' + NAME + '.png')
print('tiles', len(tiles), '->', OUT + '/' + NAME + '.png')
