# 별 평가 소식지 「굴 소식」 틀(설계 40, 2026-10-02 사용자 선택 B3 「제호 띠」, 처음 시안 A 신문 한 장 · B 주황 테 신문 · C 상장 → 「B에서 좀 더 디벨롭」 → B1 제호 꾸밈 · B2 신문지 + 큰 귀는 버림).
# 64×64칸 9-slice, border [왼 7, 아래 7, 오른 9, 위 30]: 진갈색 · 주황 띠 3(위 밝게 · 아래 어둡게) · 진갈색 · 크림 종이.
#   위쪽 테에 제호 칸(옅은 크림 띠 5~23줄, 「굴 소식」 글자는 TMP로 얹는다) + 제호 아래 이중 선(굵은 2 + 가는 1),
#   오른쪽 위 접힌 귀 7칸, 나머지 세 귀 리벳. 무늬는 모서리 · 테 줄에만(늘어나는 가운데는 민무늬).
# 출력 ../news_frame.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_news_frame.py → 메뉴 Import UI Sprites
import json
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
W, H = 64, 64
INK = (52, 32, 32)
PAPER, PAPER_D, BAND, CREAM = (246, 238, 220), (226, 214, 192), (240, 222, 196), (251, 244, 230)
ORANGE, ORANGE_D, ORANGE_L = (216, 120, 72), (176, 92, 52), (240, 168, 120)


def put(a, region, col):
    a[region] = col + (255,)


def ring(a, inset, col, thick=1):
    for i in range(inset, inset + thick):
        put(a, (i, slice(i, W - i)), col); put(a, (H - 1 - i, slice(i, W - i)), col)
        put(a, (slice(i, H - i), i), col); put(a, (slice(i, H - i), W - 1 - i), col)


def rivet(a, cy, cx):
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            put(a, (cy + dy, cx + dx), INK if abs(dy) + abs(dx) == 2 else CREAM)
    put(a, (cy, cx), (230, 214, 196))


a = np.zeros((H, W, 4), np.uint8)
a[:, :] = PAPER + (255,)
for y, x in ((0, 0), (0, W - 1), (H - 1, 0), (H - 1, W - 1)):
    a[y, x] = 0
ring(a, 0, INK)
ring(a, 1, ORANGE, 3)
put(a, (1, slice(2, W - 2)), ORANGE_L)
put(a, (H - 2, slice(2, W - 2)), ORANGE_D)
ring(a, 4, INK)
put(a, (slice(5, 24), slice(5, W - 5)), BAND)
put(a, (5, slice(5, W - 5)), CREAM)
put(a, (slice(24, 26), slice(5, W - 5)), INK)
put(a, (27, slice(5, W - 5)), INK)
# 접힌 귀(오른쪽 위 7칸): 바깥은 비우고 안쪽 삼각형은 종이 그늘, 접힌 선 · 테두리 진갈색
n = 7
for k in range(n):
    a[k, W - n + k:W] = 0
    put(a, (k, slice(W - n, W - n + k)), PAPER_D)
    put(a, (k, W - n + k), INK)
put(a, (n, slice(W - n, W)), INK)
put(a, (slice(0, n), W - n), INK)
rivet(a, 2, 2); rivet(a, H - 3, 2); rivet(a, H - 3, W - 3)
Image.fromarray(a).save(os.path.join(OUT, 'news_frame.png'))

path = os.path.join(OUT, 'ui_slices.json')
slices = json.load(open(path, encoding='utf-8'))
slices['news_frame'] = {'w': W, 'h': H, 'border': [7, 7, 9, 30]}
json.dump(slices, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('news_frame', W, 'x', H)
