# -*- coding: utf-8 -*-
# 설계 32 유물 버튼 아이콘(HUD 오른쪽 버튼 줄 네 번째). 18×18px(UI 1px = 화면 4px, 버튼 안 72×72). 2026-10-01 사용자 선택(시안 C 「천 덮은 수레」):
#   행상이 끌고 오는 접힌 수레(크림 천 · 끈 두 줄 · 민트 결정 + 나무 바퀴 둘 · 손잡이). 시안 A 유리 진열장 · B 행운 동전은 버림
#   (종은 「깨우기」, 보따리는 창고 아이콘과 겹쳐 피함). 창고 아이콘처럼 도형 + 바깥 1칸 외곽선
# 출력 ../icon_relic.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_icon_relic.py
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (0x34, 0x20, 0x20)
CLOTH, CLOTH_D, ROPE = (232, 220, 196), (200, 184, 156), (140, 96, 60)
WOOD, WOOD_D, IRON, MINT = (164, 110, 72), (120, 78, 52), (96, 92, 92), (96, 196, 164)


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
d.rounded_rectangle([1, 3, 13, 11], radius=2, fill=c(CLOTH)); d.rectangle([1, 9, 13, 11], fill=c(CLOTH_D))
d.line([(4, 3), (4, 11)], fill=c(ROPE)); d.line([(10, 3), (10, 11)], fill=c(ROPE))
d.point([(7, 6), (6, 7), (7, 7), (8, 7)], fill=c(MINT))
d.rectangle([1, 12, 13, 12], fill=c(WOOD_D)); d.line([(14, 11), (16, 11)], fill=c(WOOD))
d.ellipse([2, 12, 6, 16], fill=c(WOOD)); d.point([(4, 14)], fill=c(IRON))
d.ellipse([9, 12, 13, 16], fill=c(WOOD)); d.point([(11, 14)], fill=c(IRON))
Image.fromarray(outline(np.asarray(im).copy())).save(os.path.join(HERE, '..', 'icon_relic.png'))
print('icon_relic')
