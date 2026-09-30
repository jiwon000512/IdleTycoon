# 설계 26 창고 버튼 아이콘. 18×18px(UI 1px = 화면 4px, 버튼 안 72×72). 2026-09-30 사용자 선택(시안 B): 끈으로 묶은 천 자루(옛 나무 상자 + 밀 이삭은 농사 버튼처럼 보였다).
# 출력 ../icon_storage.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_icon_storage.py
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (0x34, 0x20, 0x20)
LINEN, LINEN_D, TIE = (0xE6, 0xCC, 0xA0), (0xC4, 0xA0, 0x6E), (0x84, 0x5C, 0x3A)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


im = Image.new('RGBA', (18, 18))
d = ImageDraw.Draw(im)
d.ellipse([3, 7, 14, 16], fill=LINEN + (255,))
d.ellipse([5, 11, 14, 16], fill=LINEN_D + (255,))
d.polygon([(6, 8), (11, 8), (12, 4), (5, 4)], fill=LINEN + (255,))
d.rectangle([5, 7, 12, 7], fill=TIE + (255,))
d.rectangle([6, 3, 11, 3], fill=LINEN_D + (255,))
Image.fromarray(outline(np.asarray(im).copy())).save(os.path.join(HERE, '..', 'icon_storage.png'))
print('icon_storage')
