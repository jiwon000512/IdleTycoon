# 메뉴 버튼 아이콘 둘(2026-10-02 사용자 선택). 18×18px(UI 1px = 화면 4px, 버튼 안 72×72), 도형 + 바깥 1칸 외곽선(창고 · 유물 아이콘과 같은 결).
#   icon_edit  편집 모드 버튼 B 「네 방향 화살표」(옮기기). 시안 A 망치 · C 연필과 자는 버림. 그동안은 파기 행동 아이콘(삽)을 같이 써서 파기 버튼과 같았다
#   icon_clerk 점원 버튼 B 「이름표」(집게 · 주황 띠 · 얼굴 동그라미 · 글 줄). 시안 A 앞치마 · C 웜뱃 얼굴은 버림. 그동안은 회색 점원 캐릭터 그림을 줄여 썼다
# 출력 ../icon_edit.png · ../icon_clerk.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_menu_icons.py → 메뉴 Import UI Sprites
import json
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
INK = (0x34, 0x20, 0x20)
CREAM, TAN = (0xFB, 0xF4, 0xE6), (0xD9, 0xC8, 0xB4)
ORANGE, ORANGE_D = (0xD8, 0x78, 0x48), (0xA8, 0x54, 0x30)
STEEL = (0xD8, 0xD2, 0xCC)
GREY, GREY_D = (0xA4, 0x9C, 0x98), (0x7C, 0x74, 0x72)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def edit():  # 네 방향 화살표: 두 칸 굵기 팔 + 머리, 아래 · 오른쪽 반은 그늘
    rows = [
        ".......OO.......",
        "......OOOO......",
        ".....OOOOOO.....",
        ".......OO.......",
        ".......OO.......",
        "..O....OO....O..",
        ".OO....OO....OO.",
        "OOOOOOOOOOOOOOOO",
        "oooooooOOooooooo",
        ".oo....OO....oo.",
        "..o....OO....o..",
        ".......oo.......",
        ".......oo.......",
        ".....oooooo.....",
        "......oooo......",
        ".......oo.......",
    ]
    a = np.zeros((18, 18, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                a[y + 1, x + 1] = (ORANGE if ch == 'O' else ORANGE_D) + (255,)
    return outline(a)


def clerk():  # 이름표: 집게 · 카드 · 주황 띠 · 얼굴 동그라미 · 글 줄 셋
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.rectangle([7, 1, 10, 3], fill=STEEL + (255,))
    d.rectangle([2, 4, 15, 15], fill=CREAM + (255,))
    d.rectangle([2, 4, 15, 6], fill=ORANGE + (255,))
    d.ellipse([3, 8, 8, 13], fill=GREY + (255,))
    d.point((5, 10), fill=GREY_D + (255,)); d.point((6, 10), fill=GREY_D + (255,))
    for x1, y in ((14, 9), (13, 11), (14, 13)):
        d.line([(10, y), (x1, y)], fill=TAN + (255,))
    d.rectangle([2, 15, 15, 15], fill=TAN + (255,))
    return outline(np.asarray(im).copy())


path = os.path.join(OUT, 'ui_slices.json')
slices = json.load(open(path, encoding='utf-8'))
for name, draw in (('icon_edit', edit), ('icon_clerk', clerk)):
    Image.fromarray(draw()).save(os.path.join(OUT, name + '.png'))
    slices[name] = {'w': 18, 'h': 18, 'border': None}
json.dump(slices, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('icon_edit · icon_clerk 18x18')
