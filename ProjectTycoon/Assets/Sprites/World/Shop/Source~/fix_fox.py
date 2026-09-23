# -*- coding: utf-8 -*-
# 여우 보정(2026-09-23 사용자 지적). ① 꼬리가 오른쪽에 붙어 몸 중심이 피벗(하단 중앙)보다 왼쪽 → 왼쪽에 투명 PAD칸을 채워 몸 중심 = 스프라이트 중앙.
# ② 뒷모습에서 꼬리가 몸 뒤로 보임 → 꼬리 덩어리를 SHIFT칸 안쪽으로 옮기고 외곽선을 몸 위에 다시 그려 앞에 오게.
import numpy as np
from PIL import Image
PAD, SHIFT = 9, 3
OUT = (48, 24, 24)
def cells(p): return np.asarray(Image.open(p).convert('RGBA'))[::2, ::2].copy()
def save(g, p): Image.fromarray(np.repeat(np.repeat(g, 2, 0), 2, 1)).save(p); print(p, g.shape[1], 'x', g.shape[0])
def pad_left(g): return np.concatenate([np.zeros((g.shape[0], PAD, 4), np.uint8), g], axis=1)

front = cells('fox_front_unpadded.png'); save(pad_left(front), 'fox_front.png')

back = cells('fox_back_unpadded.png')
y0, y1, x0 = 20, 41, 31  # 꼬리 영역(몸 오른쪽 외곽선 바깥)
tail = back[y0:y1, x0:].copy(); alpha = tail[..., 3] > 0
is_out = alpha & (np.abs(tail[..., :3].astype(int) - OUT).sum(2) < 40)
fill = alpha & ~is_out
back[y0:y1, x0:] = 0  # 꼬리 지움(몸 외곽선 col 30 이하는 남는다)
# 꼬리 채움을 SHIFT칸 왼쪽으로, 외곽선은 채움을 1칸 팽창해 몸 위에 그린다
dst = back[y0:y1, x0 - SHIFT:x0 - SHIFT + tail.shape[1]]
pf = np.pad(fill, 1); ring = (pf[:-2, 1:-1] | pf[2:, 1:-1] | pf[1:-1, :-2] | pf[1:-1, 2:]) & ~fill
dst[ring] = (*OUT, 255); dst[fill] = tail[fill]
# 몸 외곽선이 꼬리 바로 왼쪽에 이중선으로 남는 칸(꼬리 외곽선과 붙은 몸 외곽선)은 몸 색으로 메운다
body_fill = (216, 120, 72, 255)
for y in range(y0, y1):
    row = back[y]
    for x in range(x0 - SHIFT - 2, x0 - SHIFT + 1):
        if x + 1 < back.shape[1] and row[x, 3] and np.abs(row[x, :3].astype(int) - OUT).sum() < 40 and row[x + 1, 3] and np.abs(row[x + 1, :3].astype(int) - OUT).sum() < 40 and row[x - 1, 3] and np.abs(row[x - 1, :3].astype(int) - OUT).sum() >= 40:
            row[x] = body_fill
save(pad_left(back), 'fox_back.png')
