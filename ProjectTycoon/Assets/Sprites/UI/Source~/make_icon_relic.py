# -*- coding: utf-8 -*-
# 유물 메뉴 버튼 아이콘(HUD 오른쪽 버튼 줄 네 번째). 18×18px(UI 1px = 화면 4px, 버튼 안 72×72).
# 2026-10-01 사용자 선택(시안 B 「옛 항아리」): 수레를 버려서 다시. 흙빛 몸통 + 손잡이 둘 + 금 띠 + 민트 결정.
#   시안 A 보물 상자(창고와 헷갈림) · C 부적(시계 · 나침반으로 보임)은 버림. 앞서 버린 진열장 · 동전, 쓰는 중인 종(깨우기) · 보따리(창고)는 피한다.
#   창고 아이콘처럼 도형 + 바깥 1칸 외곽선
# 출력 ../icon_relic.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_icon_relic.py
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (0x34, 0x20, 0x20)
CLAY, CLAY_D, CLAY_L = (204, 122, 82), (158, 86, 60), (232, 166, 122)
GOLD, GOLD_D, GOLD_L = (0xE8, 0xC4, 0x68), (0xB8, 0x8C, 0x40), (0xF4, 0xE0, 0xA0)
MINT, MINT_D, MINT_L = (96, 196, 164), (64, 150, 128), (190, 240, 220)
WHITE = (255, 255, 255)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def c(col):
    return col + (255,)


im = Image.new('RGBA', (18, 18))
d = ImageDraw.Draw(im)
d.rectangle([5, 1, 12, 2], fill=c(CLAY_D)); d.rectangle([6, 3, 11, 4], fill=c(CLAY))          # 아가리 · 목
d.ellipse([2, 4, 15, 17], fill=c(CLAY))                                                       # 몸통
d.ellipse([2, 4, 15, 17], outline=c(WHITE))                                                   # 테두리 빛(오른쪽 · 아래는 그늘이 덮는다)
d.arc([1, 4, 5, 9], 90, 270, fill=c(CLAY_D)); d.arc([12, 4, 16, 9], 270, 90, fill=c(CLAY_D))  # 손잡이
d.rectangle([3, 9, 14, 11], fill=c(GOLD)); d.line([(3, 11), (14, 11)], fill=c(GOLD_D)); d.line([(4, 9), (6, 9)], fill=c(GOLD_L))
d.line([(4, 6), (4, 8)], fill=c(CLAY_L)); d.point([(5, 5)], fill=c(CLAY_L)); d.line([(4, 12), (4, 13)], fill=c(CLAY_L))
d.arc([2, 4, 15, 17], 300, 80, fill=c(CLAY_D)); d.line([(6, 16), (11, 16)], fill=c(CLAY_D))  # 오른쪽 · 아래 그늘
d.point([(8, 9)], fill=c(MINT_L)); d.point([(7, 10), (8, 10), (9, 10)], fill=c(MINT)); d.point([(8, 11)], fill=c(MINT_D))
Image.fromarray(outline(np.asarray(im).copy())).save(os.path.join(HERE, '..', 'icon_relic.png'))
print('icon_relic')
