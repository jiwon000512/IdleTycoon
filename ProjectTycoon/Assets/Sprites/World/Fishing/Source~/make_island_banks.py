# -*- coding: utf-8 -*-
# 섬마다 다른 둑 띠(2026-10-14 아트방, 설계 61 지하 호수의 섬들): 갈대섬 · 바위섬 · 유목섬의 뭍 아랫변에 까는 띠 셋. 지금은 bank.png(A 조약돌 둑) 하나를 모든 섬에 깐다.
#   띠는 make_bank.py와 같은 40×24칸(아래 넉 줄 = 둑 고리 선 · 턱 · 턱 · 선, 그 위는 바닥이 비치는 투명 바탕), 피벗 아래 가운데, 뭍 폭만큼 Tiled.
#   시안은 「세트」 셋(세트 하나 = 섬 셋의 띠):
#     A 있는 띠 셋: 갈대섬 = B 풀 · 갈대 둑, 바위섬 = A 조약돌 둑(지금 것), 유목섬 = C 나무 말뚝 둑(make_bank.STRIPS, 2026-10-07 사용자가 본 시안)
#     B 섬 이름대로 새 띠: 갈대섬 = 젖은 진흙 + 갈대 · 풀 포기, 바위섬 = 회색 자갈 + 큰 돌, 유목섬 = 바랜 모래 + 유목 조각 · 조개
#     C 조약돌 둑 + 섬 표시: 조약돌 둑(지금 것)에 갈대섬은 풀 포기, 바위섬은 자갈빛 + 큰 돌, 유목섬은 바랜 모래빛 + 유목 하나
#   등록 이름은 무늬별 bankstrip_<무늬>.png(리드 2026-10-14): BakeryBaker DecorFiles가 bankstrip_을 소품에서 빼 FishingView.m_bankStrips에 넣고, FishingIslandTable 섬 행 bank 칸(그림 이름, null = bank.png)으로 섬마다 깐다.
# 출력(한 칸 2px · PPU 80): ../bankstrip_<무늬>.png (PICK에 든 무늬만, 조약돌은 bank.png 그대로)
# 사용: python make_island_banks.py <폴더>   세 세트를 <폴더>/set_<a|b|c>/bank_<섬>.png(80×48)로
#       python make_island_banks.py          PICK(섬 → 무늬)의 띠를 ../bankstrip_<무늬>.png로 쓰고 섬 행 bank 칸 값을 찍는다
import os
import random
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_bank as mb  # noqa: E402

ISLANDS = ('reed', 'rock', 'drift')
# 등록 이름(2026-10-14 리드): 띠는 무늬 이름 bankstrip_<무늬>.png, FishingIslandTable 섬 행 bank 칸에 그 이름(null = 지금 bank.png 조약돌). 사용자가 섬마다 고른다(PICK, 조약돌 pebble은 파일 없이 null)
STRIP_NAMES = {}      # 아래 SETS 뒤에서 채운다: 무늬 이름 → 그리는 함수
PICK = {'reed': 'grass', 'rock': 'pebble', 'drift': 'wood'}
SW, SH, put, LINE = mb.SW, mb.SH, mb.put, mb.LINE
BAND = SH - 10            # 띠 윗줄(조약돌 둑의 모래 띠와 같다)

GRAVEL = (150, 146, 140)
GRAVEL_DARK = (132, 128, 122)
GRAVEL_EDGE = (118, 114, 108)
ROCK = [(200, 192, 184), (172, 164, 156), (136, 124, 116)]      # 윗면 · 몸 · 그늘(조약돌과 같은 계열)
MUD = (112, 84, 64)
MUD_DARK = (96, 70, 54)
MUD_EDGE = (84, 60, 46)
PALE = (218, 202, 174)
PALE_DARK = (200, 182, 154)
PALE_EDGE = (180, 160, 132)
DRIFT = [(196, 172, 142), (165, 139, 119), (132, 108, 92)]       # 유목 윗면 · 몸 · 그늘(bank_driftwood 계열, 윗면은 바랜 모래 띠와 구분되게 한 톤 아래)
SHELL = (238, 230, 218)
SHELL_PINK = (222, 178, 170)
MOSS = (120, 136, 64)


def band(s, top, light, dark, edge, rnd, specks=0.08):
    """턱 위 여섯 줄짜리 띠(윗줄은 가장자리 색)"""
    for y in range(top, SH - 4):
        for x in range(SW):
            put(s, y, x, light if rnd.random() > specks else dark)
    for x in range(SW):
        put(s, top, x, edge)


def blob(s, y, x, w, h, shades):
    """모서리를 깎은 w×h 덩어리(y = 아랫줄): 네 모서리 칸은 외곽선, 둘레 선은 모서리를 비워 둥글게. shades = (윗줄, 몸, 아랫줄)"""
    for dy in range(h):
        for dx in range(w):
            corner = dy in (0, h - 1) and dx in (0, w - 1)
            c = LINE if corner else shades[0] if dy == h - 1 else shades[2] if dy == 0 else shades[1]
            put(s, y - dy, x + dx, c)
    for dx in range(1, w - 1):
        put(s, y + 1, x + dx, LINE); put(s, y - h, x + dx, LINE)
    for dy in range(1, h - 1):
        put(s, y - dy, x - 1, LINE); put(s, y - dy, x + w, LINE)


def stone(s, y, x, w, h):
    """w×h 큰 돌: 둥근 덩어리 + 윗면 밝음 한 칸"""
    blob(s, y, x, w, h, ROCK)
    put(s, y - h + 1, x + 1, (224, 218, 210))


def driftwood(s, y, x, w):
    """누운 유목 w×3(끝이 둥글다) + 위로 삐친 가지 하나(y = 아랫줄)"""
    blob(s, y, x, w, 3, DRIFT)
    bx = x + w - 2                                   # 끝에 짧은 가지 그루터기(길면 꼬리처럼 보여 물고기로 읽힌다)
    put(s, y - 3, bx, DRIFT[1]); put(s, y - 4, bx, LINE)
    put(s, y - 3, bx - 1, LINE); put(s, y - 3, bx + 1, LINE)
    for dx in range(2, w - 2):                       # 몸통 가운데 나뭇결 한 줄 + 옹이
        put(s, y - 1, x + dx, DRIFT[2] if dx == w // 2 else DRIFT[1])
    put(s, y - 2, x + 1, DRIFT[0])


def shell(s, y, x):
    put(s, y, x, SHELL); put(s, y, x + 1, SHELL); put(s, y - 1, x, SHELL_PINK); put(s, y - 1, x + 1, SHELL)
    put(s, y + 1, x, LINE); put(s, y + 1, x + 1, LINE); put(s, y, x - 1, LINE); put(s, y - 1, x + 2, LINE); put(s, y - 2, x, LINE); put(s, y - 2, x + 1, LINE); put(s, y - 1, x - 1, LINE); put(s, y, x + 2, LINE)


def strip_reedmud():
    """B 갈대섬: 젖은 진흙 띠 + 띠 윗선에 풀 포기 · 갈대 여럿"""
    s = mb.base()
    rnd = random.Random(21)
    band(s, BAND + 1, MUD, MUD_DARK, MUD_EDGE, rnd, 0.12)
    for _ in range(9):
        mb.tuft(s, rnd.randrange(BAND - 1, BAND + 2), rnd.randrange(SW), rnd.random() < 0.5)
    for x in (3, 7, 14, 23, 29, 35):
        mb.reed(s, BAND + 2, x, rnd.randrange(7, 12))
    for _ in range(4):
        mb.tuft(s, rnd.randrange(4, BAND - 4), rnd.randrange(SW), False)
    return s


def strip_gravel():
    """B 바위섬: 회색 자갈 띠 + 큰 돌 둘 + 잔돌"""
    s = mb.base()
    rnd = random.Random(33)
    band(s, BAND, GRAVEL, GRAVEL_DARK, GRAVEL_EDGE, rnd, 0.2)
    for _ in range(12):
        mb.pebble(s, rnd.randrange(BAND + 1, SH - 5), rnd.randrange(SW), rnd.choice((2, 3)), rnd.randrange(3))
    stone(s, BAND + 2, 6, 5, 3)
    stone(s, BAND - 2, 26, 4, 3)
    for _ in range(6):
        mb.pebble(s, rnd.randrange(3, BAND - 5), rnd.randrange(SW), rnd.choice((2, 3)), rnd.randrange(3))
    return s


def strip_drift():
    """B 유목섬: 바랜 모래 띠 + 유목 둘 + 조개 둘 + 잔돌"""
    s = mb.base()
    rnd = random.Random(44)
    band(s, BAND, PALE, PALE_DARK, PALE_EDGE, rnd, 0.06)
    driftwood(s, BAND + 3, 3, 9)
    driftwood(s, BAND - 3, 24, 7)
    shell(s, BAND + 2, 17)
    shell(s, BAND - 4, 36)
    for _ in range(6):
        mb.pebble(s, rnd.randrange(BAND + 1, SH - 5), rnd.randrange(SW), 2, rnd.randrange(3))
    return s


def recolor(s, pairs):
    out = s.copy()
    for src, dst in pairs:
        m = (out[..., 0] == src[0]) & (out[..., 1] == src[1]) & (out[..., 2] == src[2]) & (out[..., 3] > 0)
        out[m, :3] = dst
    return out


def strip_pebble_reed():
    """C 갈대섬: 조약돌 둑 + 풀 포기 · 갈대 둘"""
    s = mb.strip_pebble()
    rnd = random.Random(7)
    for _ in range(6):
        mb.tuft(s, rnd.randrange(BAND - 1, BAND + 1), rnd.randrange(SW), rnd.random() < 0.5)
    mb.reed(s, BAND + 1, 9, 9); mb.reed(s, BAND + 1, 30, 8)
    return s


def strip_pebble_rock():
    """C 바위섬: 조약돌 둑을 자갈빛으로 + 큰 돌 하나"""
    s = recolor(mb.strip_pebble(), [(mb.SAND, GRAVEL), (mb.SAND_DARK, GRAVEL_DARK)])
    stone(s, BAND + 2, 22, 5, 3)
    return s


def strip_pebble_drift():
    """C 유목섬: 조약돌 둑을 바랜 모래빛으로 + 유목 하나"""
    s = recolor(mb.strip_pebble(), [(mb.SAND, PALE), (mb.SAND_DARK, PALE_DARK)])
    driftwood(s, BAND + 3, 8, 9)
    return s


SETS = {
    'a': {'reed': mb.strip_grass, 'rock': mb.strip_pebble, 'drift': mb.strip_wood},
    'b': {'reed': strip_reedmud, 'rock': strip_gravel, 'drift': strip_drift},
    'c': {'reed': strip_pebble_reed, 'rock': strip_pebble_rock, 'drift': strip_pebble_drift},
}
STRIP_NAMES.update({'pebble': mb.strip_pebble, 'grass': mb.strip_grass, 'wood': mb.strip_wood, 'reedmud': strip_reedmud, 'gravel': strip_gravel, 'drift': strip_drift,
                    'pebble_reed': strip_pebble_reed, 'pebble_rock': strip_pebble_rock, 'pebble_drift': strip_pebble_drift})


def check(s):
    assert s.shape == (SH, SW, 4)
    for y in (SH - 1, SH - 4):                          # 둑 고리 선은 그대로
        assert all(tuple(s[y, x, :3]) == LINE for x in range(SW)), y


if __name__ == '__main__':
    if len(sys.argv) > 1:
        for k, strips in SETS.items():
            out = os.path.join(sys.argv[1], 'set_' + k)
            os.makedirs(out, exist_ok=True)
            for island, fn in strips.items():
                s = fn(); check(s)
                mb.save(s, os.path.join(out, 'bank_%s.png' % island))
        print(sys.argv[1])
    else:
        for name in sorted(set(PICK.values()) - {'pebble'}):      # 조약돌은 bank.png 그대로(bank 칸 null)
            s = STRIP_NAMES[name](); check(s)
            mb.save(s, os.path.join(HERE, '..', 'bankstrip_%s.png' % name))
            print('bankstrip_%s.png' % name)
        print('bank 칸:', ', '.join('%s = %s' % (island, 'null' if name == 'pebble' else 'bankstrip_' + name) for island, name in PICK.items()))
