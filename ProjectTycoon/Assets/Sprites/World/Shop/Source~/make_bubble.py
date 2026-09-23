# -*- coding: utf-8 -*-
# 가게 말풍선(bubble)·화남(angry) 스프라이트를 칸 단위로 그린다. 32×36칸(꼬리 4칸), 한 칸 2px, 외곽선 1칸 (52,32,32)
import numpy as np
from PIL import Image
W, H, TAIL, R = 32, 32, 4, 6
OUT = (52, 32, 32)
def draw(fill, marks):
    g = np.zeros((H + TAIL, W, 4), np.uint8)
    body = np.zeros((H + TAIL, W), bool)
    for y in range(H):
        for x in range(W):
            dx = max(R - x, x - (W - 1 - R), 0); dy = max(R - y, y - (H - 1 - R), 0)
            body[y, x] = dx * dx + dy * dy <= R * R + R
    for i in range(TAIL):  # 꼬리: 아래로 갈수록 좁아짐
        w = TAIL - i
        body[H + i, W // 2 - w:W // 2 + w] = True
    pad = np.pad(body, 1)
    inner = pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    g[body] = (*OUT, 255); g[inner] = (*fill, 255)
    for (y0, y1, x0, x1, c) in marks: g[y0:y1, x0:x1] = (*c, 255)
    return Image.fromarray(np.repeat(np.repeat(g, 2, 0), 2, 1))
draw((240, 228, 216), []).save('bubble.png')
cream = (250, 240, 230)
draw((216, 84, 84), [(7, 19, 11, 14, cream), (21, 24, 11, 14, cream), (7, 19, 18, 21, cream), (21, 24, 18, 21, cream)]).save('angry.png')
print('bubble', Image.open('bubble.png').size)
