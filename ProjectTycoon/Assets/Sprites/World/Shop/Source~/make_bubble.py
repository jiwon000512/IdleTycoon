# -*- coding: utf-8 -*-
# 가게 말풍선 스프라이트를 칸 단위로 그린다. 32×36칸(꼬리 4칸), 한 칸 2px, 외곽선 1칸 (52,32,32). 결과는 이 폴더의 위(Shop/)에
#   bubble.png(빈 말풍선, 32×36칸)
#   wait.png · wait_sheet.png(2026-09-25): 빈 진열대 앞에서 기다리는 손님 머리 위. 가로로 넓은 26×15칸 + 꼬리 3칸(52×36px),
#     점 셋 「…」이 하나씩 늘어나는 3프레임 시트(프레임 52px 폭) + 점 셋 정지. 시안 A 점 셋 · B 두리번 눈 · C 빈 빵 + 물음표 중 A, 사용자가 더 작고 가로로 넓게
#   옛 angry.png(빨강 「!!」)는 기다림 말풍선으로 바뀌어 만들지 않는다
import os
import numpy as np
from PIL import Image

DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
W, H, TAIL, R = 32, 32, 4, 6
WAIT = (26, 15)  # 기다림 말풍선 몸통 폭·높이(칸)
OUT = (52, 32, 32)
CREAM = (240, 228, 216)
BROWN = (92, 76, 66)


def cells(fill, marks=(), paint=None, W=W, H=H, TAIL=TAIL, R=R):
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
    if paint:
        paint(g)
    return g


def image(g):
    return Image.fromarray(np.repeat(np.repeat(g, 2, 0), 2, 1))


def sheet(frames):
    return image(np.concatenate(frames, axis=1))


def dot(g, y, x, c):  # 4×4 둥근 점
    g[y:y + 4, x + 1:x + 3] = (*c, 255)
    g[y + 1:y + 3, x:x + 4] = (*c, 255)


def dots(n, w, h):
    # 가로로 넓은 작은 말풍선(꼬리 3칸, 모서리 4칸) 가운데에 점 n개, 간격 = 폭 26칸 이상 3칸 · 그 아래 2칸
    gap = 3 if w >= 26 else 2
    x0, y0 = (w - (12 + gap * 2)) // 2, (h - 4) // 2

    def paint(g):
        for i in range(n):
            dot(g, y0, x0 + i * (4 + gap), BROWN)
    return cells(CREAM, paint=paint, W=w, H=h, TAIL=3, R=4)


if __name__ == '__main__':
    image(cells(CREAM)).save(os.path.join(DIR, 'bubble.png'))
    sheet([dots(n, *WAIT) for n in (1, 2, 3)]).save(os.path.join(DIR, 'wait_sheet.png'))
    image(dots(3, *WAIT)).save(os.path.join(DIR, 'wait.png'))
    print('bubble 64x72, wait %dx%d cells x 3 frames ->' % (WAIT[0], WAIT[1] + 3), os.path.abspath(DIR))
