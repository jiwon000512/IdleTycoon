# -*- coding: utf-8 -*-
# 옆모습 시안 미리보기: make_breath_frames.py·make_walk_frames.py와 같은 칸 편집을, 발·귀·뺄 줄을 자동으로 재서 적용한다.
# 사용: preview_frames.py <격자 PNG(한 칸 2px)> <출력 접두어> '{"low":..,"foot":..,"left":[x0,x1],"right":[x0,x1],"ear":..,"ears":[[x0,x1]]}'  → <접두어>_idle.png(4칸 가로) · <접두어>_walk.png(4칸) · spec 출력
import sys
import numpy as np
from PIL import Image

src, prefix = sys.argv[1], sys.argv[2]
base = np.asarray(Image.open(src).convert('RGBA'))[::2, ::2].copy()
H, W = base.shape[:2]
alpha = base[..., 3] > 0


def runs(row):
    xs = np.nonzero(row)[0]
    out = []
    for x in xs:
        if out and x == out[-1][1] + 1:
            out[-1][1] = x
        else:
            out.append([x, x])
    return out


def extent(y):
    xs = np.nonzero(alpha[y])[0]
    return xs.min(), xs.max()


import json
spec = json.loads(sys.argv[3])  # 칸 지도를 보고 정한 값(make_walk_frames.py SPEC과 같은 뜻)
print(prefix, W, 'x', H, spec)


def remove_row(c, row):
    keep = [y for y in range(c.shape[0]) if y != row]
    o = np.zeros_like(c)
    o[1:] = c[keep]
    return o


# ---- 숨쉬기(make_breath_frames.py) ----
def ears_at_base(dst, b, s):
    o = dst.copy()
    for x0, x1 in s['ears']:
        o[0:s['ear'] + 1, x0:x1 + 1] = b[0:s['ear'] + 1, x0:x1 + 1]
    return o


def ears_lowered(b, s):
    o = b.copy()
    for x0, x1 in s['ears']:
        o[1:s['ear'] + 2, x0:x1 + 1] = b[0:s['ear'] + 1, x0:x1 + 1]
        o[0, x0:x1 + 1] = 0
    return o


down = remove_row(base, spec['low'])
idle = [base, ears_at_base(down, base, spec), down, ears_lowered(base, spec)]


# ---- 걷기(make_walk_frames.py) ----
def pad(c):
    o = np.zeros((c.shape[0], c.shape[1] + 2, 4), np.uint8)
    o[:, 1:-1] = c
    return o


def ears_at(dst, s_src, s, dy):
    o = dst.copy()
    e = s['ear']
    for x0, x1 in s['ears']:
        x0, x1 = x0 + 1, x1 + 1
        o[0:e + 1 + dy, x0:x1 + 1] = 0 if dy else o[0:e + 1, x0:x1 + 1]
        o[dy:e + 1 + dy, x0:x1 + 1] = s_src[0:e + 1, x0:x1 + 1]
    return o


def lift(c, s, side):
    o = c.copy()
    x0, x1 = s[side]
    x0, x1 = x0 + 1, x1 + 1
    y0 = s['foot']
    region = c[y0:, x0:x1 + 1].copy()
    o[y0:, x0:x1 + 1] = 0
    o[y0 - 1:c.shape[0] - 1, x0:x1 + 1] = np.where((region[..., 3] > 0)[..., None], region, o[y0 - 1:c.shape[0] - 1, x0:x1 + 1])
    dx = 1 if side == 'left' else -1
    e = s['ear']
    for ex0, ex1 in s['ears']:
        ex0, ex1 = ex0 + 1, ex1 + 1
        earr = c[0:e + 1, ex0:ex1 + 1].copy()
        o[0:e + 1, ex0:ex1 + 1] = 0
        o[0:e + 1, ex0 + dx:ex1 + 1 + dx] = np.where((earr[..., 3] > 0)[..., None], earr, o[0:e + 1, ex0 + dx:ex1 + 1 + dx])
    return o


pb = pad(base)
contact = ears_at(remove_row(pb, spec['low']), pb, spec, 0)
walk = [contact, lift(pb, spec, 'left'), contact, lift(pb, spec, 'right')]


def sheet(frames, name, cw):
    h = max(f.shape[0] for f in frames)
    o = np.zeros((h, cw * len(frames), 4), np.uint8)
    for i, f in enumerate(frames):
        x = i * cw + (cw - f.shape[1]) // 2
        o[h - f.shape[0]:, x:x + f.shape[1]] = f
    Image.fromarray(np.repeat(np.repeat(o, 2, 0), 2, 1)).save(name)


cw = W + 4
sheet(idle, prefix + '_idle.png', cw)
sheet(walk, prefix + '_walk.png', cw)
