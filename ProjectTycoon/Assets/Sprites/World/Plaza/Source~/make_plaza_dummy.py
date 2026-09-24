# 설계 11 광장 더미 그림: 칸 단위로 그리고 2배(가게 한 칸 = 2px) 또는 그대로(UI 아이콘)로 저장.
# 실제 아트는 아트방이 같은 경로에 덮어쓴다(2026-09-24 사용자 확정: 장식 두 벌 A 소박한 흙·나무 / B 아기자기한 상가 거리, 고정 부품은 B 결)
# 사용: python make_plaza_dummy.py <Assets 폴더>
import os, sys
from PIL import Image, ImageDraw

OUT = sys.argv[1]
LINE = (52, 32, 32, 255)
CREAM = (244, 230, 207, 255)
RED = (201, 72, 59, 255)
BOARD = (150, 102, 62, 255)
BOARD_L = (190, 138, 86, 255)
STONE = (168, 148, 128, 255)
STONE_D = (126, 106, 90, 255)
TILE = (106, 170, 196, 255)
TILE_L = (214, 238, 244, 255)
WATER = (79, 147, 181, 255)
WATER_L = (160, 214, 236, 255)
WOOD = (176, 123, 69, 255)
WOOD_L = (201, 140, 80, 255)
IRON = (70, 64, 70, 255)
IRON_L = (110, 104, 112, 255)
LEAF = (79, 138, 60, 255)
LEAF_L = (122, 180, 90, 255)
FLOWER = (234, 120, 150, 255)
POT = (181, 101, 58, 255)
LAMP = (242, 193, 78, 255)
GLOBE = (255, 246, 214, 255)
DARK = (46, 28, 24, 255)
LIGHT = (255, 238, 178, 255)


def save(img, rel, scale):
    path = os.path.join(OUT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.resize((img.width * scale, img.height * scale), Image.NEAREST).save(path)


def canvas(w, h):
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


# 차양 68×12칸(B): 빨강·크림 줄무늬, 아래 반원 물결
img, d = canvas(68, 12)
n = 8
sw = 68 // n
for i in range(n):
    c = RED if i % 2 == 0 else CREAM
    x0 = i * sw + (68 - n * sw) // 2
    d.rectangle([x0, 0, x0 + sw - 1, 7], fill=c)
    d.ellipse([x0, 4, x0 + sw - 1, 11], fill=c)
    d.arc([x0, 4, x0 + sw - 1, 11], 0, 180, fill=LINE)
d.rectangle([0, 0, 67, 0], fill=LINE)
save(img, 'Sprites/World/Plaza/awning.png', 2)

# 간판 40×14칸(B): 둥근 나무판. 글자는 게임이 TMP로
img, d = canvas(40, 14)
d.rounded_rectangle([0, 0, 39, 13], radius=5, fill=LINE)
d.rounded_rectangle([1, 1, 38, 12], radius=4, fill=BOARD_L)
d.rounded_rectangle([2, 2, 37, 11], radius=3, fill=BOARD)
save(img, 'Sprites/World/Plaza/sign.png', 2)

# 지상 계단 48×40칸(B): 띠에 뚫린 입구 + 위에서 드는 빛 + 밝은 나무 계단. 밑변 = 띠 밑변
img, d = canvas(48, 40)
d.rounded_rectangle([0, 0, 47, 39], radius=10, fill=LINE)
d.rounded_rectangle([2, 2, 45, 39], radius=9, fill=DARK)
for y in range(3, 40):
    k = 1 - (y - 3) / 37
    col = tuple(int(DARK[i] + (LIGHT[i] - DARK[i]) * k * 0.8) for i in range(3)) + (255,)
    d.line([4, y, 43, y], fill=col)
for y in range(10, 40, 6):
    d.rectangle([4, y, 43, y + 1], fill=WOOD_L)
    d.line([4, y + 2, 43, y + 2], fill=LINE)
save(img, 'Sprites/World/Plaza/stairs.png', 2)


# 분수 76×52칸: 테 타원 + 물 + 가운데 기둥과 물줄기. rim·inner = 테 색
def fountain(name, rim, rim_d):
    img, d = canvas(76, 52)
    d.ellipse([0, 20, 75, 51], fill=LINE)
    d.ellipse([2, 21, 73, 49], fill=rim)
    d.ellipse([9, 25, 66, 44], fill=WATER)
    d.ellipse([19, 29, 56, 40], outline=WATER_L)
    d.rectangle([34, 6, 41, 34], fill=LINE)
    d.rectangle([35, 7, 40, 34], fill=rim_d)
    d.arc([10, 0, 48, 36], 200, 280, fill=WATER_L, width=2)
    d.arc([28, 0, 66, 36], 260, 340, fill=WATER_L, width=2)
    save(img, 'Resources/Sprites/Decor/' + name + '.png', 2)


fountain('fountain_stone', STONE, STONE_D)
fountain('fountain_tile', TILE, TILE_L)


# 벤치 64×30칸: 등받이 + 앉는 판 + 다리
def bench(name, back, seat, leg):
    img, d = canvas(64, 30)
    for x in (6, 54):
        d.rectangle([x, 4, x + 3, 29], fill=leg)
    d.rectangle([1, 2, 62, 10], fill=LINE)
    d.rectangle([2, 3, 61, 9], fill=back)
    d.rectangle([0, 16, 63, 24], fill=LINE)
    d.rectangle([1, 17, 62, 23], fill=seat)
    save(img, 'Resources/Sprites/Decor/' + name + '.png', 2)


bench('bench_log', WOOD, WOOD_L, LINE)
bench('bench_iron', IRON_L, WOOD_L, IRON)

# 화분 30×40칸: 잎 덩이 + 주황 화분(pot) / 꽃 상자(box)
img, d = canvas(30, 40)
for (cx, cy, r, c) in [(9, 14, 8, LEAF), (20, 13, 8, LEAF), (15, 7, 7, LEAF_L)]:
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c, outline=LINE)
d.polygon([(3, 22), (26, 22), (22, 39), (7, 39)], fill=POT, outline=LINE)
d.rectangle([2, 21, 27, 24], fill=POT, outline=LINE)
save(img, 'Resources/Sprites/Decor/plant_pot.png', 2)

img, d = canvas(30, 40)
for i, x in enumerate(range(4, 27, 5)):
    d.ellipse([x - 3, 12, x + 3, 24], fill=LEAF, outline=LINE)
    d.ellipse([x - 2, 10 + (i % 2) * 2, x + 2, 14 + (i % 2) * 2], fill=FLOWER, outline=LINE)
d.rectangle([0, 22, 29, 39], fill=LINE)
d.rectangle([1, 23, 28, 38], fill=WOOD_L)
d.line([1, 30, 28, 30], fill=WOOD)
save(img, 'Resources/Sprites/Decor/plant_box.png', 2)


# 가로등 12×56칸: 기둥 + 네모 등(lantern) / 둥근 등(globe)
def lamp(name, globe):
    img, d = canvas(12, 56)
    d.rectangle([5, 12, 6, 55], fill=LINE)
    d.rectangle([2, 52, 9, 55], fill=LINE)
    if globe:
        d.ellipse([0, 0, 11, 11], fill=LINE)
        d.ellipse([1, 1, 10, 10], fill=GLOBE)
    else:
        d.rectangle([1, 2, 10, 13], fill=LINE)
        d.rectangle([2, 3, 9, 12], fill=LAMP)
        d.rectangle([0, 0, 11, 2], fill=LINE)
    save(img, 'Resources/Sprites/Decor/' + name + '.png', 2)


lamp('lamp_lantern', False)
lamp('lamp_globe', True)

# 행동 아이콘 18×18(UI px): 아치 + 화살표(들어가기 = 위, 나가기 = 아래)
for name, up in (('enter', True), ('exit', False)):
    img, d = canvas(18, 18)
    d.pieslice([2, 1, 15, 14], 180, 360, fill=LINE)
    d.rectangle([2, 7, 15, 16], fill=LINE)
    d.pieslice([4, 3, 13, 12], 180, 360, fill=DARK)
    d.rectangle([4, 7, 13, 16], fill=DARK)
    if up:
        d.polygon([(9, 5), (5, 10), (13, 10)], fill=CREAM)
        d.rectangle([8, 10, 9, 14], fill=CREAM)
    else:
        d.polygon([(9, 15), (5, 10), (13, 10)], fill=CREAM)
        d.rectangle([8, 5, 9, 10], fill=CREAM)
    save(img, 'Resources/Sprites/Actions/' + name + '.png', 1)

print('ok')
