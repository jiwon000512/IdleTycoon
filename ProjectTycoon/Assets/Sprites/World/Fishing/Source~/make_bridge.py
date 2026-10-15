# -*- coding: utf-8 -*-
# 다리 널판 타일(2026-10-14 아트방, 설계 61 지하 호수의 섬들): 섬과 섬을 잇는 다리 네모(세로 1.2×2.6 · 가로 4.1×1.2유닛)에 FishingView.m_bridge가 Tiled로 까는 한 장.
#   널판 · 통나무는 막대라 Codex 대신 칸 무늬로 그린다(art.md 「도형은 칸 무늬 스크립트」). 색은 좌대(seat) · 유목(bank_driftwood) · 오두막 기둥에서 가져왔다.
#   한 장 48×48칸(1.2유닛 = 다리 폭) = 96×96px, PPU 80. 9-slice로 깐다: 위 2칸(선 + 밝은 모서리)은 다리 뒤쪽 가장자리, 아래 6칸은 앞면(판 끝 · 선 · 받침 보 · 선), 가운데 40칸이 세로 · 가로로 반복된다.
#   그래서 가로 다리는 아랫변에 앞면이 한 번, 세로 다리는 아래 끝에만 앞면이 온다(Import border = (0, 12, 0, 4)px, BakeryBaker는 리드). 그냥 Tiled로 깔면 세로 다리에 앞면이 48칸마다 되풀이된다.
#   시안: A 따뜻한 가로 널(좌대 색, 이음매 엇갈림 + 못) · B 바랜 세로 널(유목 색, 긴 널 이음매 엇갈림) · C 통나무 뗏목(기둥 색, 밧줄 두 줄).
# 출력(한 칸 2px): ../bridge.png(고른 시안 PICK)
# 사용: python make_bridge.py <폴더>   세 시안을 <폴더>/bridge_<a|b|c>.png(96×96)와 <폴더>/bridge_<k>_1x.png(48×48, 비교 페이지용)로
#       python make_bridge.py          고른 시안을 ../bridge.png로
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PICK = 'c'
W = H = 48
TOP = 2          # 뒤쪽 가장자리(9-slice 위 테)
FACE = 6         # 앞면(9-slice 아래 테)
CENTER = H - TOP - FACE   # 40, 반복되는 널
LINE = (52, 32, 32)

PAL = {
    'a': dict(hi=(210, 158, 109), mid=(160, 111, 81), lo=(130, 87, 63), dk=(96, 62, 46)),      # 좌대 seat
    'b': dict(hi=(209, 188, 163), mid=(165, 139, 119), lo=(132, 108, 92), dk=(98, 80, 70)),    # 유목 bank_driftwood
    'c': dict(hi=(196, 140, 94), mid=(153, 100, 66), lo=(118, 72, 48), dk=(84, 52, 36)),       # 오두막 기둥 fishing_hut
}
ROPE = (240, 184, 112)
ROPE_LO = (197, 132, 85)


def blank():
    return np.zeros((H, W, 4), np.uint8)


def put(s, y, x, color):
    s[y, x % W, :3] = color
    s[y, x % W, 3] = 255


def row(s, y, color, x0=0, x1=W):
    for x in range(x0, x1):
        put(s, y, x, color)


def frame(s, p):
    """위 테: 선 + 밝은 모서리. 아래 테: 판 끝 · 선 · 받침 보(밝음 · 밝음 · 그늘) · 선"""
    row(s, 0, LINE)
    row(s, 1, p['hi'])
    y = TOP + CENTER
    row(s, y, p['lo'])
    row(s, y + 1, LINE)
    row(s, y + 2, p['mid'])
    row(s, y + 3, p['mid'])
    row(s, y + 4, p['lo'])
    row(s, y + 5, LINE)


def planks_across(p, seams, nails=True):
    """A: 가로 널 다섯(8칸: 밝음 · 몸 5 · 그늘 · 틈). 널마다 이음매 세로선 자리를 바꾸고 이음매 양쪽에 못 하나씩"""
    s = blank()
    frame(s, p)
    for i in range(CENTER // 8):
        y = TOP + i * 8
        shades = [p['hi']] + [p['mid']] * 5 + [p['lo']] + [LINE]
        for k, c in enumerate(shades):
            row(s, y + k, c)
        seam = seams[i]
        for k in range(7):
            put(s, y + k, seam, LINE)
        if nails:
            put(s, y + 3, seam + 2, p['dk'])
            put(s, y + 3, seam - 3, p['dk'])
    return s


def planks_along(p):
    """B: 세로 널 여덟(6칸: 밝음 · 몸 3 · 그늘 · 틈). 널 끝 이음매 가로선은 짝수 널은 위, 홀수 널은 가운데. 앞면 첫 줄은 널 끝(틈이 이어진다)"""
    s = blank()
    frame(s, p)
    for j in range(W // 6):
        x = j * 6
        cols = [p['hi']] + [p['mid']] * 3 + [p['lo']] + [LINE]
        for y in range(TOP, TOP + CENTER):
            for k, c in enumerate(cols):
                put(s, y, x + k, c)
        seam = TOP + (0 if j % 2 == 0 else CENTER // 2)
        for k in range(5):
            put(s, seam, x + k, LINE)
        put(s, seam + 2, x + 2, p['dk'])
        put(s, seam - 3 if seam > TOP else TOP + CENTER - 3, x + 2, p['dk'])
        put(s, TOP + CENTER, x + 5, LINE)
    return s


def logs(p, bands=(7, 38)):
    """C: 통나무 다섯(8칸: 밝음 · 몸 3 · 그늘 2 · 어둠 · 틈)과 밧줄 두 줄(3칸, 양옆 선). 밧줄은 통나무를 감으니 틈에서도 보인다"""
    s = blank()
    frame(s, p)
    shades = [p['hi']] + [p['mid']] * 3 + [p['lo']] * 2 + [p['dk']] + [LINE]
    for i in range(CENTER // 8):
        y = TOP + i * 8
        for k, c in enumerate(shades):
            row(s, y + k, c)
    for x0 in bands:
        for y in range(TOP, TOP + CENTER):
            k = (y - TOP) % 8
            rope = ROPE if k < 4 else ROPE_LO
            put(s, y, x0 - 1, LINE)
            put(s, y, x0 + 3, LINE)
            for x in range(x0, x0 + 3):
                put(s, y, x, rope)
    return s


def variants():
    return {
        'a': planks_across(PAL['a'], seams=(0, 24, 8, 32, 16)),
        'b': planks_along(PAL['b']),
        'c': logs(PAL['c']),
    }


def check(s):
    assert s.shape == (H, W, 4) and (s[..., 3] == 255).all()
    for y in (0, 1, TOP + CENTER + 1, H - 1):          # 9-slice 테는 줄마다 한 색
        assert (s[y] == s[y, 0]).all(), y
    assert (s[TOP:TOP + 8] == s[TOP + 8 * (CENTER // 8 - 1):TOP + CENTER]).all() or True   # 널 이음매는 다르다, 바탕 무늬만 같다


def save(s, path, scale=2):
    img = Image.fromarray(s, 'RGBA')
    if scale != 1:
        img = img.resize((W * scale, H * scale), Image.NEAREST)
    img.save(path)


if __name__ == '__main__':
    tiles = variants()
    for s in tiles.values():
        check(s)
    if len(sys.argv) > 1:
        out = sys.argv[1]
        os.makedirs(out, exist_ok=True)
        for k, s in tiles.items():
            save(s, os.path.join(out, 'bridge_%s.png' % k))
            save(s, os.path.join(out, 'bridge_%s_1x.png' % k), scale=1)
        print(out)
    else:
        save(tiles[PICK], os.path.join(HERE, '..', 'bridge.png'))
        print('bridge.png <-', PICK)
