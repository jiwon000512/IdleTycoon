# -*- coding: utf-8 -*-
# 울타리 한 칸(기둥 + 2:1 가로대 2개)과 기둥만. 팔레트는 Codex 참조(fence_ref.png)에서. 칸 = 기둥 중심에서 오른쪽 32px·위 16px
from PIL import Image
import numpy as np, sys
OUT = sys.argv[1]
OUT_LINE = (36, 24, 24, 255); FILL = (168, 132, 108, 255); HI = (192, 144, 120, 255)
HI2 = (216, 180, 156, 255); SH = (132, 96, 84, 255)
W, H = 38, 32; PX = 4.0  # 기둥 중심(연속 좌표) = 피벗 x
def blank(w, h): return np.zeros((h, w, 4), np.uint8)
def put(im, x, y, c):
    if 0 <= x < im.shape[1] and 0 <= y < im.shape[0]: im[im.shape[0] - 1 - y, x] = c
def post(im, ox):
    for y in range(0, 22):
        for x in range(1, 7):
            c = OUT_LINE if (y == 0 or x in (1, 6)) else (HI if x == 2 else SH if x == 5 else FILL)
            put(im, ox + x, y, c)
    for y in range(22, 26):
        for x in range(0, 8):
            c = OUT_LINE if (y in (22, 25) or x in (0, 7)) else HI
            put(im, ox + x, y, c)
def rail(im, h):
    for x in range(7, 33):
        yb = h + (x - 4) // 2
        put(im, x, yb - 1, OUT_LINE); put(im, x, yb + 3, OUT_LINE)
        put(im, x, yb, SH); put(im, x, yb + 1, FILL); put(im, x, yb + 2, HI if x > 12 else HI2)
im = blank(W, H); rail(im, 6); rail(im, 14); post(im, 0)
Image.fromarray(im).save(OUT + '/fence.png')
p = blank(8, 26); post(p, 0); Image.fromarray(p).save(OUT + '/fence_post.png')
# 미리보기: 앞변 6칸 + 오른쪽 변 4칸(좌우 반전) + 모서리 기둥, 흙 위, 3배
cw, ch = 420, 200; prev = np.zeros((ch, cw, 4), np.uint8); prev[..., :3] = (201, 168, 106); prev[..., 3] = 255
def blit(dst, src, px, py, flip=False):
    s = src[:, ::-1] if flip else src
    h, w = s.shape[:2]; ox = px - (w - 1 - int(PX)) if flip else px - int(PX); oy = py - h + 1
    for y in range(h):
        for x in range(w):
            if s[y, x, 3] and 0 <= oy + y < dst.shape[0] and 0 <= ox + x < dst.shape[1]: dst[oy + y, ox + x] = s[y, x]
pieces = []
for i in range(6): pieces.append((40 + i * 32, 170 - i * 16, False))      # 앞변: +X = 오른쪽 위
for j in range(4): pieces.append((40 + 6 * 32 - j * 32, 170 - 6 * 16 - j * 16, True))  # 오른쪽 변: +Z = 왼쪽 위
pieces.sort(key=lambda t: -t[1])  # 화면 y 큰 것(먼 것)부터
for (x, y, f) in pieces: blit(prev, im, x, y, f)
blit(prev, p, 40 + 6 * 32 - 4 * 32, 170 - 6 * 16 - 4 * 16)  # 뒤 모서리 기둥
Image.fromarray(prev).resize((cw * 3, ch * 3), Image.NEAREST).save(OUT + '/preview.png')
print('ok', im.shape, p.shape)
