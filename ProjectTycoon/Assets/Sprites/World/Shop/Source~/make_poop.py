# 설계 24 웜뱃 똥 더미: 앞쪽 위에서 내려다본 네모 똥 한 덩이(윗면 3줄 · 앞면 4줄, 10×9칸) + 위로 오르는 냄새 김 2프레임.
# 한 칸 2px · PPU 80, 피벗 아래 가운데(BakeryBaker). 출력 ../poop_0.png · ../poop_1.png(김만 다르다). 실제 그림은 시안 셋에서 고른다.
# 실행: Windows Python(Pillow) make_poop.py
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
PX = 2
PAL = {
    'o': (52, 32, 32, 255),     # 외곽선(art.md)
    'h': (178, 142, 104, 255),  # 윗면 하이라이트 한 획
    't': (140, 104, 74, 255),   # 윗면
    'f': (108, 76, 52, 255),    # 앞면
    's': (88, 60, 42, 255),     # 앞면 오른쪽 그늘
    'g': (150, 164, 112, 255),  # 냄새 김
}
CUBE = [
    "..oooooooo..",
    ".ohhtttttto.",
    ".ohttttttto.",
    ".otttttttto.",
    ".offffffsso.",
    ".offffffsso.",
    ".offffffsso.",
    ".offffffsso.",
    "..oooooooo..",
]
STINK = [
    [
        "...g....g...",
        "..g....g....",
        "...g....g...",
        "..g....g....",
        "............",
    ],
    [
        "..g....g....",
        "...g....g...",
        "..g....g....",
        "...g....g...",
        "............",
    ],
]


def draw(rows):
    im = Image.new('RGBA', (len(rows[0]) * PX, len(rows) * PX), (0, 0, 0, 0))
    px = im.load()
    for y, line in enumerate(rows):
        for x, ch in enumerate(line):
            if ch in PAL:
                for dy in range(PX):
                    for dx in range(PX):
                        px[x * PX + dx, y * PX + dy] = PAL[ch]
    return im


if __name__ == '__main__':
    for i, stink in enumerate(STINK):
        im = draw(stink + CUBE)
        im.save(os.path.join(OUT, 'poop_%d.png' % i))
        print('poop_%d' % i, im.size)
