# -*- coding: utf-8 -*-
# 발밑 그림자: 칸 격자 단색 타원(한 칸 = 2px, PPU 80). 색은 흰색이고 렌더러가 검정 × 알파 0.35로 칠하므로 알파만 뜻이 있다
# 사용: make_shadow.py <출력.png> <폭칸> <높이칸>
# 지금 shadow.png = make_shadow.py ../shadow.png 30 8 (2026-09-24 리뷰: 40×10칸은 줄 간격 0.8보다 넓어 손님끼리 겹치고 옆벽을 덮어 시안 C로). 게임에서 세로로 누르지 않는다
import sys, numpy as np
from PIL import Image
out, w, h = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
PX = 2
y, x = np.mgrid[0:h, 0:w]
a = ((x + 0.5 - w / 2) / (w / 2)) ** 2 + ((y + 0.5 - h / 2) / (h / 2)) ** 2 <= 1
img = np.zeros((h * PX, w * PX, 4), np.uint8)
img[..., :3] = 255
img[..., 3] = np.kron(a, np.ones((PX, PX))) * 255
Image.fromarray(img).save(out)
print(out, (w * PX, h * PX))
