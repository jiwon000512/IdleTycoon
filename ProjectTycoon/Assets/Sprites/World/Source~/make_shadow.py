# -*- coding: utf-8 -*-
# 발밑 그림자: 칸 격자 타원(한 칸 = 2px, PPU 80). 색은 흰색이고 게임이 검정 × 알파 0.35로 칠하므로 알파만 뜻이 있다
# 사용: make_shadow.py <출력.png> <폭칸> <높이칸> [테알파 0~1, 기본 1 = 단색]
# 지금 shadow.png = make_shadow.py ../shadow.png 40 10 (2026-09-24, 시안 A 단색 · B 2톤 테 0.5 · C 30×8칸 중 A). 게임에서 세로로 누르지 않는다
import sys, numpy as np
from PIL import Image
out, w, h = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
rim = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
PX = 2
def inside(W, H, x, y):
    return ((x + 0.5 - W / 2) / (W / 2)) ** 2 + ((y + 0.5 - H / 2) / (H / 2)) ** 2 <= 1
a = np.zeros((h, w))
for y in range(h):
    for x in range(w):
        if inside(w, h, x, y):
            # 테: 안쪽 타원(좌우 4칸·위아래 2칸 작게) 밖이면 옅게
            a[y, x] = 1.0 if inside(w - 8, h - 4, x - 4, y - 2) else rim
img = np.zeros((h * PX, w * PX, 4), np.uint8)
img[..., :3] = 255
img[..., 3] = np.kron(a, np.ones((PX, PX))) * 255
Image.fromarray(img).save(out)
print(out, (w * PX, h * PX))
