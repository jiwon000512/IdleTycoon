# -*- coding: utf-8 -*-
# 낚시터 넓히기 그림(2026-10-07 아트방, 설계 53): 구간 경계의 바위벽 rock_wall · 물속 잔해 댐 dam_3 · dam_2 · dam_1 · 마른 강바닥 타일 dry_bed · 좌대 seat · 끌려 나오는 잔해 조각 debris_log · debris_pot.
#   Codex 시안 셋(a · b · c), 사용자 선택 전에는 권장안을 등록한다(PICK). 바탕 둘을 make_decor.py 방식으로 만든다(한 칸 10px, 흰 바탕 + 둑 띠 A + 흙 면 + 평면 물 + 웜뱃 + 빨간 발끝 표시):
#     raw/wide_bank_template.png(170×120칸): 둑 위 표시 넷 = 바위벽 · 좌대 · 통나무 조각 · 냄비 조각(조각 둘은 둑에 놓인 모습으로 그리고 가운데 피벗으로 쓴다)
#     raw/wide_water_template.png(210×130칸): 물 위 표시 셋 = 댐 3단계(조각 많음 → 적음, 같은 바닥선)
#   떼기는 make_decor.extract와 같다(원본 격자 그대로 옮기고 바탕을 색 · 줄로 지운 뒤 둑 위 · 물 위 덩어리를 왼쪽부터 이름 붙인다).
#   dry_bed(32×32px, 물 타일과 같은 방식 · 이음새 없음 · 외곽선 없음)는 재질이라 칸 무늬로 그린다: 젖은 진흙 바탕 · 갈라진 금 · 조약돌 몇 · 물웅덩이 자국. 시안 a 금 많음 · b 조약돌 많음 · c 웅덩이 자국.
# 출력(한 칸 2px · PPU 80, 피벗 아래 가운데 · 조각 둘은 가운데): ../rock_wall.png · ../dam_3.png · ../dam_2.png · ../dam_1.png · ../seat.png · ../debris_log.png · ../debris_pot.png · ../dry_bed.png
# 사용: python make_wide.py template <폴더>   바탕 둘 + 프롬프트 여섯(<폴더>/prompt_<k>_<bank|water>.txt)
#       python make_wide.py props <폴더>      <폴더>/wide_<k>_<bank|water>.png에서 떼어 <폴더>/out_<k>/<이름>.png + dry_bed 세 시안 <폴더>/out_<k>/dry_bed.png
#       python make_wide.py                   고른 시안을 ../<이름>.png로
import os
import random
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import make_bank as mb  # noqa: E402
from make_decor import groups_of, crop  # noqa: E402
from make_dig import blobs  # noqa: E402
import snap_codex  # noqa: E402

SHEETS = {   # 이름: (칸 폭, 칸 높이, 둑 선 줄, 둑 위 표시 x, 물 위 표시 x, 물 표시 y)
    'bank': dict(cw=170, ch=150, bank_row=90, bank_x=(62, 106, 132, 156), water_x=(), water_y=0, cuts=(84, 86, 88),   # 바위벽 · 좌대가 붙어 그려져 83~89열을 비운다(바위벽은 비교용이라 오른쪽 가장자리가 잘려도 된다)   # 위를 넓게: 바위벽 64칸이 서야 Codex가 줄이지 않는다(1차 120칸 바탕에서 33×36으로 나옴)
                 names=['rock_wall', 'seat', 'debris_log', 'debris_pot']),
    'water': dict(cw=210, ch=130, bank_row=58, bank_x=(), water_x=(45, 110, 165), water_y=124,
                  names=['dam_3', 'dam_2', 'dam_1']),
    'rock': dict(cw=120, ch=150, bank_row=90, bank_x=(76,), water_x=(), water_y=0, names=['rock_wall'], guide=(48, 64), zone={'c': 45}),   # Codex가 웜뱃을 42칸(9~50열)으로 그려 바위와 닿는다 → 51열 왼쪽은 지운다. c는 바위 밑 흙 비탈이 48열부터라 45   # 바위벽만 다시: 크기 안내 빨간 네모(48×64칸). 2차 둑 시트의 바위벽은 웜뱃 키(45칸)에 그쳤다
}
PICK = {'rock_wall': 'a', 'seat': 'a', 'debris_log': 'a', 'debris_pot': 'a', 'dam_3': 'a', 'dam_2': 'a', 'dam_1': 'a', 'dry_bed': 'a'}

COMMON = ("Use your IMAGE GENERATION tool to paint on top of image 1 (do NOT write code, do NOT resample or copy anything). Keep the canvas size, the white background, "
          "the wombat, the riverbank strip, the dirt face and the flat teal water of image 1 EXACTLY where and how they are: one art pixel = 10 screen pixels, the wombat is "
          "52 art pixels wide. Remove the short red lines. Use the same big 10-pixel art pixels everywhere, never smaller pixels.\n"
          "This is a cozy underground wombat-burrow idle game. The wombat stands on a riverbank and fishes in the teal water below. ")
STYLE = ("\nSTYLE: chunky pixel art exactly like the wombat and the bank: no anti-aliasing, no gradients, no dithering, flat soft pastel palette with low saturation, "
         "dark brown outline (#342020) one art pixel wide around each object, one darker shade and a small white highlight. Orthographic three-quarter top-down view like "
         "the wombat: seen a little from above so tops show, never from below, vertical edges stay vertical. No ground shadow, no text, no letters, no numbers. "
         "Objects must not touch the wombat or each other.\nSave the image as wide_%s.png in the current working directory and reply with only the path.")
BANK = ("Paint FOUR objects standing on the bank, one at each red line, feet exactly on the red line, rising upward over the white area, nothing drawn below the line. From left to right:\n"
        "1) a tall pile of big boulders about 48 art pixels wide and 64 tall that blocks the way along the bank like a rockfall: %s. It leans against the burrow's dirt wall behind it, so the back is flat and the stones are stacked and wedged.\n"
        "2) a small wooden angler's seat about 32 art pixels wide and 20 tall: %s.\n"
        "3) a single charred, broken log about 24 art pixels wide and 14 tall, lying on the bank, black-brown with a few ember-orange cracks: %s.\n"
        "4) a dented old cooking pot about 20 art pixels wide and 18 tall, lying on the bank, soot-dark metal with a bent handle: %s.")
WATER = ("Paint THREE heaps of wildfire debris rising out of the water, one at each red line, the lowest pixel of each heap exactly on its red line, with a thin pale ripple ring where it meets the water. "
         "They are the SAME dam in three stages, same ground line: %s\n"
         "1) left: the full dam, about 64 art pixels wide and 48 tall: many pieces.\n"
         "2) middle: the half-cleared dam, about 48 art pixels wide and 32 tall: about half the pieces, the same style.\n"
         "3) right: the last remains, about 32 art pixels wide and 18 tall: two or three pieces.")
VARIANTS = {
    'a': dict(rock='five or six rounded grey boulders of different sizes stacked on each other, lighter grey tops',
              seat='a folding wooden stool with crossed legs and a small plank footboard in front of it',
              log='rounded log end showing growth rings', pot='round-bellied pot on its side',
              dam='charred black-brown logs leaning on each other, a dented pot, a broken wooden sign board and a grey rock wedged in between'),
    'b': dict(rock='angular dark-grey rock slabs and chunks with cracks, jammed together, some moss on top',
              seat='a low wooden bench of two planks on short legs with a small wooden box as a footrest',
              log='split log with a jagged broken end', pot='kettle-shaped pot with a crumpled lid',
              dam='mostly charred logs stacked crosswise with a tin kettle and a scorched bucket caught in them'),
    'c': dict(rock='big rounded boulders half buried in a brown dirt mound, with pebbles and a few grass tufts around the base',
              seat='a wooden stool with a round padded cushion on top and a small plank footboard',
              log='short thick stump-like log with bark', pot='wide shallow pan with a long handle',
              dam='broken fence planks and a burnt sign board tangled with two logs, a barrel and small rocks'),
}


def template(kind, path):
    s = SHEETS[kind]
    sc = mb.scene(s['cw'], s['ch'], s['bank_row'], mb.STRIPS['a'](), wombat=False, floor_on=False)
    a = np.asarray(sc).copy()
    F = mb.tex('Fishing/stream_face.png').shape[0]
    a[s['bank_row'] + F + 1:, :, :3] = mb.LIGHT
    sc = Image.fromarray(a, 'RGBA')
    wb = Image.open(os.path.join(mb.W, 'Shop', 'wombat_front.png')).convert('RGBA')
    wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
    sc.alpha_composite(wb, (6, s['bank_row'] - 10 - wb.height))
    big = sc.resize((s['cw'] * 10, s['ch'] * 10), Image.NEAREST).convert('RGB')
    d = ImageDraw.Draw(big)
    for fx in s['bank_x']:
        fy = s['bank_row'] - mb.SH + 3
        d.line((fx * 10 - 60, fy * 10, fx * 10 + 60, fy * 10), fill=(220, 40, 40), width=4)
        if 'guide' in s:
            gw, gh = s['guide']
            d.rectangle((fx * 10 - gw * 5, fy * 10 - gh * 10, fx * 10 + gw * 5, fy * 10), outline=(220, 40, 40), width=4)
    for fx in s['water_x']:
        d.line((fx * 10 - 60, s['water_y'] * 10, fx * 10 + 60, s['water_y'] * 10), fill=(220, 40, 40), width=4)
    big.save(path)


ROCK = ("Paint ONE object: a BIG pile of boulders standing on the bank at the red line, that blocks the way along the bank like a rockfall. SIZE IS THE POINT: the pile fills the red rectangle, "
        "which is 48 art pixels wide and 64 tall, so it is clearly TALLER than the wombat (the wombat is 47 tall) and about as wide as the wombat. Then remove the red rectangle and the red line. "
        "Design: %s. It leans against the burrow's dirt wall behind it, so the back is flat and the stones are stacked and wedged, the biggest at the bottom.")


def prompt(k, kind):
    v = VARIANTS[k]
    body = {'bank': BANK % (v['rock'], v['seat'], v['log'], v['pot']), 'water': WATER % v['dam'], 'rock': ROCK % v['rock']}[kind]
    return COMMON + body + STYLE % ('%s_%s' % (k, kind))


def extract(raw_path, bank_names, water_names, cuts=(), zone=None):
    """make_decor.extract와 같다: 원본 격자 그대로 칸으로 → 바탕(흰 · 물 · 둑 띠 줄) 지움 → 둑 위(웜뱃 뺌) · 물 위 덩어리를 왼쪽부터.
    cuts: 둑 위에서 비우는 열(붙어 그려진 두 물건을 가른다, 둑 시트의 바위벽 · 좌대 사이 84열). zone: 이 열 왼쪽은 웜뱃 자리로 지운다(바위벽 시트, 웜뱃과 바위가 닿는다)"""
    cells, _ = snap_codex.snap(raw_path, square=True, min_hole=40, raw=True)
    rgb = cells[..., :3].astype(int)
    water = (np.abs(rgb - mb.LIGHT).max(-1) <= 28) & (cells[..., 3] > 0)
    wtop = int(np.nonzero(water.mean(1) > 0.5)[0].min())
    sandish = (np.abs(rgb - mb.SAND).max(-1) <= 30) | (np.abs(rgb - mb.SAND_DARK).max(-1) <= 30)
    stop = int(np.nonzero(sandish[:wtop].mean(1) > 0.4)[0].min())
    keep = cells[..., 3] > 0
    keep[stop:wtop] = False
    keep[wtop:] &= ~water[wtop:]
    rows = np.arange(keep.shape[0])[:, None]
    red = (rgb[..., 0] > 170) & (rgb[..., 1] < 90) & (rgb[..., 2] < 90)                   # 남은 빨간 안내선
    keep &= ~red
    out = []
    if bank_names:
        above = keep & (rows < stop)
        for c in cuts:
            above[:, c - 1:c + 2] = False
        if zone:
            above[:, :zone] = False
        else:
            for y, x in max((g for g in blobs(above) if min(x for _, x in g) < 40), key=len):   # 웜뱃 = 왼쪽에 닿는 가장 큰 덩어리(바위벽이 더 클 수 있다)
                above[y, x] = False
        gs = groups_of(above, 12, big=120)[:len(bank_names)]                                   # 큰 덩어리끼리는 안 묶는다(바위벽 옆 좌대)
        assert len(gs) == len(bank_names), '둑 위 덩어리 %d' % len(gs)
        gs.sort(key=lambda g: np.mean([x for _, x in g]))
        out += [(n, crop(cells, g, stop - 1)) for n, g in zip(bank_names, gs)]
    if water_names:
        gs = groups_of(keep & (rows >= wtop))[:len(water_names)]
        assert len(gs) == len(water_names), '물 위 덩어리 %d' % len(gs)
        gs.sort(key=lambda g: np.mean([x for _, x in g]))
        out += [(n, crop(cells, g)) for n, g in zip(water_names, gs)]
    return out


# 마른 강바닥 타일: 물 타일(make_stream.water_tile)처럼 32×32 한 텍셀 = 한 칸, 이음새 없음(모든 좌표는 % 32)
MUD = (122, 92, 70)
MUD_DARK = (104, 76, 58)
MUD_WET = (90, 66, 52)
CRACK = (72, 50, 40)
PEBBLE = ((172, 160, 150), (148, 136, 128))


def dry_bed(k):
    S = 32
    rnd = random.Random({'a': 7, 'b': 11, 'c': 5}[k])
    a = np.zeros((S, S, 4), np.uint8)
    a[..., :3] = MUD; a[..., 3] = 255
    for _ in range(26):                                               # 젖은 얼룩(짙은 진흙 조각 2~3칸)
        y, x, w = rnd.randrange(S), rnd.randrange(S), rnd.choice((2, 3))
        for dx in range(w):
            a[y, (x + dx) % S, :3] = MUD_DARK
    cracks = {'a': 7, 'b': 3, 'c': 4}[k]
    for _ in range(cracks):                                           # 갈라진 금: 꺾이며 내려가는 한 칸 선
        y, x = rnd.randrange(S), rnd.randrange(S)
        for _ in range(rnd.randrange(6, 12)):
            a[y % S, x % S, :3] = CRACK
            y += 1; x += rnd.choice((-1, 0, 0, 1))
    pebbles = {'a': 4, 'b': 10, 'c': 5}[k]
    for _ in range(pebbles):                                          # 조약돌 2×1 + 아래 그늘
        y, x, c = rnd.randrange(S), rnd.randrange(S), rnd.choice(PEBBLE)
        a[y, x, :3] = c; a[y, (x + 1) % S, :3] = c
        a[(y + 1) % S, x, :3] = MUD_WET; a[(y + 1) % S, (x + 1) % S, :3] = MUD_WET
    if k == 'c':                                                      # 물웅덩이 자국: 젖은 타원 셋
        for _ in range(3):
            cy, cx, rw, rh = rnd.randrange(S), rnd.randrange(S), rnd.randrange(3, 6), rnd.randrange(2, 3)
            for y in range(-rh, rh + 1):
                for x in range(-rw, rw + 1):
                    if (x / rw) ** 2 + (y / rh) ** 2 <= 1:
                        a[(cy + y) % S, (cx + x) % S, :3] = MUD_WET
    return a


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pick'
    if cmd == 'template':
        out = sys.argv[2]; os.makedirs(out, exist_ok=True)
        for kind in SHEETS:
            template(kind, os.path.join(out, 'wide_%s_template.png' % kind))
            for k in VARIANTS:
                open(os.path.join(out, 'prompt_%s_%s.txt' % (k, kind)), 'w', encoding='utf-8').write(prompt(k, kind))
        print('templates + prompts')
    elif cmd == 'props':
        out = sys.argv[2]
        for k in VARIANTS:
            od = os.path.join(out, 'out_' + k); os.makedirs(od, exist_ok=True)
            Image.fromarray(dry_bed(k), 'RGBA').save(os.path.join(od, 'dry_bed.png'))
            for kind, s in SHEETS.items():
                raw = os.path.join(out, 'wide_%s_%s.png' % (k, kind))
                if not os.path.exists(raw):
                    continue
                try:
                    for name, ic in extract(raw, s['names'] if kind != 'water' else [], s['names'] if kind == 'water' else [], s.get('cuts', ()), s.get('zone', {}).get(k, 51) if 'zone' in s else None):
                        if kind == 'bank' and name == 'rock_wall':
                            name = 'rock_wall_bank'                                            # 둑 시트의 작은 바위벽은 비교용으로만
                        mb.save(ic, os.path.join(od, name + '.png'))
                        print('%s %s.png %dx%d칸' % (k, name, ic.shape[1], ic.shape[0]))
                except AssertionError as e:
                    print('%s %s 실패: %s' % (k, kind, e))
    else:
        done = set()
        Image.fromarray(dry_bed(PICK['dry_bed']), 'RGBA').save(os.path.join(HERE, '..', 'dry_bed.png')); done.add('dry_bed')
        for kind, s in SHEETS.items():
            for k in sorted({PICK[n] for n in s['names']}):
                for name, ic in extract(os.path.join(HERE, 'raw', 'wide_%s_%s.png' % (k, kind)), s['names'] if kind != 'water' else [], s['names'] if kind == 'water' else [], s.get('cuts', ()), s.get('zone', {}).get(k, 51) if 'zone' in s else None):
                    if kind == 'bank' and name == 'rock_wall':
                        continue                                  # 바위벽은 rock 시트에서
                    if PICK[name] == k:
                        mb.save(ic, os.path.join(HERE, '..', name + '.png'))
                        print('%s.png %dx%d칸 시안 %s' % (name, ic.shape[1], ic.shape[0], k))
                        done.add(name)
        assert done == set(PICK), '못 뗀 것: %s' % (set(PICK) - done)
