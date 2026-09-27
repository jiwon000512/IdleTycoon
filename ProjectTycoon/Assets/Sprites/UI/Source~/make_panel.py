# -*- coding: utf-8 -*-
# 공용 팝업 틀 panel(2026-09-27, Codex 시안 A 「깔끔한 주황」, UI 작업 규칙 v1.0): raw/panel_a.png의 픽셀 격자를 재서
# 한 칸 = 1px로 줄이고 흰 바탕을 투명으로, 비슷한 색을 한 색으로 모아 ../panel.png(9-slice 경계 8, PPU 25 = 1칸 4px)에 쓴다.
# 사용: make_panel.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
src = np.asarray(Image.open('raw/panel_a.png').convert('RGB')).astype(int)


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
        c = np.median(src[int(y + p * 0.3):int(y + p * 0.7) + 1, int(x + p * 0.3):int(x + p * 0.7) + 1].reshape(-1, 3), axis=0)
        a[j, i, :3] = c
        a[j, i, 3] = 0 if c.min() > 235 else 255
ys_, xs_ = np.nonzero(a[..., 3])
a = a[ys_.min():ys_.max() + 1, xs_.min():xs_.max() + 1]

# 비슷한 색(거리 28 안)을 한 색으로
rgb = a[..., :3].astype(float); on = a[..., 3] > 0
centers = []
for c in rgb[on]:
    for k in centers:
        if np.sqrt(((k[0] - c) ** 2).sum()) < 28:
            k[1].append(c); break
    else:
        centers.append([c, [c]])
centers = [np.mean(k[1], axis=0) for k in centers]
for idx in zip(*np.nonzero(on)):
    a[idx][:3] = np.round(min(centers, key=lambda k: ((k - rgb[idx]) ** 2).sum()))

Image.fromarray(a).save(f'{OUT}panel.png')
slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
slices['panel'] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': [8, 8, 8, 8]}
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('grid %.2f panel %d x %d colors %d' % (p, a.shape[1], a.shape[0], len(centers)))
