# 설계 25 밀 농사 → 설계 27 칸 밭 더미(시안 비교 전, 에이전트 판단): 갈아 놓은 흙판(칸 크기 135×96칸 = 3.375×2.4유닛, 바닥 재질이라 외곽선 없음) + 밀 3단계 + 밀 아이콘.
# 한 칸 2px · PPU 80. 흙판은 피벗 아래 가운데(BakeryBaker)라 칸 밑변에 놓이고, 밀 포기 그림(64×40칸, 세 두둑)은 흙판 위 네 자리(BakeryBaker k_CropOffsets)에 얹힌다.
# 출력: ../plot.png(Sprites/World/Farm) · Resources/Sprites/Farm/wheat_0~2.png · Resources/Sprites/Items/wheat.png
# 실행: Windows Python(Pillow) make_farm.py
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
FARM = os.path.join(HERE, '..')
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites')
PX = 2

OUT = (52, 32, 32, 255)        # 외곽선(art.md)
PLANK_SH = (112, 76, 48, 255)  # 아이콘 끈
SOIL = (96, 64, 44, 255)       # 흙
RIDGE = (120, 84, 58, 255)     # 두둑
FURROW = (74, 48, 34, 255)     # 고랑
GREEN = (122, 168, 88, 255)
GREEN_DK = (86, 128, 64, 255)
STALK = (196, 160, 84, 255)
GOLD = (232, 196, 104, 255)
GOLD_DK = (184, 140, 64, 255)

# 흙판(칸 크기)
PLOT_W, PLOT_H = 135, 96
# 밀 포기 그림: 폭·밑변 기준 두둑 세 줄(그림 위에서 몇 칸째)
W, H = 64, 24
RIDGES = [4, 8, 12]
CROP_H = 40


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


def plot():
    # 갈아 놓은 흙판: 칸 전체를 채우는 흙, 가로 두둑(2줄) + 고랑(1줄)이 15칸마다, 가장자리 한 칸은 고랑빛
    c = {}
    for y in range(PLOT_H):
        for x in range(PLOT_W):
            edge = x in (0, PLOT_W - 1) or y in (0, PLOT_H - 1)
            c[(x, y)] = FURROW if edge else SOIL
    for r in range(6, PLOT_H - 6, 15):
        for x in range(3, PLOT_W - 3):
            c[(x, r)] = RIDGE
            c[(x, r + 1)] = RIDGE
            c[(x, r + 2)] = FURROW
    save(c, PLOT_W, PLOT_H, os.path.join(FARM, 'plot.png'))


def plant_columns():
    return range(7, W - 6, 7)


def put(c, x, y, color):
    if (x, y) not in c or c[(x, y)] != OUT:
        c[(x, y)] = color


def sprout(c, x, base):
    # 새싹 두 잎(3칸)
    put(c, x, base, GREEN_DK)
    put(c, x, base - 1, GREEN)
    put(c, x - 1, base - 1, GREEN)
    put(c, x + 1, base - 2, GREEN)
    put(c, x - 1, base - 2, GREEN_DK)


def young(c, x, base):
    # 푸른 줄기 8칸 + 잎 둘
    for i in range(8):
        put(c, x, base - i, GREEN if i % 3 else GREEN_DK)
    for i, (dx, dy) in enumerate([(-1, 3), (-2, 4), (1, 5), (2, 6), (-1, 7)]):
        put(c, x + dx, base - dy, GREEN if i % 2 else GREEN_DK)


def ripe(c, x, base):
    # 금빛 줄기 9칸 + 이삭 5칸(외곽선) + 까락
    for i in range(9):
        put(c, x, base - i, STALK)
    top = base - 9
    for i in range(5):
        y = top - i
        c[(x - 1, y)] = OUT
        c[(x + 1, y)] = OUT
        c[(x, y)] = GOLD if i % 2 else GOLD_DK
    c[(x, top - 5)] = OUT
    put(c, x - 2, top - 5, GOLD_DK)
    put(c, x + 2, top - 6, GOLD_DK)


def crop(stage, name):
    c = {}
    # 뒤 두둑부터 그려 앞 줄이 앞에 온다. 밑변은 옛 밭 그림 밑변과 같다
    for r in RIDGES:
        base = CROP_H - H + r + 1
        for x in plant_columns():
            shift = 3 if (r // 4) % 2 else 0
            [sprout, young, ripe][stage](c, x + shift, base)
    save(c, W, CROP_H, os.path.join(RES, 'Farm', name))


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
    plot()
    for stage in range(3):
        crop(stage, 'wheat_%d.png' % stage)
    icon()
