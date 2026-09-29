# 설계 22 이모지 말풍선 더미 시트: wait_sheet(52×36, 점 셋 3프레임)의 빈 틀 위에 글리프를 칸(2px) 단위로 찍는다.
# 칸 순서 = BubbleTable frame: 0~2 점(그대로) · 3 ♪ · 4 ? · 5 ! · 6 ♥ · 7 !! · 8 💬(작은 말풍선 둘) · 9 🤢(설계 24: 똥 + 냄새 김). 실제 아트는 아트방(같은 틀·같은 칸 순서).
# 깨우기 · 치우기(설계 24) · 심기(설계 25) 버튼 아이콘(Resources/Sprites/Actions/wake.png · clean.png · plant.png, 18×18)도 같은 팔레트로 더미를 찍는다.
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..')
ACTIONS = os.path.join(SHOP, '..', '..', '..', 'Resources', 'Sprites', 'Actions')
CELL = 52
INK = (52, 32, 32, 255)
CREAM = (240, 228, 216, 255)
DOT = (92, 76, 66, 255)
RED = (166, 75, 60, 255)
PINK = (222, 96, 108, 255)
POOP = (108, 76, 52, 255)
STINK = (132, 150, 96, 255)
STRAW = (214, 170, 96, 255)
LEAF = (86, 128, 64, 255)
SOIL = (108, 76, 52, 255)

GLYPHS = {
    'note': [
        "....##..",
        "....#.#.",
        "....#..#",
        "....#..#",
        "....#...",
        "..###...",
        ".####...",
        "..##....",
    ],
    'question': [
        ".####...",
        "#....#..",
        ".....#..",
        "....#...",
        "...#....",
        "...#....",
        "........",
        "...#....",
    ],
    'alert': [
        "..##....",
        "..##....",
        "..##....",
        "..##....",
        "..##....",
        "........",
        "..##....",
        "..##....",
    ],
    'heart': [
        ".##..##.",
        "########",
        "########",
        "########",
        ".######.",
        "..####..",
        "...##...",
        "........",
    ],
    'angry': [
        ".##..##.",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        ".##..##.",
        "........",
        ".##..##.",
        ".##..##.",
    ],
    'chat': [
        "####....",
        "#..#....",
        "####....",
        ".#......",
        "....####",
        "....#..#",
        "....####",
        "......#.",
    ],
    # 설계 24: 똥(#) 위에 냄새 김(+)
    'yuck': [
        ".+..+...",
        "+..+....",
        ".+..+...",
        "..####..",
        ".######.",
        ".######.",
        ".######.",
        "........",
    ],
}
COLORS = {'note': DOT, 'question': DOT, 'alert': RED, 'heart': PINK, 'angry': RED, 'chat': DOT, 'yuck': POOP}
# 두 번째 색(+)
COLORS2 = {'yuck': STINK}
ORDER = ['note', 'question', 'alert', 'heart', 'angry', 'chat', 'yuck']


def blank_frame(sheet):
    frame = sheet.crop((0, 0, CELL, sheet.height)).copy()
    px = frame.load()
    for y in range(frame.height):
        for x in range(frame.width):
            if px[x, y][:3] == DOT[:3]:
                px[x, y] = CREAM
    return frame


def stamp(frame, glyph, color, color2=None):
    px = frame.load()
    rows = len(glyph)
    cols = len(glyph[0])
    # 글리프 1칸 = 2px, 말풍선 안쪽(꼬리 위) 가운데
    ox = (CELL - cols * 2) // 2
    oy = (26 - rows * 2) // 2 + 2
    for r, line in enumerate(glyph):
        for c, ch in enumerate(line):
            if ch in '#+':
                for dy in range(2):
                    for dx in range(2):
                        px[ox + c * 2 + dx, oy + r * 2 + dy] = color if ch == '#' else color2


def make_sheet():
    wait = Image.open(os.path.join(SHOP, 'wait_sheet.png')).convert('RGBA')
    frames = [wait.crop((i * CELL, 0, (i + 1) * CELL, wait.height)) for i in range(3)]
    for name in ORDER:
        frame = blank_frame(wait)
        stamp(frame, GLYPHS[name], COLORS[name], COLORS2.get(name))
        frames.append(frame)
    sheet = Image.new('RGBA', (CELL * len(frames), wait.height), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * CELL, 0))
    sheet.save(os.path.join(SHOP, 'bubble_sheet.png'))
    print('bubble_sheet', sheet.size)


def make_wake_icon():
    # 18×18: 둥근 크림 바탕 + 붉은 「!」 (open.png와 같은 팔레트)
    im = Image.new('RGBA', (18, 18), (0, 0, 0, 0))
    px = im.load()
    for y in range(18):
        for x in range(18):
            dx, dy = x - 8.5, y - 8.5
            d = dx * dx + dy * dy
            if d <= 64:
                px[x, y] = CREAM if d <= 49 else INK
    for y in range(4, 11):
        for x in range(8, 10):
            px[x, y] = RED
    for y in range(12, 14):
        for x in range(8, 10):
            px[x, y] = RED
    os.makedirs(ACTIONS, exist_ok=True)
    im.save(os.path.join(ACTIONS, 'wake.png'))
    print('wake icon')


def make_clean_icon():
    # 설계 24 치우기: 둥근 크림 바탕 + 세운 빗자루(진갈색 자루 · 묶음 띠, 아래로 벌어지는 짚색 솔). 기운 선은 「금지」로 읽혀 세웠다
    im = Image.new('RGBA', (18, 18), (0, 0, 0, 0))
    px = im.load()
    for y in range(18):
        for x in range(18):
            dx, dy = x - 8.5, y - 8.5
            d = dx * dx + dy * dy
            if d <= 64:
                px[x, y] = CREAM if d <= 49 else INK
    for y in range(3, 10):
        for x in range(8, 10):
            px[x, y] = INK
    for x in range(7, 11):
        px[x, 10] = INK
    for y in range(11, 15):
        spread = (y - 11) // 2
        for x in range(7 - spread, 11 + spread):
            px[x, y] = STRAW
    os.makedirs(ACTIONS, exist_ok=True)
    im.save(os.path.join(ACTIONS, 'clean.png'))
    print('clean icon')


def make_plant_icon():
    # 설계 25 심기: 둥근 크림 바탕 + 흙 둔덕 위 두 잎 새싹
    im = Image.new('RGBA', (18, 18), (0, 0, 0, 0))
    px = im.load()
    for y in range(18):
        for x in range(18):
            dx, dy = x - 8.5, y - 8.5
            d = dx * dx + dy * dy
            if d <= 64:
                px[x, y] = CREAM if d <= 49 else INK
    for y in range(12, 15):
        half = 2 + (y - 12)
        for x in range(9 - half, 9 + half):
            px[x, y] = SOIL
    for y in range(7, 12):
        px[8, y] = LEAF
        px[9, y] = LEAF
    for x, y in ((6, 6), (7, 6), (5, 5), (6, 5), (7, 7), (10, 5), (11, 5), (11, 4), (12, 4), (10, 6)):
        px[x, y] = LEAF
    os.makedirs(ACTIONS, exist_ok=True)
    im.save(os.path.join(ACTIONS, 'plant.png'))
    print('plant icon')


if __name__ == '__main__':
    make_sheet()
    make_wake_icon()
    make_clean_icon()
    make_plant_icon()
