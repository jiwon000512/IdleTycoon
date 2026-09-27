# -*- coding: utf-8 -*-
# 공용 칩 chip(평소)·chip_selected(선택)(2026-09-27, Codex 시안 C 「들어간 칸」, UI 작업 규칙 v1.0): raw/chip_c.png의 픽셀 격자를 재서
# 한 칸 = 1px로 줄이고, 바깥에서 이어진 흰 바탕만 투명으로(안쪽 밝은 선은 남긴다), 비슷한 색을 한 색으로 모아 두 상태를 따로 쓴다.
# 9-slice 경계 6, PPU 25(1칸 4px). 사물 시트 칩·편집 모드 카드·점원 탭이 쓴다.
# 사용: make_chip.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
src = np.asarray(Image.open('raw/chip_c.png').convert('RGB')).astype(int)


def edges(axis):
    d = np.abs(np.diff(src, axis=axis)).sum(2)
    return (d > 40).sum(axis=1 - axis).astype(float)


def period(axis):
    e = edges(axis) - edges(axis).mean()
    f = np.abs(np.fft.rfft(e)); fr = np.fft.rfftfreq(len(e))
    m = (fr > 1 / 30) & (fr < 1 / 4)
    return 1 / fr[m][np.argmax(f[m])]


def phase(axis, p):
    e = edges(axis)
    best = None
    for off in np.arange(0, p, 0.1):
        score = sum(e[i] for i in (int(round(x)) for x in np.arange(off, len(e), p)) if i < len(e))
        if best is None or score > best[0]:
            best = (score, off)
    return best[1]


p = (period(1) + period(0)) / 2
ox, oy = phase(1, p), phase(0, p)
xs = np.arange(ox + 1, src.shape[1] - p, p)
ys = np.arange(oy + 1, src.shape[0] - p, p)
a = np.zeros((len(ys), len(xs), 4), np.uint8)
for j, y in enumerate(ys):
    for i, x in enumerate(xs):
        a[j, i, :3] = np.median(src[int(y + p * 0.3):int(y + p * 0.7) + 1, int(x + p * 0.3):int(x + p * 0.7) + 1].reshape(-1, 3), axis=0)
a[..., 3] = 255

# 바깥 가장자리에서 이어진 흰 칸만 투명으로
white = a[..., :3].astype(int).min(2) > 235
h, w = white.shape
seen = np.zeros_like(white)
q = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
while q:
    y, x = q.pop()
    if 0 <= y < h and 0 <= x < w and white[y, x] and not seen[y, x]:
        seen[y, x] = True
        q += [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
a[seen] = 0


def quantize(part):
    rgb = part[..., :3].astype(float); on = part[..., 3] > 0
    centers = []
    for c in rgb[on]:
        for k in centers:
            if np.sqrt(((k[0] - c) ** 2).sum()) < 28:
                k[1].append(c); break
        else:
            centers.append([c, [c]])
    centers = [np.mean(k[1], axis=0) for k in centers]
    for idx in zip(*np.nonzero(on)):
        part[idx][:3] = np.round(min(centers, key=lambda k: ((k - rgb[idx]) ** 2).sum()))
    return part


# 왼쪽 = 평소, 오른쪽 = 선택(빈 열로 나뉜다)
cols = np.nonzero(a[..., 3].any(0))[0]
split = [i for i in range(1, len(cols)) if cols[i] != cols[i - 1] + 1][0]
slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
for name, (x0, x1) in (('chip', (cols[0], cols[split - 1])), ('chip_selected', (cols[split], cols[-1]))):
    rows = np.nonzero(a[:, x0:x1 + 1, 3].any(1))[0]
    part = quantize(a[rows[0]:rows[-1] + 1, x0:x1 + 1].copy())
    Image.fromarray(part).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(part.shape[1]), 'h': int(part.shape[0]), 'border': [6, 6, 6, 6]}
    print('%s %d x %d' % (name, part.shape[1], part.shape[0]))
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('grid %.2f' % p)
