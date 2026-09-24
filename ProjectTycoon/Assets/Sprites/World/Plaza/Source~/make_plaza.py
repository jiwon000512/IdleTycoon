# -*- coding: utf-8 -*-
# 설계 11 광장 실제 아트(2026-09-24): Codex 스프라이트 시트 → 칸 격자(한 칸 = 2px, PPU 80) → 색 합치기 → 제자리 저장
#   raw/plaza_sheet_a.png  장식 A 소박한 흙·나무 (2×2: 분수·벤치 / 화분·가로등)
#   raw/plaza_sheet_b.png  장식 B 아기자기한 상가 거리 (2×2: 분수·벤치 / 꽃 상자·가로등)
#   raw/plaza_sheet_f.png  고정 부품 B 결 (위: 차양 / 아래: 간판·계단). Codex가 차양 위에 판을 붙여 그려서 판 줄은 잘라 낸다
#   프롬프트 raw/plaza_sheet_prompt_{a,b,f}.txt, 콘셉트 raw/plaza_concept_{a,b}.png
# 분수는 물결 4프레임(<이름>_0~3, 바탕 = _0): 수면 바탕색 칸에만 가운데에서 바깥으로 퍼지는 고리, 물줄기·반짝임은 그대로
# 폭(칸)은 DecorationTable.json halfWidth·depth와 맞춘 더미 크기를 따른다. 사용: make_plaza.py [<Assets 폴더>]
import os, subprocess, sys, tempfile
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', '..', '..', '..')
MAKE_PIXEL = os.path.join(HERE, '..', '..', 'Source~', 'make_pixel.py')
TMP = tempfile.mkdtemp()
DECOR = 'Resources/Sprites/Decor/'
PLAZA = 'Sprites/World/Plaza/'
# 시트별 (사분면 → 경로, 폭 칸, 색 수). 사분면: tl·tr·bl·br, 고정 부품은 top·bl·br
SHEETS = {
    'a': {'tl': (DECOR + 'fountain_stone', 76, 8), 'tr': (DECOR + 'bench_log', 64, 8), 'bl': (DECOR + 'plant_pot', 30, 8), 'br': (DECOR + 'lamp_lantern', 26, 8)},
    'b': {'tl': (DECOR + 'fountain_tile', 76, 8), 'tr': (DECOR + 'bench_iron', 64, 8), 'bl': (DECOR + 'plant_box', 40, 10), 'br': (DECOR + 'lamp_globe', 16, 8)},
    'f': {'top': (PLAZA + 'awning', 68, 8), 'bl': (PLAZA + 'sign', 40, 8), 'br': (PLAZA + 'stairs', 48, 8)},
}
RING = {'fountain_stone': (108, 180, 216), 'fountain_tile': (156, 204, 224)}


def blobs(im):
    # 흰 바탕 위 덩어리(4배 줄인 격자에서 8방향 연결)
    ink = np.asarray(im).astype(int).sum(2) < 735
    s = 4
    h, w = ink.shape[0] // s, ink.shape[1] // s
    m = ink[:h * s, :w * s].reshape(h, s, w, s).any(axis=(1, 3))
    seen = np.zeros_like(m)
    out = []
    for y0 in range(h):
        for x0 in range(w):
            if not m[y0, x0] or seen[y0, x0]:
                continue
            stack, ys, xs = [(y0, x0)], [], []
            seen[y0, x0] = True
            while stack:
                y, x = stack.pop()
                ys.append(y); xs.append(x)
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))
            if len(ys) > 40:
                out.append((min(xs) * s, min(ys) * s, (max(xs) + 1) * s, (max(ys) + 1) * s))
    return out


def quadrant(box, size, three):
    cx, cy = (box[0] + box[2]) / 2 / size[0], (box[1] + box[3]) / 2 / size[1]
    if three and cy < 0.4:
        return 'top'
    return ('t' if cy < 0.5 else 'b') + ('l' if cx < 0.5 else 'r')


def pixel(im, box, width):
    pad = (max(box[0] - 6, 0), max(box[1] - 6, 0), box[2] + 6, box[3] + 6)
    src, dst = os.path.join(TMP, 'src.png'), os.path.join(TMP, 'dst.png')
    im.crop(pad).save(src)
    subprocess.run([sys.executable, MAKE_PIXEL, src, dst, '--px=1', '--cellsw=%d' % width, '--th=0.001'], check=True, stdout=subprocess.DEVNULL)
    a = np.asarray(Image.open(dst).convert('RGBA')).copy()
    a[..., 3] = np.where(a[..., 3] > 0, 255, 0)
    return a


def reduce_close(a, n):
    # 가장 가까운 두 색을 합친다(적은 쪽 → 많은 쪽). 드문 강조색(꽃·불빛)이 살아남는다
    op = a[..., 3] > 0
    while True:
        cols, cnt = np.unique(a[op][:, :3], axis=0, return_counts=True)
        if len(cols) <= n:
            return a
        c = cols.astype(int)
        d = ((c[:, None] - c[None]) ** 2).sum(2) + np.eye(len(c)) * 1e9
        i, j = np.unravel_index(np.argmin(d), d.shape)
        src, dst = (i, j) if cnt[i] < cnt[j] else (j, i)
        a[op & (a[..., :3] == cols[src]).all(2), :3] = cols[dst]


def trim_board(a):
    # 차양 위에 붙은 나무판(갈색: 40 < r−g < 90)이 나오는 위쪽 절반의 마지막 줄까지 지우고, 새 윗줄을 외곽선으로 닫는다
    c = a[..., :3].astype(int)
    board = (a[..., 3] > 0) & (c[..., 0] - c[..., 1] > 40) & (c[..., 0] - c[..., 1] < 90) & (c[..., 0] < 200)
    rows = np.where(board[:a.shape[0] // 2].any(1))[0]
    a = a[rows[-1] + 1:].copy()
    a[0, a[0, :, 3] > 0, :3] = (48, 24, 24)
    return a


def ripple_frames(a, ring):
    # 수면 바탕색(가장 많은 파랑)의 칸에만 고리. 파랑 칸의 덩어리마다 중심·반지름을 따로 잡는다
    blue = (a[..., 2] > a[..., 0] + 20) & (a[..., 3] > 0)
    cols, cnt = np.unique(a[blue][:, :3], axis=0, return_counts=True)
    base = cols[np.argmax(cnt)]
    water = blue & (a[..., :3] == base).all(2)
    frames = [a.copy() for _ in range(4)]
    seen = np.zeros_like(water)
    H, W = water.shape
    for y0, x0 in zip(*np.where(water)):
        if seen[y0, x0]:
            continue
        stack, pts = [(y0, x0)], []
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            pts.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < H and 0 <= nx < W and water[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(pts) < 30:
            continue
        ys, xs = np.array(pts).T
        cy, cx = ys.mean(), xs.mean()
        ry, rx = max((ys.max() - ys.min()) / 2, 1), max((xs.max() - xs.min()) / 2, 1)
        d = np.hypot((xs - cx) / rx, (ys - cy) / ry)
        for k in range(4):
            phase = ((d - k * 0.085) / 0.34) % 1
            on = (phase < 0.2) & (d > 0.3) & (d < 0.95)
            frames[k][ys[on], xs[on], :3] = ring
    return frames


def save(a, rel):
    path = os.path.join(ASSETS, rel + '.png')
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(path)
    return path


for key, items in SHEETS.items():
    im = Image.open(os.path.join(HERE, 'raw', 'plaza_sheet_%s.png' % key)).convert('RGB')
    for box in blobs(im):
        q = quadrant(box, im.size, key == 'f')
        rel, width, colors = items[q]
        a = pixel(im, box, width)
        if rel.endswith('awning'):
            a = trim_board(a)
        a = reduce_close(a, colors)
        name = os.path.basename(rel)
        if name in RING:
            frames = ripple_frames(a, RING[name])
            for k, f in enumerate(frames):
                save(f, '%s_%d' % (rel, k))
            a = frames[0]
        save(a, rel)
        print(name, a.shape[1], 'x', a.shape[0], 'cells')
