# -*- coding: utf-8 -*-
# 반짝돌 뽑기 카드 뒷면(설계 36, 2026-10-01 사용자 선택 A 「반짝돌 무늬」). 메인의 큰 카드 한 장 · 결과 팝업에서 뒤집히기 전 그림.
#   69×95 UI칸(= 276×380px, 1 UI px = 화면 4px). 칸 단위로 직접 그린다: 진갈색 외곽선 · 둥근 모서리 · 크림 바깥 테 · 캐러멜 바탕(메뉴 단추 색)
#   위 마름모 격자 · 가운데 크림 원판 속 민트 반짝돌(반짝돌 아이콘 색). 시안 B 너구리 보자기 · C 별밤은 버림.
# 출력 ../card_back.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_card_back.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
W, H = 69, 95
INK = (52, 32, 32)
CREAM, CREAM_D = (251, 244, 230), (217, 200, 180)
MINT, MINT_D, MINT_L = (96, 196, 164), (64, 150, 128), (190, 240, 220)
Y, X = np.mgrid[0:H, 0:W]


def rrect(x0, y0, x1, y1, r):
    # 둥근 사각형 마스크(칸 단위, 모서리 반지름 r)
    m = (X >= x0) & (X <= x1) & (Y >= y0) & (Y <= y1)
    for cx, cy in ((x0 + r, y0 + r), (x1 - r, y0 + r), (x0 + r, y1 - r), (x1 - r, y1 - r)):
        corner = ((X < x0 + r) if cx == x0 + r else (X > x1 - r)) & ((Y < y0 + r) if cy == y0 + r else (Y > y1 - r))
        m &= ~corner | ((X - cx) ** 2 + (Y - cy) ** 2 <= (r + 0.3) ** 2)
    return m

def disc(cx, cy, r):
    return (X - cx) ** 2 + (Y - cy) ** 2 <= (r + 0.3) ** 2

def paint(a, m, c):
    a[m] = c + (255,)

def base(body, body_d, rim, rim_d):
    a = np.zeros((H, W, 4), np.uint8)
    paint(a, rrect(0, 0, W - 1, H - 1, 5), INK)
    paint(a, rrect(1, 1, W - 2, H - 2, 4), rim)
    paint(a, rrect(1, 1, W - 2, H - 2, 4) & ~rrect(1, 1, W - 3, H - 3, 4), rim_d)       # 바깥 테 오른쪽 · 아래 그늘
    paint(a, rrect(4, 4, W - 5, H - 5, 3), INK)
    paint(a, rrect(5, 5, W - 6, H - 6, 2), body)
    paint(a, rrect(5, 5, W - 6, H - 6, 2) & ~rrect(5, 5, W - 7, H - 7, 2), body_d)
    return a

def inner():
    return rrect(6, 6, W - 7, H - 7, 2)

def gem(a, cx, cy, s):
    # 반짝돌(민트 결정): 위 밝은 면 · 아래 어두운 면, 외곽선 1칸
    d = np.abs(X - cx) / s + np.abs(Y - cy) / (s * 1.25)
    body = d <= 1
    ring = (d <= 1 + 1.3 / s) & ~body
    paint(a, ring, INK)
    paint(a, body, MINT)
    paint(a, body & (Y < cy) & (X < cx + 0.5), MINT_L)
    paint(a, body & (Y >= cy) & (X >= cx), MINT_D)

def card_back():
    CAR, CAR_D, CAR_L, CAR_DD = (216, 150, 96), (176, 112, 68), (240, 188, 132), (132, 80, 52)
    a = base(CAR, CAR_D, CREAM, CREAM_D)
    lattice = inner() & (((X + Y) % 8 == 0) | ((X - Y) % 8 == 0))
    paint(a, lattice, CAR_L)
    dots = inner() & ((X + Y) % 8 == 4) & ((X - Y) % 8 == 4)
    paint(a, dots, CAR_DD)
    cx, cy = W // 2, H // 2
    paint(a, disc(cx, cy, 17), INK); paint(a, disc(cx, cy, 16), CREAM); paint(a, disc(cx, cy, 16) & ~disc(cx - 1, cy - 1, 15), CREAM_D)
    gem(a, cx, cy, 9)
    return a


if __name__ == '__main__':
    Image.fromarray(card_back()).save(os.path.join(HERE, '..', 'card_back.png'))
    print('card_back', W, 'x', H)
