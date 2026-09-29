# -*- coding: utf-8 -*-
# 빵집 소품 C 「웜뱃 굴 공방」(2026-09-29 사용자 선택). 시점은 모든 리소스 공통 「앞쪽 위에서 비스듬히 내려다본 평행 투영」(art.md 시점).
#   오븐 1·2단: Codex 시트 shop_raw/props_c_sheet.png(프롬프트 props_prompt_common.txt + props_prompt_c.txt)
#   식빵 · 계산대 돌 받침 · 단지: Codex 시트 shop_raw/props_c45_sheet.png(프롬프트 props_prompt_c45.txt, 약 45도)
#   진열대: 받침대 위 나무 빵 상자 하나(사용자 선택 3-1). Codex(shop_raw/shelf_crate_raw.png, 프롬프트 shelf_prompt_crate.txt)에
#     시점 블록아웃(shop_raw/view_blockout.png, make_view_blockout.py)을 참조로 줬다. Codex는 말로만 평행 투영을 시키면 판을 사다리꼴로 그렸다
#   계산대 통나무 · 흙 단지: 칸 무늬로 직접 그린다(Codex는 통나무 끝에 나이테 면을 그려 「아래에서 올려다본 듯」). 윗면 깊이 = TOP 줄
# 순서(Codex 그림): 시트에서 왼쪽부터 자름 → make_pixel.py(한 칸 2px, 가로 칸 수 = 옛 그림) → 색 줄이기(가중 k-평균) → 외곽선 (48,24,24) → 외톨이 칸 정리
# 출력: ../oven.png · ../oven_2.png · ../shelf.png · ../counter.png, Resources/Sprites/Shop/Breads/b01.png. 다음: make_oven_fx.py(불빛 · 연기)
import os
import subprocess
import sys
import tempfile
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..')
BREAD = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Shop', 'Breads', 'b01.png')
MAKE_PIXEL = os.path.join(HERE, '..', '..', 'Source~', 'make_pixel.py')
OUTLINE = (48, 24, 24)   # 가게 소품 공통 외곽선(옛 소품 · 입구 · 식빵과 같다)
TOP = 6                  # 계산대 통나무 윗면 줄 수(내려다보는 각도). 크면 더 위에서 본다


def load_cells(path):
    a = np.asarray(Image.open(path).convert('RGBA'))[::2, ::2].copy()
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    return a


def save_cells(a, path):
    Image.fromarray(a, 'RGBA').resize((a.shape[1] * 2, a.shape[0] * 2), Image.NEAREST).save(path)


def cut(sheet, n):
    """흰 바탕 시트에서 가로로 떨어진 덩어리 n개(왼쪽부터)"""
    rgb = np.asarray(sheet.convert('RGB')).astype(int)
    ink = (rgb < 235).any(-1)
    segs = []
    for x in np.nonzero(ink.any(0))[0]:
        if segs and x - segs[-1][1] <= 24:
            segs[-1][1] = x
        else:
            segs.append([x, x])
    segs = sorted(sorted(segs, key=lambda g: g[1] - g[0], reverse=True)[:n])
    out = []
    for x0, x1 in segs:
        rows = np.nonzero(ink[:, x0:x1 + 1].any(1))[0]
        out.append(sheet.crop((x0 - 8, rows.min() - 8, x1 + 9, rows.max() + 9)))
    return out


def reduce_colors(a, k):
    """불투명 칸 색을 k개로(가중 k-평균, 빈도 높은 색에서 시작). 가장 어두운 색은 외곽선 색으로"""
    op = a[..., 3] > 0
    cols, counts = np.unique(a[op][:, :3].astype(float), axis=0, return_counts=True)
    order = np.argsort(-counts)
    cent = []
    for i in order:
        if all(np.abs(cols[i] - c).sum() > 48 for c in cent):
            cent.append(cols[i])
        if len(cent) == k:
            break
    cent = np.array(cent)
    for _ in range(20):
        lab = ((cols[:, None] - cent[None]) ** 2).sum(-1).argmin(1)
        for j in range(len(cent)):
            m = lab == j
            if m.any():
                cent[j] = (cols[m] * counts[m, None]).sum(0) / counts[m].sum()
    cent = np.round(cent / 4) * 4
    cent[cent.sum(1).argmin()] = OUTLINE
    lut = {tuple(c): tuple(int(v) for v in cent[l]) for c, l in zip(cols.astype(int), lab)}
    b = a.copy()
    for y, x in zip(*np.nonzero(op)):
        b[y, x, :3] = lut[tuple(int(v) for v in a[y, x, :3])]
    return b


def edge(op):
    p = np.pad(op, 1)
    return op & ~(p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:])


def outline(a):
    """가장자리 칸은 외곽선 색으로(투명과 닿는 칸)"""
    b = a.copy()
    b[edge(a[..., 3] > 0), :3] = OUTLINE
    return b


def denoise(a):
    """네 이웃이 모두 한 색인데 혼자 다른 칸(칸 격자로 줄일 때 생긴 잡티) → 이웃 색"""
    b = a.copy()
    H, W = a.shape[:2]
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if not a[y, x, 3]:
                continue
            n = [tuple(a[y + dy, x + dx]) for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1))]
            if n[0] == n[1] == n[2] == n[3] and n[0][3] and n[0] != tuple(a[y, x]):
                b[y, x] = n[0]
    return b


def clear_holes(a, min_cells=12):
    """갇힌 흰 바탕(판 사이 · 다리 사이 틈, make_pixel은 가장자리에 이어진 흰색만 지운다) → 투명. 큰 덩어리, 투명과 맞닿은 덩어리,
    둘레가 어두운 덩어리를 지운다. 밝은 겉면 위 작은 흰 칸(하이라이트)은 둔다. 지우면 새로 드러난 조각이 생겨 없을 때까지 되풀이한다"""
    while True:
        b = _clear_holes_once(a, min_cells)
        if (b[..., 3] == a[..., 3]).all():
            return b
        a = b


def _clear_holes_once(a, min_cells):
    white = (a[..., 3] > 0) & (a[..., :3] >= 235).all(-1)
    clear = np.pad(a[..., 3] == 0, 1, constant_values=True)
    seen = np.zeros_like(white)
    b = a.copy()
    for y, x in zip(*np.nonzero(white)):
        if seen[y, x]:
            continue
        stack, cells = [(y, x)], []
        seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            cells.append((cy, cx))
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < a.shape[0] and 0 <= nx < a.shape[1] and white[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        touches = any(clear[cy, cx + 1] or clear[cy + 2, cx + 1] or clear[cy + 1, cx] or clear[cy + 1, cx + 2] for cy, cx in cells)
        ring = [a[ny, nx, :3].astype(int).sum() for cy, cx in cells for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1))
                if 0 <= ny < a.shape[0] and 0 <= nx < a.shape[1] and a[ny, nx, 3] and not white[ny, nx]]
        dark = bool(ring) and np.mean(ring) < 330        # 둘레가 외곽선 · 짙은 나무 = 바탕이 비친 틈(하이라이트는 밝은 겉면에 있다)
        if len(cells) >= min_cells or touches or dark:
            for cy, cx in cells:
                b[cy, cx, 3] = 0
    return b


def mend_outline(a):
    """흰 틈을 지운 자리에서 끊긴 가로 외곽선(판 윗선 등): 좌우 2칸 안에 외곽선이 있고 아래가 불투명한 빈 칸 → 외곽선"""
    b = a.copy()
    line = (a[..., 3] > 0) & (a[..., :3] == OUTLINE).all(-1)
    H, W = line.shape
    for y in range(H - 1):
        for x in range(W):
            if a[y, x, 3] or not a[y + 1, x, 3]:
                continue
            if line[y, max(0, x - 2):x].any() and line[y, x + 1:x + 3].any():
                b[y, x] = OUTLINE + (255,)
    return b


def codex_prop(crop, cells, k, tmp):
    raw, px = os.path.join(tmp, 'raw.png'), os.path.join(tmp, 'px.png')
    crop.save(raw)
    subprocess.run([sys.executable, MAKE_PIXEL, raw, px, '--px=2', f'--cellsw={cells}'], check=True, capture_output=True)
    return outline(denoise(mend_outline(clear_holes(reduce_colors(load_cells(px), k)))))


# ---------- 칸 무늬로 그리는 것(평행 투영: 윗면은 직사각형, 세로 선은 곧다) ----------
# 나무 색(C 시트 진열대에서 뽑음): 외곽선 · 가장 어두움 · 어두움 · 중간 · 밝음 · 더 밝음 · 하이라이트
WOOD = {'O': OUTLINE, 'A': (84, 48, 36), 'B': (120, 72, 48), 'C': (152, 92, 68), 'D': (180, 120, 84), 'E': (204, 144, 96), 'F': (240, 192, 156)}


class Canvas:
    def __init__(self, h, w, pal):
        self.a = np.zeros((h, w, 4), np.uint8)
        self.pal = pal

    def put(self, y, x, ch):
        if ch != '.' and 0 <= y < self.a.shape[0] and 0 <= x < self.a.shape[1]:
            self.a[y, x] = self.pal[ch] + (255,)

    def row(self, y, x0, x1, ch):
        for x in range(x0, x1 + 1):
            self.put(y, x, ch)

    def pattern(self, y, x, text):
        for i, line in enumerate(text):
            for j, ch in enumerate(line):
                self.put(y + i, x + j, ch)


POT = ['..OOOOO..',     # 뚜껑 덮은 흙 단지(오른쪽 끝)
       '.OGHHHGO.',
       '.OAAAAAO.',
       'OFHHHGGEO',
       'OFHGGGEEO',
       'OFGGGEEBO',
       '.OGEEEBO.',
       '..OOOOO..']


def counter(stones):
    """계산대 84칸: 윗면을 평평하게 켠 통나무(윗면 TOP줄 · 앞 나무껍질 6줄, 끝은 둥글게) + 돌 받침(Codex) + 오른쪽 끝 흙 단지"""
    W = 84
    pal = dict(WOOD, G=(212, 176, 148), H=(228, 216, 192))
    pot = Canvas(len(POT), len(POT[0]), pal)
    pot.pattern(0, 0, POT)
    pot = pot.a
    log_h = 1 + TOP + 1 + 6 + 1
    top = pot.shape[0] - 2                                # 단지가 윗면 뒤쪽에 선다
    H = top + log_h + stones.shape[0] - 1
    c = Canvas(H, W, pal)
    sy = H - stones.shape[0]                              # 돌 받침(가운데 맞춤)
    sx = (W - stones.shape[1]) // 2
    c.a[sy:sy + stones.shape[0], sx:sx + stones.shape[1]][stones[..., 3] > 0] = stones[stones[..., 3] > 0]
    y, x0, x1 = top, 1, W - 2
    c.row(y, x0 + 2, x1 - 2, 'O')                         # 윗면: 켠 나무(밝음), 끝은 둥근 모서리
    for r in range(1, TOP + 1):
        c.row(y + r, x0, x1, 'E')
        c.put(y + r, x0, 'O')
        c.put(y + r, x1, 'O')
    c.put(y + 1, x0 + 1, 'O')
    c.put(y + 1, x1 - 1, 'O')
    c.row(y + 1, x0 + 4, x0 + 14, 'F')
    for dx0, dx1 in ((8, 26), (34, 50), (58, 70)):        # 켠 면의 결(길게)
        c.row(y + 1 + TOP // 2, x0 + dx0, x0 + dx1, 'D')
    for dx0, dx1 in ((18, 30), (44, 62)):
        c.row(y + TOP - 1, x0 + dx0, x0 + dx1, 'D')
    y += TOP + 1
    c.row(y, x0 + 1, x1 - 1, 'G')                         # 앞 모서리(켠 면과 껍질 사이) 빛
    c.put(y, x0, 'O')
    c.put(y, x1, 'O')
    for r in range(1, 7):                                 # 앞 나무껍질: 어둡고 세로 틈
        c.row(y + r, x0, x1, 'B' if r < 5 else 'A')
        c.put(y + r, x0, 'O')
        c.put(y + r, x1, 'O')
    for x in range(x0 + 6, x1 - 3, 9):
        c.put(y + 2, x, 'A')
        c.put(y + 3, x, 'A')
        c.put(y + 3, x + 1, 'O')
        c.put(y + 4, x + 1, 'A')
    c.row(y + 1, x0 + 2, x0 + 30, 'C')                    # 껍질 윗줄 왼쪽에 빛
    c.row(y + 7, x0 + 2, x1 - 2, 'O')                     # 밑(둥근 모서리)
    c.put(y + 6, x0, '.')
    c.put(y + 6, x1, '.')
    c.put(y + 6, x0 + 1, 'O')
    c.put(y + 6, x1 - 1, 'O')
    a = c.a
    px = W - pot.shape[1] - 3                             # 단지(오른쪽 끝, 윗면 뒤쪽)
    m = pot[..., 3] > 0
    a[0:pot.shape[0], px:px + pot.shape[1]][m] = pot[m]
    return outline(a)


def crate_shelf(tmp):
    """진열대 = 받침대 위 나무 빵 상자 하나(3-1). Codex는 상자 둘을 그렸는데 빵 아이콘이 하나라 두 상자 사이에 걸쳐서,
    상자 줄(0~19)의 가운데 칸막이를 지우고 상자 가운데 결(13열)을 이어 긴 상자 하나로 만든다. 받침대 · 다리는 그대로"""
    a = codex_prop(cut(Image.open(os.path.join(HERE, 'shop_raw', 'shelf_crate_raw.png')), 1)[0], 55, 8, tmp)
    b = a.copy()
    for y in range(20):
        b[y] = np.concatenate([a[y, :13], np.repeat(a[y, 13:14], 29, axis=0), a[y, 42:]])
    # 다리를 7줄 줄인다(사용자 「진열대 높이가 높다」): 앞 · 뒷다리가 같이 있는 27~29줄, 앞다리만 있는 33~36줄을 뺀다(뒷다리가 위에서 끝나는 깊이감은 남는다)
    return np.stack([b[y] for y in range(b.shape[0]) if y not in (27, 28, 29, 33, 34, 35, 36)])


def main():
    with tempfile.TemporaryDirectory() as tmp:
        c = cut(Image.open(os.path.join(HERE, 'shop_raw', 'props_c_sheet.png')), 4)
        for (name, cells, k), crop in zip((('oven', 60, 8), ('oven_2', 60, 9)), c[:2]):
            a = codex_prop(crop, cells, k, tmp)
            save_cells(a, os.path.join(SHOP, name + '.png'))
            print(name, a.shape[1], 'x', a.shape[0])
        c45 = cut(Image.open(os.path.join(HERE, 'shop_raw', 'props_c45_sheet.png')), 5)
        codex_counter = codex_prop(c45[3], 84, 9, tmp)
        loaf = codex_prop(c45[4], 20, 8, tmp)          # 식빵 가로 20칸(26칸은 「너무 크다」, 사용자)
        shelf = crate_shelf(tmp)
    # Codex 계산대에서는 돌 받침(밑 8줄)만 가져온다
    stones = codex_counter[-8:, :, :].copy()
    for name, a in (('shelf', shelf), ('counter', counter(stones))):
        save_cells(a, os.path.join(SHOP, name + '.png'))
        print(name, a.shape[1], 'x', a.shape[0])
    save_cells(loaf, BREAD)
    print('b01', loaf.shape[1], 'x', loaf.shape[0])


if __name__ == '__main__':
    main()
