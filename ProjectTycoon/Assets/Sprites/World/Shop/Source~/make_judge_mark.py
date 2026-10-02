# 별 평가 평가단 손님 머리 위 표시(설계 40, 2026-10-02 사용자 「머리 위 표시로」 → 선택 A 「수첩과 연필」 → 「좀 더 크게」).
# 시안 B 작은 금별 · C 포크와 나이프는 버림. 몸에 겹치지 않고 머리 위에 뜨는 월드 그림(한 칸 2px · PPU 80), 19×16칸(외곽선 포함).
#   수첩: 주황 표지(고리 다섯) · 크림 종이 · 줄 넷 · 오른쪽 · 아래 쪽 두께 그늘. 연필: 오른쪽 위 분홍 지우개 · 쇠 띠 · 노란 몸(아래쪽 한 톤 어둡게) · 나무 끝 · 심.
#   말풍선(♥ · ✨ · 🤢)과 같은 자리라 말풍선이 뜰 때 숨기거나 올리는 것은 코드.
# 출력 ../judge_mark.png. 실행: Windows Python(Pillow · numpy) make_judge_mark.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (52, 32, 32)
PAPER, PAPER_D, LINE = (246, 238, 220), (214, 202, 186), (196, 184, 168)
ORANGE, ORANGE_L = (216, 120, 72), (240, 168, 120)
YELLOW, YELLOW_D = (240, 196, 96), (198, 150, 60)
PINK, METAL, WOOD, LEAD = (230, 150, 150), (176, 170, 164), (222, 176, 130), (60, 44, 40)
W, H = 17, 14

a = np.zeros((H, W, 4), np.uint8)


def put(x, y, col):
    a[y, x] = col + (255,)


# 수첩(x 0~11, y 0~13)
for y in range(14):
    for x in range(12):
        put(x, y, PAPER)
for x in range(12):
    put(x, 0, ORANGE_L); put(x, 1, ORANGE)
for x in (1, 3, 5, 7, 9):
    put(x, 1, INK)
for y in (4, 6, 8, 10):
    for x in range(1, 10):
        put(x, y, LINE)
for y in range(2, 14):
    put(11, y, PAPER_D)
for x in range(12):
    put(x, 13, PAPER_D)
# 연필: 오른쪽 위 → 왼쪽 아래 두 칸 굵기(왼쪽 칸 밝게, 오른쪽 칸 어둡게)
for t in range(11):
    x, y = 15 - t, 2 + t
    if t <= 1:
        c0 = c1 = PINK
    elif t == 2:
        c0 = c1 = METAL
    elif t <= 8:
        c0, c1 = YELLOW, YELLOW_D
    elif t == 9:
        c0 = c1 = WOOD
    else:
        put(x, y, LEAD)
        continue
    put(x, y, c0); put(x + 1, y, c1)

out = np.zeros((H + 2, W + 2, 4), np.uint8)
out[1:-1, 1:-1] = a
f = out[..., 3] > 0
g = f.copy()
g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
out[g & ~f] = INK + (255,)
Image.fromarray(np.repeat(np.repeat(out, 2, 0), 2, 1)).save(os.path.join(HERE, '..', 'judge_mark.png'))
print('judge_mark', out.shape[1], 'x', out.shape[0], 'cells', len(np.unique(out[out[..., 3] > 0][:, :3], axis=0)), 'colors')
