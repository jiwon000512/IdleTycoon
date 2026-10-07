# 강가 둑(2026-10-07 아트방, 낚시를 「방 아래쪽을 가로지르는 강에 직접 던지는 손맛 낚시」로 바꾸며): 둑 선(y −8.6)을 따라 가로로 반복하는 둑 띠 + 물가 소품.
#   물결 타일 · 둑 선 아래 흙 면(stream_face 14줄) · 거품 줄은 코드가 그린다(물길 때처럼). 이 스크립트는 둑 띠(40×24칸, 피벗 아래 가운데, 아래변 = 둑 선)를 칸 무늬로 그리고,
#   소품은 Codex가 둑 띠 바탕(raw/bank_<a|b|c>_template.png) 위에 그린 것을 바탕과 다른 칸만 떼어 옮긴다(발끝 피벗).
#   띠 아래 넉 줄은 물길 둑 고리와 같다(선 LINE · 턱 LIP 둘 · 선). 그 위는 바닥이 비치는 투명 바탕에 결마다 다른 장식.
#   시안 셋: A 조약돌 둑(모래 띠 + 조약돌) · B 풀 · 갈대 둑(풀 포기 + 갈대) · C 나무 말뚝 둑(가로 널 + 말뚝 + 밧줄). 사용자 선택 A 「조약돌 둑」(B · C는 raw/bank_<b|c>_props.png에 기록). 프롬프트 raw/prompt_bank.txt.
# 출력(한 칸 2px · PPU 80): ../bank.png(띠 80×48px, 피벗 아래 가운데) · ../bank_<소품>.png(발끝 피벗)
# 사용: python make_bank.py strips <폴더>     세 띠 + 소품 바탕(Codex용)
#       python make_bank.py props <폴더>      raw/bank_<k>_props.png에서 소품을 떼어 <폴더>/out_<k>/bank_<이름>.png
#       python make_bank.py mock <폴더>       1080×1920 모형(바닥 + 띠 + 흙 면 + 물 + 소품 + 웜뱃)
#       python make_bank.py                   고른 시안을 ../bank.png · ../bank_<소품>.png로
import math
import os
import random
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
A = r'C:\project\Tycoon\ProjectTycoon\Assets'
W = os.path.join(A, 'Sprites', 'World')
PICK = 'a'
SW, SH = 40, 24
LINE = (52, 32, 32)
LIP = (168, 120, 96)
LIP_DARK = (150, 104, 82)
SAND = (196, 160, 124)
SAND_DARK = (176, 140, 108)
LIGHT = (110, 182, 170)
STONE = [(200, 192, 184), (180, 168, 160), (164, 150, 142)]
STONE_LO = (136, 124, 116)
GRASS = [(160, 176, 88), (120, 136, 64), (80, 96, 48)]          # 딸기 잎 색(밝음 · 중간 · 어두움)
REED = (176, 144, 96)
REED_HEAD = (104, 72, 48)
WOOD = (160, 96, 64)
WOOD_HI = (200, 136, 88)
WOOD_LO = (104, 56, 40)
ROPE = (240, 184, 112)
PROPS = {'a': ['rock', 'pebbles', 'driftwood'], 'b': ['reeds', 'bush', 'stump'], 'c': ['post', 'barrel', 'crate']}
PROP_LABEL = {'rock': '큰 바위', 'pebbles': '돌무더기', 'driftwood': '유목', 'reeds': '갈대', 'bush': '덤불', 'stump': '그루터기', 'post': '밧줄 말뚝', 'barrel': '나무 통', 'crate': '나무 상자'}


def tex(name):
    return np.asarray(Image.open(os.path.join(W, name)).convert('RGBA'))


def put(s, y, x, color):
    if 0 <= y < SH:
        s[y, x % SW, :3] = color
        s[y, x % SW, 3] = 255


def base():
    """둑 고리 넉 줄: 선 · 턱 · 턱 · 선(아래부터)"""
    s = np.zeros((SH, SW, 4), np.uint8)
    for x in range(SW):
        put(s, SH - 1, x, LINE); put(s, SH - 2, x, LIP); put(s, SH - 3, x, LIP); put(s, SH - 4, x, LINE)
    return s


def pebble(s, y, x, w, shade):
    """w×2 조약돌: 윗줄 돌 색, 아랫줄 그늘"""
    for dx in range(w):
        put(s, y, x + dx, STONE[shade]); put(s, y + 1, x + dx, STONE_LO)


def strip_pebble():
    """A 조약돌 둑: 턱 위 여섯 줄은 모래 띠(촘촘한 조약돌), 그 위로 띄엄띄엄 조약돌"""
    s = base()
    rnd = random.Random(11)
    for y in range(SH - 10, SH - 4):
        for x in range(SW):
            put(s, y, x, SAND if rnd.random() > 0.08 else SAND_DARK)
    for x in range(SW):
        put(s, SH - 10, x, SAND_DARK)
    for _ in range(14):
        pebble(s, rnd.randrange(SH - 10, SH - 5), rnd.randrange(SW), rnd.choice((2, 2, 3)), rnd.randrange(3))
    for _ in range(9):
        pebble(s, rnd.randrange(2, SH - 11), rnd.randrange(SW), rnd.choice((2, 3)), rnd.randrange(3))
    return s


def tuft(s, y, x, big):
    """풀 포기: 아래 어두운 밑동, 위로 밝은 잎 끝(큰 것은 다섯 잎)"""
    if big:
        for dx, h in ((-2, 1), (-1, 2), (0, 3), (1, 2), (2, 1)):
            for k in range(h):
                put(s, y - k, x + dx, GRASS[0] if k == h - 1 else GRASS[1])
        put(s, y, x - 1, GRASS[2]); put(s, y, x + 1, GRASS[2]); put(s, y, x, GRASS[2])
    else:
        for dx, h in ((-1, 1), (0, 2), (1, 1)):
            for k in range(h):
                put(s, y - k, x + dx, GRASS[0] if k == h - 1 else GRASS[1])
        put(s, y, x, GRASS[2])


def reed(s, y, x, h):
    for k in range(h):
        put(s, y - k, x, REED)
    put(s, y - h, x, REED_HEAD); put(s, y - h - 1, x, REED_HEAD); put(s, y - h - 2, x, REED_HEAD)
    put(s, y - h - 3, x, REED)


def strip_grass():
    """B 풀 · 갈대 둑: 턱은 젖은 색 한 줄, 그 위로 풀 포기(턱 가까이 촘촘) · 갈대 몇"""
    s = base()
    for x in range(SW):
        put(s, SH - 2, x, LIP_DARK)
    rnd = random.Random(5)
    for _ in range(10):
        tuft(s, rnd.randrange(SH - 7, SH - 4), rnd.randrange(SW), rnd.random() < 0.5)
    for _ in range(8):
        tuft(s, rnd.randrange(4, SH - 8), rnd.randrange(SW), rnd.random() < 0.3)
    for x in (6, 9, 27, 31):
        reed(s, SH - 5, x, rnd.randrange(8, 12))
    return s


def strip_wood():
    """C 나무 말뚝 둑: 턱 위에 가로 통나무 널(세 줄) + 한 칸(40칸)마다 말뚝 하나(밧줄 없음, 울타리로 보이지 않게)"""
    s = base()
    for x in range(SW):
        put(s, SH - 5, x, LINE); put(s, SH - 6, x, WOOD); put(s, SH - 7, x, WOOD); put(s, SH - 8, x, WOOD_HI); put(s, SH - 9, x, LINE)
    px = 17
    for y in range(SH - 16, SH - 4):
        put(s, y, px, LINE); put(s, y, px + 5, LINE)
        put(s, y, px + 1, WOOD_HI); put(s, y, px + 2, WOOD); put(s, y, px + 3, WOOD); put(s, y, px + 4, WOOD_LO)
    for x in range(px, px + 6):
        put(s, SH - 17, x, LINE)
    put(s, SH - 16, px + 1, WOOD_HI)
    return s


STRIPS = {'a': strip_pebble, 'b': strip_grass, 'c': strip_wood}


def save(img, path, scale=2):
    Image.fromarray(np.repeat(np.repeat(img, scale, 0), scale, 1), 'RGBA').save(path)


def scene(cells_w, cells_h, bank_row, strip, props=(), wombat=True, floor_on=True):
    """바닥 + 띠(아래변 = bank_row) + 흙 면 · 거품 · 물 + 아래 벽, 한 칸 1px. props = [(그림 RGBA, 발끝 x, 발끝 y)]"""
    floor, wall, face, water = tex('Shop/floor_tile.png'), tex('Shop/wall_tile.png'), tex('Fishing/stream_face.png'), tex('Fishing/water_tile_0.png')
    yy, xx = np.mgrid[0:cells_h, 0:cells_w]
    img = floor[yy % 32, xx % 32].copy() if floor_on else np.full((cells_h, cells_w, 4), 255, np.uint8)
    F = face.shape[0]
    for y in range(bank_row, cells_h):
        k = y - bank_row
        if k < F:
            img[y] = face[k, xx[y] % face.shape[1]]
        elif k == F:
            img[y, :, :3] = LIGHT; img[y, :, 3] = 255
        else:
            img[y] = water[y % 32, xx[y] % 32]
    out = Image.fromarray(img, 'RGBA')
    tile = Image.fromarray(strip, 'RGBA')
    for x in range(-(cells_w // SW) - 1, cells_w // SW + 2):
        px = x * SW + (cells_w // 2) % SW
        if -SW < px < cells_w:
            out.alpha_composite(tile, (px, bank_row - SH))
    for sp, fx, fy in sorted(props, key=lambda p: p[2]):
        im = Image.fromarray(sp, 'RGBA')
        out.alpha_composite(im, (int(fx - im.width / 2), int(fy - im.height)))
    if wombat:
        wb = Image.open(os.path.join(W, 'Shop', 'wombat_front.png')).convert('RGBA')
        wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
        out.alpha_composite(wb, (cells_w // 2 - wb.width // 2, bank_row - 12 - wb.height))
    return out


def template(k, path):
    """Codex 소품 바탕(한 칸 10px): 흰 바탕, 아래쪽에 띠 + 흙 면 + 물, 왼쪽에 웜뱃, 빨간 발끝 표시 셋"""
    cw, ch = 153, 102
    bank_row = 72
    sc = scene(cw, ch, bank_row, STRIPS[k](), wombat=False, floor_on=False)
    wb = Image.open(os.path.join(W, 'Shop', 'wombat_front.png')).convert('RGBA')
    wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
    sc.alpha_composite(wb, (6, bank_row - 10 - wb.height))
    big = sc.resize((cw * 10, ch * 10), Image.NEAREST).convert('RGB')
    d = ImageDraw.Draw(big)
    for fx in (62, 92, 124):
        d.line((fx * 10 - 60, (bank_row - SH + 3) * 10, fx * 10 + 60, (bank_row - SH + 3) * 10), fill=(220, 40, 40), width=4)
    big.save(path)
    return big


def extract(k, raw_path, template_path):
    """Codex 결과에서 바탕과 다른 칸만(10px 격자) 떼어 소품 셋으로: 발끝 표시 가까운 순"""
    raw = np.asarray(Image.open(raw_path).convert('RGB')).astype(int)
    tpl = np.asarray(Image.open(template_path).convert('RGB')).astype(int)
    cw, ch = 153, 102
    cells = np.zeros((ch, cw, 4), np.uint8)
    diff = np.zeros((ch, cw), bool)
    for y in range(ch):
        for x in range(cw):
            r = raw[y * 10 + 3:y * 10 + 8, x * 10 + 3:x * 10 + 8].reshape(-1, 3)
            t = tpl[y * 10 + 3:y * 10 + 8, x * 10 + 3:x * 10 + 8].reshape(-1, 3)
            col = np.median(r, 0).round()
            cells[y, x, :3] = col
            cells[y, x, 3] = 255
            diff[y, x] = np.abs(col - np.median(t, 0)).max() > 40
    # 빨간 표시 자리는 바탕과 다른 것으로 잡히므로(지웠다) 뺀다: 흰 바탕 · 흰에 가까운 칸도 뺀다
    diff &= ~((cells[..., :3].min(-1) > 238))
    sys.path.insert(0, os.path.join(W, 'Shop', 'Source~'))
    from make_dig import blobs
    # 표시마다 가까운(가로 ±16칸) 덩어리 중 가장 큰 것 + 그 테두리에 닿는 덩어리만(Codex가 둑 위에 덧그린 자갈 · 풀 조각은 버린다)
    marks = [62, 92, 124]
    near = [[] for _ in marks]
    for gr in blobs(diff, diag=True):
        if len(gr) >= 6:
            cx = np.mean([x for _, x in gr])
            i = int(np.argmin([abs(cx - m) for m in marks]))
            if abs(cx - marks[i]) <= 16:
                near[i].append(gr)
    groups = []
    for cand in near:
        if not cand:
            groups.append([]); continue
        cand.sort(key=len, reverse=True)
        keep, rest = [cand[0]], cand[1:]
        box = lambda g: (min(y for y, _ in g) - 1, min(x for _, x in g) - 1, max(y for y, _ in g) + 1, max(x for _, x in g) + 1)
        changed = True
        while changed:
            changed = False
            y0, x0, y1, x1 = box([c for g in keep for c in g])
            for g in rest[:]:
                b = box(g)
                if b[2] >= y0 and b[0] <= y1 and b[3] >= x0 and b[1] <= x1:
                    keep.append(g); rest.remove(g); changed = True
        groups.append([c for g in keep for c in g])
    for name, gr in zip(PROPS[k], groups):
        if not gr:
            continue
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = cells[y, x]
        yield name, ic


def mock(k, out_dir, props_dir=None):
    """1080×1920 모형(한 칸 4px): 카메라 가운데 (0, −8.5), 둑 선 y −8.6, 아래 벽 y −14"""
    cw, ch = 270, 480
    bank_row = int((-2.5 - (-8.6)) * 40)
    props = []
    if props_dir:
        for name, fx in zip(PROPS[k], (-2.5, 1.7, 2.9)):
            p = os.path.join(props_dir, 'bank_%s.png' % name)
            if os.path.exists(p):
                sp = Image.open(p).convert('RGBA')
                sp = sp.resize((sp.width // 2, sp.height // 2), Image.NEAREST)
                props.append((np.asarray(sp), cw / 2 + fx * 40, bank_row - 10))
    sc = scene(cw, ch, bank_row, STRIPS[k](), props)
    a = np.asarray(sc).copy()
    wall = tex('Shop/wall_tile.png')
    bottom = int((-2.5 - (-14.0)) * 40)
    yy, xx = np.mgrid[0:ch, 0:cw]
    a[bottom:] = wall[yy[bottom:] % 32, xx[bottom:] % 32]
    a[bottom, :, :3] = LINE; a[bottom + 1:bottom + 5, :, :3] = LIP; a[bottom + 5, :, :3] = LINE
    Image.fromarray(a, 'RGBA').resize((cw * 4, ch * 4), Image.NEAREST).save(os.path.join(out_dir, 'bank_mock_%s.png' % k))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pick'
    if cmd == 'strips':
        out = sys.argv[2]; os.makedirs(out, exist_ok=True)
        for k, fn in STRIPS.items():
            save(fn(), os.path.join(out, 'bank_%s.png' % k))
            template(k, os.path.join(out, 'bank_%s_template.png' % k))
            print(k, 'strip 40x24 · template')
    elif cmd == 'props':
        out = sys.argv[2]
        for k in 'abc':
            raw = os.path.join(out, 'bank_%s_props.png' % k)
            if not os.path.exists(raw):
                continue
            od = os.path.join(out, 'out_' + k); os.makedirs(od, exist_ok=True)
            for name, ic in extract(k, raw, os.path.join(out, 'bank_%s_template.png' % k)):
                save(ic, os.path.join(od, 'bank_%s.png' % name))
                print('%s bank_%s.png %dx%d칸' % (k, name, ic.shape[1], ic.shape[0]))
    elif cmd == 'mock':
        out = sys.argv[2]
        for k in 'abc':
            mock(k, out, os.path.join(out, 'out_' + k))
            print(k, 'mock')
    else:
        save(STRIPS[PICK](), os.path.join(HERE, '..', 'bank.png'))
        for name, ic in extract(PICK, os.path.join(HERE, 'raw', 'bank_%s_props.png' % PICK), os.path.join(HERE, 'raw', 'bank_%s_template.png' % PICK)):
            save(ic, os.path.join(HERE, '..', 'bank_%s.png' % name))
            print('bank_%s.png %dx%d칸' % (name, ic.shape[1], ic.shape[0]))
        print('bank.png 40x24칸 시안', PICK)
