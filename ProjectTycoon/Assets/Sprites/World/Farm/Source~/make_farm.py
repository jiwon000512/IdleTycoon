# 설계 25 밀 농사: 밀 재료 아이콘(창고 칸 · 거두기 팝업 · 굽기 칩). 한 칸 2px · PPU 80, 피벗 가운데(BakeryBaker).
# 밭 흙판과 밀 단계 그림은 2026-09-30부터 Codex 시안을 칸 단위로 줄인 make_farm_art.py가 만든다(옛 더미 밭·밀 그림은 지웠다).
# 출력: Resources/Sprites/Items/wheat.png. 실행: Windows Python(Pillow) make_farm.py
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites')
PX = 2

OUT = (52, 32, 32, 255)        # 외곽선(art.md)
PLANK_SH = (112, 76, 48, 255)  # 끈
STALK = (196, 160, 84, 255)
GOLD = (232, 196, 104, 255)
GOLD_DK = (184, 140, 64, 255)


def save(cells, w, h, path):
    im = Image.new('RGBA', (w * PX, h * PX), (0, 0, 0, 0))
    px = im.load()
    for (x, y), c in cells.items():
        if 0 <= x < w and 0 <= y < h:
            for dy in range(PX):
                for dx in range(PX):
                    px[x * PX + dx, y * PX + dy] = c
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print(os.path.relpath(path, HERE))


def icon():
    # 밀 단 12×12칸: 이삭 셋을 끈으로 묶은 다발
    w, h = 12, 12
    c = {}
    for x, top in ((3, 1), (6, 0), (9, 1)):
        for i in range(4):
            y = top + i
            c[(x - 1, y)] = OUT
            c[(x + 1, y)] = OUT
            c[(x, y)] = GOLD if i % 2 else GOLD_DK
        c[(x, top - 1)] = OUT
    for y in range(5, 11):
        for x in range(4, 9):
            c[(x, y)] = STALK if x != 8 else GOLD_DK
        c[(3, y)] = OUT
        c[(9, y)] = OUT
    for x in range(3, 10):
        c[(x, 7)] = PLANK_SH
        c[(x, 11)] = OUT
    save(c, w, h, os.path.join(RES, 'Items', 'wheat.png'))


if __name__ == '__main__':
    icon()
