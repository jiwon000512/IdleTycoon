# -*- coding: utf-8 -*-
# 상호작용 버튼 + 행동 아이콘 실제 아트(시안 B 흙 테 + 크림 판, 2026-09-23 사용자 선택). AI 없이 칸 단위로 그린다.
# 1 px = 1 UI px(PPU 25). 버튼 44: 조이스틱 받침과 같은 흙 테 + 크림 판. 아이콘 18: 버튼 행동(ActionTable.json manual)만 — 열기·파기·들어가기·나가기(꺼내기는 2026-09-23 auto로 바뀌어 아이콘 삭제).
# 비활성은 코드 틴트(버튼 × 0.65, 아이콘 40%)라 따로 그리지 않는다. 칠한 뒤 바깥 1칸 진갈색 외곽선.
# 2026-10-01 사용자: 상호작용 버튼 btn_act는 B 「돌 테」, 오른쪽 메뉴 버튼 · 팝업은 새 조각 btn_menu C 「볼록 단추」(옛 흙 테 + 크림 판은 버림).
# 2026-10-09 사용자(설계 55 메뉴 줄 104px): btn_menu는 26칸 「둥근 네모」 시안 C로 다시 그림(44칸 볼록 단추를 줄여 쓰면 흐렸다).
# 사용: make_act_button.py (이 폴더에서) → ../btn_act.png · ../btn_menu.png, Resources/Sprites/Actions/<id>.png → 메뉴 ZooTycoon/Bake/Import UI Sprites → Bake/UI
import os
import random
import numpy as np
from PIL import Image, ImageDraw

os.chdir(os.path.dirname(os.path.abspath(__file__)))
INK = (0x34, 0x20, 0x20)
CREAM, LIGHT, TAN = (0xFB, 0xF4, 0xE6), (0xF0, 0xE4, 0xD8), (0xD9, 0xC8, 0xB4)
RIM, RIM_L = (0xC8, 0xAA, 0x96), (0xE2, 0xCC, 0xB8)
WOOD, WOOD_D = (0xC9, 0x9A, 0x6B), (0xA0, 0x72, 0x4C)
STEEL, STEEL_D = (0xD8, 0xD2, 0xCC), (0xA8, 0xA0, 0x98)

def disc(n, r, dx=0, dy=0):
    y, x = np.mgrid[0:n, 0:n] - (n - 1) / 2
    return (x - dx) ** 2 + (y - dy) ** 2 <= (r + 0.3) ** 2


def paint(a, m, c):
    a[m] = c + (255,)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


Y, X = np.mgrid[0:44, 0:44] - 21.5
STONE = [(88, 80, 78), (122, 114, 108), (156, 148, 138), (190, 182, 170), (222, 216, 204)]   # 석상 · 축복 메달과 같은 돌색
CARAMEL, CARAMEL_D, CARAMEL_DD, CARAMEL_L = (0xD8, 0x96, 0x60), (0xB0, 0x70, 0x44), (0x84, 0x50, 0x34), (0xF0, 0xBC, 0x84)


def face(a, r, dx=0, dy=0):
    # 크림 판: 아래 · 오른쪽 그늘 한 톤, 위 · 왼쪽 안쪽 밝은 초승달
    paint(a, disc(44, r, dx, dy), LIGHT)
    paint(a, disc(44, r, dx, dy) & ~disc(44, r, dx - 1, dy - 1), TAN)
    paint(a, disc(44, r, dx, dy) & ~disc(44, r, dx + 1.2, dy + 1.2) & (X - dx + Y - dy < -r * 0.6), CREAM)


def button():
    # 상호작용 버튼(2026-10-01 사용자 B 「돌 테」): 석상 돌색 테 + 점무늬 + 크림 판
    a = np.zeros((44, 44, 4), np.uint8)
    paint(a, disc(44, 20.5), STONE[2])
    paint(a, disc(44, 20.5) & ~disc(44, 20.5, 1, 1), STONE[3])
    paint(a, disc(44, 20.5) & ~disc(44, 20.5, 1.6, 1.6) & (X + Y < -20), STONE[4])
    paint(a, disc(44, 20.5) & ~disc(44, 20.5, -1, -1), STONE[1])
    paint(a, disc(44, 16.5) & ~disc(44, 15.5), STONE[1])
    rnd = random.Random(7)
    band = np.argwhere(disc(44, 20) & ~disc(44, 17))
    for _ in range(30):
        j, i = band[rnd.randrange(len(band))]
        a[j, i, :3] = STONE[1]
    face(a, 15)
    return outline(a)


def menu_button():
    # 메뉴 버튼(오른쪽 메뉴 줄 · 편집, 2026-10-09 사용자 시안 C 「둥근 네모」): 26칸 · 한 칸 4px = 104px.
    #   모서리 6칸 둥근 네모 캐러멜 테 + 아래 두께(진한 캐러멜) + 왼쪽 위 밝은 · 오른쪽 아래 어두운 테두리 + 크림 판 20×19칸(아이콘 18칸 = 72px).
    #   판은 가운데보다 반 칸 위(아래 두께만큼). 버린 시안: A 볼록 단추 작은 판 · B 납작 단추
    n = 26
    y, x = np.mgrid[0:n, 0:n] - (n - 1) / 2

    def rbox(hw, hh, r, dx=0, dy=0):
        # 가운데에서 반폭 hw · 반높이 hh, 모서리 반지름 r인 둥근 네모
        qx = np.clip(np.abs(x - dx) - (hw - r), 0, None)
        qy = np.clip(np.abs(y - dy) - (hh - r), 0, None)
        return qx ** 2 + qy ** 2 <= (r + 0.3) ** 2

    a = np.zeros((n, n, 4), np.uint8)
    top = lambda dx, dy: rbox(11.9, 11.4, 6, dx, -0.5 + dy)
    pan = lambda dx, dy: rbox(9.9, 9.4, 4.5, dx, -0.5 + dy)
    paint(a, rbox(11.9, 11.9, 6, 0, 0.5) & (y > -12.5), CARAMEL_DD)
    paint(a, top(0, 0), CARAMEL)
    paint(a, top(0, 0) & ~top(1, 1), CARAMEL_L)
    paint(a, top(0, 0) & ~top(-1, -1), CARAMEL_D)
    paint(a, pan(0, 0), LIGHT)
    paint(a, pan(0, 0) & ~pan(-1, -1), TAN)
    paint(a, pan(0, 0) & ~pan(1, 1) & (x + y < -6), CREAM)
    return outline(a)


def open_():  # 돋보기
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.line([(10, 10), (15, 15)], fill=WOOD_D + (255,), width=3)
    a = np.asarray(im).copy()
    paint(a, disc(18, 5.5, -2, -2), WOOD_D)
    paint(a, disc(18, 3.8, -2, -2), LIGHT)
    paint(a, disc(18, 1.2, -4, -4), CREAM)
    return outline(a)


def dig():  # 삽
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.line([(8, 9), (14, 3)], fill=WOOD + (255,), width=2)
    d.line([(13, 4), (15, 2)], fill=WOOD_D + (255,), width=2)    # 손잡이 끝
    d.polygon([(2, 11), (6, 7), (10, 11), (6, 15), (3, 15), (2, 14)], fill=STEEL + (255,))
    d.polygon([(3, 15), (6, 15), (10, 11), (9, 10)], fill=STEEL_D + (255,))
    return outline(np.asarray(im).copy())


HOLE, ORANGE, ORANGE_L = (0x48, 0x2C, 0x2C), (0xD8, 0x78, 0x48), (0xF0, 0xA0, 0x70)


def arch(a, x0):  # 흙 테 아치 문: 폭 8칸, 윗부분 반원, 밑은 16줄. 안은 폭 4칸 어두운 구멍
    y, x = np.mgrid[0:18, 0:18]
    cx = x0 + 3.5
    frame = (x >= x0) & (x < x0 + 8) & (y < 16) & ((y >= 6) | ((x - cx) ** 2 + (y - 6) ** 2 <= 4.3 ** 2))
    hole = (x >= x0 + 2) & (x < x0 + 6) & (y < 16) & ((y >= 7) | ((x - cx) ** 2 + (y - 7) ** 2 <= 2.4 ** 2))
    paint(a, frame, RIM)
    paint(a, hole, HOLE)


def arrow(a, x0, x1, y):  # 오른쪽 화살표: 몸통 3칸 두께, 머리 7칸
    a[y - 1:y + 2, x0:x1 - 3] = ORANGE + (255,)
    for i in range(4):
        a[y - 3 + i:y + 4 - i, x1 - 4 + i] = ORANGE + (255,)
    a[y - 1, x0:x1 - 3] = ORANGE_L + (255,)


def enter():  # 들어가기: 오른쪽 아치로 들어가는 화살표
    a = np.zeros((18, 18, 4), np.uint8)
    arch(a, 9)
    arrow(a, 1, 13, 11)
    return outline(a)


def exit_():  # 나가기: 왼쪽 아치에서 나오는 화살표
    a = np.zeros((18, 18, 4), np.uint8)
    arch(a, 1)
    arrow(a, 5, 17, 11)
    return outline(a)


# 2026-09-30 사용자 선택(시안 페이지): 심기 B 새싹 + 흙 봉우리, 깨우기 B 종, 치우기 A 빗자루. 옛 그림은 아이콘 자체에 둥근 테 · 세로 선이 있어 둥근 버튼 안에서 이상했다
LEAF, LEAF_D = (0x78, 0x84, 0x24), (0x54, 0x60, 0x24)
GOLD, GOLD_D, GOLD_L = (0xE8, 0xC4, 0x68), (0xB8, 0x8C, 0x40), (0xF4, 0xE0, 0xA0)
BRUSH, BRUSH_D = (0xE2, 0xC8, 0x8C), (0xB8, 0x98, 0x5C)


def plant():  # 심기: 새싹 + 흙 봉우리
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.pieslice([2, 11, 15, 22], 180, 360, fill=WOOD_D + (255,))
    d.rectangle([8, 7, 9, 13], fill=LEAF_D + (255,))
    d.polygon([(8, 8), (3, 5), (3, 2), (7, 3)], fill=LEAF + (255,))
    d.polygon([(9, 8), (14, 5), (14, 2), (10, 3)], fill=LEAF + (255,))
    return outline(np.asarray(im).copy())


def wake():  # 깨우기: 종
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.pieslice([4, 3, 13, 12], 180, 360, fill=GOLD + (255,))
    d.rectangle([4, 7, 13, 12], fill=GOLD + (255,))
    d.rectangle([3, 12, 14, 13], fill=GOLD_D + (255,))
    d.rectangle([8, 14, 9, 15], fill=GOLD_D + (255,))
    d.rectangle([8, 1, 9, 2], fill=GOLD_D + (255,))
    d.rectangle([5, 5, 6, 9], fill=GOLD_L + (255,))
    return outline(np.asarray(im).copy())


def clean():  # 치우기: 빗자루(삽과 같은 기울기)
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.line([(9, 8), (15, 2)], fill=WOOD + (255,), width=2)
    d.line([(14, 3), (16, 1)], fill=WOOD_D + (255,), width=2)
    d.polygon([(2, 12), (7, 6), (11, 10), (9, 16), (3, 16)], fill=BRUSH + (255,))
    d.polygon([(3, 16), (9, 16), (11, 10), (10, 12)], fill=BRUSH_D + (255,))
    d.rectangle([7, 8, 8, 9], fill=WOOD_D + (255,))
    return outline(np.asarray(im).copy())


def till():  # 갈기(2026-10-02 사용자 선택 B 쇠스랑, 시안 A 괭이 · C 흙 이랑은 버림): 두 칸 굵기 나무 자루 + 가로대 + 아래로 난 발 넷
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    for i in range(8):
        d.point((2 + i, 15 - i), fill=WOOD + (255,))
        d.point((2 + i, 16 - i), fill=WOOD_D + (255,))
    d.line([(8, 6), (15, 3)], fill=STEEL_D + (255,))
    d.line([(8, 5), (15, 2)], fill=STEEL + (255,))
    for x, y in ((9, 6), (11, 5), (13, 4), (15, 3)):
        d.line([(x, y), (x + 1, y + 6)], fill=STEEL + (255,))
    return outline(np.asarray(im).copy())


ORANGE, ORANGE_D, ORANGE_L = (0xD8, 0x78, 0x48), (0xB0, 0x58, 0x34), (0xF0, 0xA0, 0x70)   # 주 버튼 주황(크림 판 위에서 또렷하게)


def talk():  # 대화(너구리에게 말 걸어 뽑기, 2026-10-01 사용자 A): 주황 말풍선 + 크림 점 셋
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([1, 2, 16, 12], radius=3, fill=ORANGE + (255,))
    d.polygon([(4, 12), (8, 12), (3, 16)], fill=ORANGE + (255,))
    d.line([(3, 3), (13, 3)], fill=ORANGE_L + (255,))
    d.line([(2, 12), (15, 12)], fill=ORANGE_D + (255,))
    for x in (5, 8, 11):
        d.rectangle([x, 6, x + 1, 7], fill=CREAM + (255,))
    return outline(np.asarray(im).copy())


CHECK = (0x3E, 0x7A, 0x4C)   # UI 「가능」 초록


def evaluate():  # 평가(설계 40 평가판, 2026-10-02 사용자 선택 A 평가표 클립보드, 시안 B 금별 · C 나팔은 버림): 나무 판 + 쇠 집게 + 종이 · 체크 셋
    im = Image.new('RGBA', (18, 18)); d = ImageDraw.Draw(im)
    d.rectangle([3, 2, 14, 16], fill=WOOD + (255,))
    d.rectangle([14, 3, 14, 16], fill=WOOD_D + (255,)); d.rectangle([3, 16, 14, 16], fill=WOOD_D + (255,))
    d.rectangle([5, 4, 12, 14], fill=CREAM + (255,))
    d.rectangle([6, 1, 11, 3], fill=STEEL + (255,)); d.rectangle([6, 3, 11, 3], fill=STEEL_D + (255,))
    for y in (6, 9, 12):
        d.point((6, y), fill=CHECK + (255,)); d.point((7, y + 1), fill=CHECK + (255,)); d.point((8, y), fill=CHECK + (255,))
        d.line([(9, y + 1), (11, y + 1)], fill=TAN + (255,))
    return outline(np.asarray(im).copy())


Image.fromarray(button()).save('../btn_act.png')
Image.fromarray(menu_button()).save('../btn_menu.png')
# enter·exit는 2026-09-26 굴 이동 자동으로 아이콘이 없다(그리는 함수만 남김)
for action_id, draw in [('open', open_), ('dig', dig), ('till', till), ('plant', plant), ('wake', wake), ('clean', clean), ('talk', talk), ('evaluate', evaluate)]:
    Image.fromarray(draw()).save(f'../../../Resources/Sprites/Actions/{action_id}.png')
