# -*- coding: utf-8 -*-
# 루프 기획 장면 합성기: 현재 에셋으로 메인 루프 장면 띠(패널 4장)를 만든다. 장면 = 존 하나 + 길 고리 + 잔디 + 숲 일부, 위에 개념별 오버레이
# 사용: python panels.py <출력 폴더> [specs.json]   (1회차 specs.json · 2회차 specs2.json · 3회차 specs3.json · 4회차 specs4.json)
from PIL import Image, ImageDraw, ImageFont
import numpy as np, os, sys, json, re, math
A = 'C:/project/Tycoon/ProjectTycoon/Assets/'
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
W, H = 400, 280
OX, OY = 232, 242
FONT = 'C:/Windows/Fonts/malgunbd.ttf'
INK = (52, 32, 32)
def font(s): return ImageFont.truetype(FONT, s)
def load(p): return np.asarray(Image.open(p).convert('RGBA'))

# ---------- 주기 텍스처 복원(시트 → 192×96) ----------
def period(sheet):
    sh = load(sheet); tex = np.zeros((96, 192, 4), np.uint8)
    for d in range(6):
        for k in range(3):
            s_ = (d % 2) + 2 * k; t = sh[:, (d * 3 + k) * 64:(d * 3 + k + 1) * 64]
            for y in range(32):
                hw = (y + 1) * 2 if y < 16 else (32 - y) * 2
                for x in range(32 - hw, 32 + hw):
                    tex[(-(16 * s_ + 16 - y)) % 96, (32 * d + x - 32) % 192] = t[y, x]
    return tex
GRASS, DIRT, PATH = period(A + 'Sprites/World/Tiles/grass.png'), period(A + 'Sprites/World/Tiles/dirt.png'), period(A + 'Sprites/World/Tiles/path.png')
FENCE, POST = load(A + 'Sprites/World/fence.png'), load(A + 'Sprites/World/fence_post.png')
TREE, TREE_S, BUSH = load(A + 'Sprites/World/Border/tree_big.png'), load(A + 'Sprites/World/Border/tree_small.png'), load(A + 'Sprites/World/Border/bush.png')
COIN = np.asarray(Image.open(A + 'Sprites/World/coin.png').convert('RGBA').resize((12, 12), Image.NEAREST))
def fit(path, h):
    im = Image.open(path).convert('RGBA'); im = im.crop(im.getbbox())
    return np.asarray(im.resize((max(1, int(im.size[0] * h / im.size[1])), h), Image.NEAREST))
WOMBAT, BABY = fit(A + 'Resources/Sprites/Animals/Wombat/Wombat.png', 34), fit(A + 'Resources/Sprites/Animals/Wombat/Wombat.png', 20)
VISITOR = fit(A + 'Resources/Sprites/Visitors/Visitor/Visitor.png', 40)
SIGN, BENCH, SHOP, FTREE = fit(A + 'Resources/Sprites/Facilities/Sign/Sign.png', 40), fit(A + 'Resources/Sprites/Facilities/Bench/Bench.png', 22), fit(A + 'Resources/Sprites/Facilities/Shop/Shop.png', 44), fit(A + 'Resources/Sprites/Facilities/Tree/Tree.png', 44)
LOCK = fit('C:/Users/jiwon/AppData/Local/Temp/claude/C--project-Tycoon/fa162f9e-8d3f-47f0-9626-25c6a5841ff4/scratchpad/imgtest/test.png', 26)
def recolor(img, fn):
    a = img.copy(); rgb = a[..., :3].astype(int); dark = rgb.sum(2) < 200
    out = fn(rgb).clip(0, 255).astype(np.uint8); out[dark] = rgb[dark]; a[..., :3] = out; return a
def flat(img, color):
    a = img.copy(); a[..., :3] = np.where(a[..., 3:4] > 0, np.array(color, np.uint8), a[..., :3]); return a
ALBINO = recolor(WOMBAT, lambda c: c * 0.35 + np.array([245, 238, 228]) * 0.65)
GOLDEN = recolor(WOMBAT, lambda c: c * 0.5 + np.array([236, 196, 90]) * 0.5)
KEEPER = flat(VISITOR, (96, 140, 92))        # 사육사(플레이어) 더미: 녹색 실루엣
HELPER = flat(VISITOR, (86, 118, 168))       # 고용 사육사(봇) 더미: 파란 실루엣
SIL = flat(WOMBAT, (58, 54, 66))             # 야생 실루엣(흑백 출현) 더미
def hat(img, color):
    a = img.copy(); top = np.where(a[..., 3].any(1))[0][0]; band = a[top:top + 7]; band[..., :3] = np.where(band[..., 3:4] > 0, np.array(color, np.uint8), band[..., :3]); return a
HATS = {'v_r': hat(VISITOR, (226, 84, 72)), 'v_y': hat(VISITOR, (244, 204, 72)), 'v_b': hat(VISITOR, (84, 140, 220))}
def shape(w, h, fn):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); fn(ImageDraw.Draw(im)); return np.asarray(im)
STUMP = shape(22, 14, lambda d: (d.ellipse([0, 4, 21, 13], fill=(110, 72, 40), outline=INK), d.ellipse([3, 1, 18, 9], fill=(196, 156, 104), outline=INK)))
TROUGH = shape(30, 16, lambda d: (d.rectangle([0, 5, 29, 15], fill=(120, 80, 45), outline=INK), d.rectangle([3, 0, 26, 8], fill=(232, 190, 84), outline=INK)))
ACTORS = {'w': WOMBAT, 'b': BABY, 'al': ALBINO, 'go': GOLDEN, 'v': VISITOR, 'k': KEEPER, 'kb': HELPER, 'sil': SIL, 'sign': SIGN, 'bench': BENCH, 'shop': SHOP, 'ftree': FTREE, 'lock': LOCK,
          'tree': TREE, 'tree_s': TREE_S, 'bush': BUSH, 'stump': STUMP, 'trough': TROUGH, **HATS}
DROPS = {
    'egg': shape(10, 12, lambda d: d.ellipse([0, 0, 9, 11], fill=(250, 246, 232), outline=INK)),
    'fur': shape(12, 9, lambda d: (d.polygon([(0, 8), (3, 0), (6, 8)], fill=(150, 100, 60)), d.polygon([(5, 8), (8, 1), (11, 8)], fill=(120, 80, 45)))),
    'gift': shape(11, 11, lambda d: (d.rectangle([0, 0, 10, 10], fill=(236, 120, 150), outline=INK), d.line([5, 0, 5, 10], fill=(255, 240, 200)), d.line([0, 5, 10, 5], fill=(255, 240, 200)))),
    'log': shape(14, 7, lambda d: (d.rectangle([0, 0, 13, 6], fill=(140, 90, 50), outline=INK), d.ellipse([10, 0, 13, 6], fill=(200, 160, 110)))),
    'feed': shape(9, 9, lambda d: d.ellipse([0, 0, 8, 8], fill=(240, 150, 60), outline=INK)),
    'rock': shape(11, 9, lambda d: d.ellipse([0, 0, 10, 8], fill=(150, 150, 150), outline=INK)),
}
TILES = {'water': (96, 156, 204), 'rock': (160, 158, 150), 'bamboo': (126, 178, 96), 'mud': (118, 86, 60), 'burrow': (64, 48, 40),
         'grass1': (150, 170, 96), 'grass2': (118, 176, 88), 'grass3': (84, 150, 70), 'trail': (170, 140, 100), 'roof': (70, 62, 84), 'glass': (170, 214, 226), 'lit': (250, 226, 130)}
def sx(x, z): return OX + int(32 * (x - z))
def sy(x, z): return OY - int(16 * (x + z))

# ---------- 기본 장면 ----------
def base_scene():
    py, px = np.mgrid[0:H, 0:W]; wx = px - OX; wy = OY - py
    lx = (wx / 32.0 + wy / 16.0) / 2.0; lz = (wy / 16.0 - wx / 32.0) / 2.0
    zone = (lx >= 0) & (lx < 6) & (lz >= 0) & (lz < 8)
    ring = (lx >= -2) & (lx < 8) & (lz >= -2) & (lz < 10) & ~zone
    u = np.floor(wx).astype(int) % 192; v = (15 - np.floor(wy).astype(int)) % 96
    cv = GRASS[v, u].copy(); cv[ring] = PATH[v, u][ring]; cv[zone] = DIRT[v, u][zone]
    def edge_of(m):
        e = m.copy(); e[1:] &= m[:-1]; e[:-1] &= m[1:]; e[:, 1:] &= m[:, :-1]; e[:, :-1] &= m[:, 1:]; return m & ~e
    cv[edge_of(zone) | edge_of(ring | zone)] = INK + (255,)
    return cv
BASE = base_scene()
def blit(cv, img, pcol, prow, x, y):
    h, w = img.shape[:2]; left = x - pcol; top = y - (h - 1 - prow)
    x0, y0 = max(left, 0), max(top, 0); x1, y1 = min(left + w, W), min(top + h, H)
    if x1 <= x0 or y1 <= y0: return
    src = img[y0 - top:y1 - top, x0 - left:x1 - left]; m = src[..., 3] > 0
    cv[y0:y1, x0:x1][m] = src[m]
def fences(spec):
    items = []; ff = FENCE[:, ::-1]
    if not spec.get('nofence'):
        for x in range(0, 6):
            items.append((sy(x, 0), FENCE, 4, sx(x, 0))); items.append((sy(x, 8), FENCE, 4, sx(x, 8)))
        for z in range(0, 8):
            items.append((sy(0, z), ff, 33, sx(0, z))); items.append((sy(6, z), ff, 33, sx(6, z)))
        items.append((sy(6, 8), POST, 4, sx(6, 8)))
    if not spec.get('notrees'):
        for (x, z, im) in ((-3.2, 0.5, TREE), (-3.6, 2.8, TREE_S), (-3.1, 4.6, TREE), (-2.9, 6.5, BUSH), (-3.8, 5.6, TREE_S), (8.6, -0.6, BUSH), (1.5, -3.2, TREE_S), (4.6, -3.4, BUSH)):
            items.append((sy(x, z), im, im.shape[1] // 2, sx(x, z)))
    return items

# ---------- 오버레이 ----------
EMOJI = re.compile('[\U0001F000-\U0001FFFF\u2700-\u27BF\u2600-\u2604\u2606-\u26FF]')
def clean(t): return EMOJI.sub('', t).replace('  ', ' ')
def draw_card(im, x, y, w, h, title, lines, accent=(122, 74, 46)):
    title = clean(title); lines = [clean(l) for l in lines]; h = 30 + 16 * len(lines)
    d = ImageDraw.Draw(im, 'RGBA')
    d.rounded_rectangle([x, y, x + w, y + h], 6, fill=(255, 252, 245, 235), outline=INK + (255,), width=2)
    d.rectangle([x + 2, y + 2, x + w - 2, y + 20], fill=accent + (255,))
    d.text((x + 8, y + 3), title, font=font(12), fill=(255, 255, 255))
    for i, ln in enumerate(lines): d.text((x + 8, y + 26 + i * 16), ln, font=font(12), fill=(40, 34, 30))
def draw_bubble(im, x, y, text, tail=True):
    text = clean(text); d = ImageDraw.Draw(im, 'RGBA'); f = font(12); tw = int(d.textlength(text, font=f)) + 14
    x = max(4, min(x, W - tw - 4))
    d.rounded_rectangle([x, y, x + tw, y + 22], 8, fill=(255, 255, 255, 240), outline=INK, width=2)
    if tail: d.polygon([(x + 12, y + 22), (x + 22, y + 22), (x + 14, y + 30)], fill=(255, 255, 255), outline=INK)
    d.text((x + 7, y + 4), text, font=f, fill=(40, 34, 30))
def draw_hearts(im, x, y, n=3):
    d = ImageDraw.Draw(im, 'RGBA')
    for i in range(n):
        hx, hy = x + (i - 1) * 12, y - i * 7
        d.ellipse([hx - 4, hy - 4, hx, hy], fill=(232, 96, 120)); d.ellipse([hx, hy - 4, hx + 4, hy], fill=(232, 96, 120))
        d.polygon([(hx - 4, hy - 1), (hx + 4, hy - 1), (hx, hy + 5)], fill=(232, 96, 120))
def draw_coins(cv, x, y, n=4):
    for i in range(n): blit(cv, COIN, 6, 0, x + (i - n // 2) * 10, y - 8 - (i % 2) * 9)
def draw_text(im, x, y, text, size=13, color=(60, 140, 70)):
    text = clean(text); d = ImageDraw.Draw(im, 'RGBA'); f = font(size)
    for ox, oy in ((1, 1), (-1, 1), (1, -1), (-1, -1)): d.text((x + ox, y + oy), text, font=f, fill=(255, 255, 255))
    d.text((x, y), text, font=f, fill=tuple(color))
def draw_plus(im, x, y, text, color=(60, 140, 70)): draw_text(im, x, y, text, 13, color)
def draw_gauge(im, x, y, w, ratio, label):
    d = ImageDraw.Draw(im, 'RGBA'); d.rounded_rectangle([x, y, x + w, y + 12], 6, fill=(255, 255, 255, 230), outline=INK, width=2)
    d.rounded_rectangle([x + 2, y + 2, x + 2 + int((w - 4) * ratio), y + 10], 5, fill=(93, 160, 88)); draw_text(im, x, y - 16, clean(label), 11, (40, 34, 30))
def draw_timing(im, x, y, w, marker, z0, z1, label):
    d = ImageDraw.Draw(im, 'RGBA'); d.rounded_rectangle([x, y, x + w, y + 14], 6, fill=(255, 255, 255, 235), outline=INK, width=2)
    d.rectangle([x + 2 + int((w - 4) * z0), y + 2, x + 2 + int((w - 4) * z1), y + 12], fill=(93, 160, 88))
    mx = x + 2 + int((w - 4) * marker); d.polygon([(mx - 5, y - 6), (mx + 5, y - 6), (mx, y + 1)], fill=(232, 96, 120), outline=INK)
    draw_text(im, x, y - 24, clean(label), 11, (40, 34, 30))
def draw_arrow(im, x0, y0, x1, y1):
    d = ImageDraw.Draw(im, 'RGBA'); d.line([x0, y0, x1, y1], fill=INK, width=3); ang = math.atan2(y1 - y0, x1 - x0)
    d.polygon([(x1, y1), (x1 - 10 * math.cos(ang - 0.5), y1 - 10 * math.sin(ang - 0.5)), (x1 - 10 * math.cos(ang + 0.5), y1 - 10 * math.sin(ang + 0.5))], fill=INK)
def draw_arc(im, x0, y0, x1, y1, lift=60):
    d = ImageDraw.Draw(im, 'RGBA'); n = 14; cx, cy = (x0 + x1) / 2, min(y0, y1) - lift; pts = []
    for i in range(n + 1):
        t = i / n; pts.append(((1 - t) ** 2 * x0 + 2 * (1 - t) * t * cx + t * t * x1, (1 - t) ** 2 * y0 + 2 * (1 - t) * t * cy + t * t * y1))
    for (px, py) in pts[:-1]: d.ellipse([px - 2, py - 2, px + 2, py + 2], fill=(255, 255, 255), outline=INK)
    (ax, ay), (bx, by) = pts[-2], pts[-1]; draw_arrow(im, ax, ay, bx, by)
def draw_dash(im, x0, z0, x1, z1, color=(232, 96, 120), lift=14):
    d = ImageDraw.Draw(im, 'RGBA'); ax, ay, bx, by = sx(x0, z0), sy(x0, z0) - lift, sx(x1, z1), sy(x1, z1) - lift
    n = max(2, int(math.hypot(bx - ax, by - ay) / 9))
    for i in range(0, n, 2):
        t0, t1 = i / n, min(1, (i + 1) / n); d.line([ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1], fill=tuple(color), width=3)
    ang = math.atan2(by - ay, bx - ax)
    d.polygon([(bx, by), (bx - 9 * math.cos(ang - 0.5), by - 9 * math.sin(ang - 0.5)), (bx - 9 * math.cos(ang + 0.5), by - 9 * math.sin(ang + 0.5))], fill=tuple(color))
def draw_zonetint(im, color, alpha, x0=0, z0=0, x1=6, z1=8):
    ov = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    d.polygon([(sx(x0, z0), sy(x0, z0)), (sx(x1, z0), sy(x1, z0)), (sx(x1, z1), sy(x1, z1)), (sx(x0, z1), sy(x0, z1))], fill=tuple(color) + (int(255 * alpha),)); return Image.alpha_composite(im, ov)
def draw_frame(im, x, y, w, h):
    d = ImageDraw.Draw(im, 'RGBA'); L = 10
    for (cx, cy, dx, dy) in ((x, y, 1, 1), (x + w, y, -1, 1), (x, y + h, 1, -1), (x + w, y + h, -1, -1)):
        d.line([cx, cy, cx + dx * L, cy], fill=(255, 255, 255), width=3); d.line([cx, cy, cx, cy + dy * L], fill=(255, 255, 255), width=3)
def draw_hand(im, x, y, mode='tap', dx=0, dy=0):
    d = ImageDraw.Draw(im, 'RGBA')
    if mode == 'drag':
        n = 9
        for i in range(n): px, py = x + dx * i / n, y + dy * i / n; d.ellipse([px - 2, py - 2, px + 2, py + 2], fill=(255, 255, 255, 230), outline=INK)
        draw_arrow(im, x + dx * 0.85, y + dy * 0.85, x + dx, y + dy); x, y = x + dx, y + dy
    if mode in ('tap', 'hold'):
        for r in (9, 15): d.ellipse([x - r, y - r, x + r, y + r], outline=(255, 255, 255, 220), width=2)
    d.rounded_rectangle([x - 3, y - 2, x + 3, y + 16], 3, fill=(255, 244, 230), outline=INK, width=2)
    d.rounded_rectangle([x - 9, y + 10, x + 11, y + 26], 6, fill=(255, 244, 230), outline=INK, width=2)
def draw_button(im, x, y, w, label, sub, hot=False):
    d = ImageDraw.Draw(im, 'RGBA'); fill = (246, 160, 70) if hot else (222, 134, 52)
    d.rounded_rectangle([x, y, x + w, y + 34], 6, fill=fill, outline=(255, 255, 255) if hot else INK, width=2)
    d.text((x + 8, y + 3), clean(label), font=font(12), fill=(255, 255, 255)); d.text((x + 8, y + 18), clean(sub), font=font(11), fill=(70, 40, 20))
def draw_minicards(im, x, y, labels, sel=-1):
    d = ImageDraw.Draw(im, 'RGBA'); cw, ch = 62, 40
    for i, lb in enumerate(labels):
        cx = x + i * (cw + 6); cy = y - (6 if i == sel else 0)
        d.rounded_rectangle([cx, cy, cx + cw, cy + ch], 5, fill=(255, 252, 245, 240), outline=(46, 94, 138) if i == sel else INK, width=3 if i == sel else 2)
        d.rectangle([cx + 3, cy + 3, cx + cw - 3, cy + 16], fill=(231, 238, 226))
        d.text((cx + 6, cy + 20), clean(lb), font=font(11), fill=(40, 34, 30))
def tint(im, color, alpha):
    ov = Image.new('RGBA', im.size, tuple(color) + (int(255 * alpha),)); return Image.alpha_composite(im, ov)
def ribbon(im, text):
    d = ImageDraw.Draw(im, 'RGBA'); f = font(13); tw = int(d.textlength(text, font=f)) + 16
    d.rectangle([0, 0, tw, 22], fill=INK + (230,)); d.text((8, 3), text, font=f, fill=(255, 250, 240))
def draw_tile(im, x, z, kind):
    d = ImageDraw.Draw(im, 'RGBA'); cx, cy = sx(x + 0.5, z + 0.5), sy(x + 0.5, z + 0.5); c = TILES[kind]
    if kind == 'burrow':
        d.polygon([(cx, cy - 16), (cx + 32, cy), (cx, cy + 16), (cx - 32, cy)], fill=(118, 86, 60), outline=INK)
        d.ellipse([cx - 14, cy - 7, cx + 14, cy + 7], fill=c, outline=INK); return
    d.polygon([(cx, cy - 16), (cx + 32, cy), (cx, cy + 16), (cx - 32, cy)], fill=c, outline=INK)
    if kind == 'bamboo':
        for k in (-14, 0, 14): d.line([cx + k, cy - 8, cx + k, cy + 8], fill=(70, 120, 60), width=2)
    if kind == 'water': d.line([cx - 12, cy, cx - 4, cy - 3, cx + 4, cy, cx + 12, cy - 3], fill=(200, 230, 250), width=2)
    if kind == 'rock': d.ellipse([cx - 8, cy - 5, cx + 8, cy + 5], fill=(190, 188, 180), outline=INK)

# ---------- 패널 ----------
def panel(spec):
    cv = BASE.copy()
    if 'tiles' in spec:
        im0 = Image.fromarray(cv)
        for (x, z, kind) in spec['tiles']: draw_tile(im0, x, z, kind)
        cv = np.asarray(im0).copy()
    if 'zonetint' in spec:
        im0 = Image.fromarray(cv)
        for z in spec['zonetint']: im0 = draw_zonetint(im0, *z)
        cv = np.asarray(im0).copy()
    for (x, z, kind, n) in spec.get('drops', []):
        rng = np.random.default_rng(int(abs(x * 31 + z * 17 + n * 7)))
        for i in range(n): blit(cv, DROPS[kind], 5, 0, sx(x, z) + int(rng.integers(-18, 19)), sy(x, z) + int(rng.integers(-9, 10)))
    items = fences(spec)
    for (kind, x, z, *rest) in spec.get('actors', []):
        img = ACTORS[kind]
        if rest and rest[0] == 'flip': img = img[:, ::-1]
        items.append((sy(x, z), img, img.shape[1] // 2, sx(x, z)))
    for wy, img, pc, wx in sorted(items, key=lambda t: -t[0]): blit(cv, img, pc, 0, wx, wy)
    for (x, z, n) in spec.get('coins', []): draw_coins(cv, sx(x, z), sy(x, z) - 30, n)
    im = Image.fromarray(cv)
    if 'tint' in spec: im = tint(im, *spec['tint'])
    for a in spec.get('dashes', []): draw_dash(im, *a)
    for (x, z, n) in spec.get('hearts', []): draw_hearts(im, sx(x, z), sy(x, z) - 36, n)
    for (x, z, t, *c) in spec.get('plus', []): draw_plus(im, sx(x, z) - 10, sy(x, z) - 48, t, *c)
    for (x, z, r, l) in spec.get('bars', []): draw_gauge(im, sx(x, z) - 36, sy(x, z) - 54, 72, r, l)
    for (x, z, t) in spec.get('bubbles', []): draw_bubble(im, sx(x, z) - 10, sy(x, z) - 70, t)
    for (x, y, w, h, title, *lines) in spec.get('cards', []): draw_card(im, x, y, w, h, title, lines)
    for (x, y, w, r, l) in spec.get('gauges', []): draw_gauge(im, x, y, w, r, l)
    for (x, y, w, m, z0, z1, l) in spec.get('timing', []): draw_timing(im, x, y, w, m, z0, z1, l)
    for a in spec.get('arrows', []): draw_arrow(im, *a)
    for a in spec.get('arcs', []): draw_arc(im, *a)
    for f in spec.get('frames', []): draw_frame(im, *f)
    for (x, y, *b) in spec.get('buttons', []): draw_button(im, x, y, *b)
    for (x, y, labels, *s) in spec.get('minicards', []): draw_minicards(im, x, y, labels, *s)
    for (x, y, t, *rest) in spec.get('labels', []): draw_text(im, x, y, t, *rest)
    for (x, y, *rest) in spec.get('hand', []): draw_hand(im, x, y, *rest)
    if 'ribbon' in spec: ribbon(im, spec['ribbon'])
    if 'rain' in spec:
        d = ImageDraw.Draw(im, 'RGBA'); rng = np.random.default_rng(5)
        for _ in range(90): x, y = int(rng.integers(0, W)), int(rng.integers(0, H)); d.line([x, y, x - 3, y + 9], fill=(210, 228, 250, 200), width=1)
    if 'snow' in spec:
        d = ImageDraw.Draw(im, 'RGBA'); rng = np.random.default_rng(3)
        for _ in range(60): x, y = rng.integers(0, W), rng.integers(0, H); d.ellipse([x, y, x + 2, y + 2], fill=(255, 255, 255, 220))
    return im

def strip(name, panels):
    ims = [panel(p) for p in panels]
    out = Image.new('RGBA', (W * 2 + 8, H * 2 + 8), (243, 246, 239, 255))
    for i, im in enumerate(ims): out.paste(im, ((i % 2) * (W + 8), (i // 2) * (H + 8)))
    out.save(OUT + '/' + name + '.png'); return name

CONCEPTS = json.load(open(sys.argv[2] if len(sys.argv) > 2 else os.path.dirname(os.path.abspath(__file__)) + '/specs.json', encoding='utf-8'))
for c in CONCEPTS:
    strip(c['id'], c['panels']); print('ok', c['id'])
