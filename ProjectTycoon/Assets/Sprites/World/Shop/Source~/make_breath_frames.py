# -*- coding: utf-8 -*-
# 웜뱃 숨쉬기 프레임(칸 편집, AI 없음). 몸은 최대 1칸만 오르내리고 귀가 한 박자 늦게 따라온다(2026-09-18 A안).
#   0 기본
#   1 몸 1칸↓(발 바로 위 무늬 없는 줄을 뺌), 귀는 아직 제자리 → 귀가 1칸 길어 보임
#   2 몸·귀 1칸↓
#   3 몸 제자리, 귀는 아직 1칸↓ → 귀가 1칸 짧아 보임
# 재생 0 → 1 → 2 → 3, 숨 한 번 1.6초. 발·폭은 그대로. 이 폴더에서 실행하면 ../wombat_{front,back}_{1,2,3}.png 를 쓴다
import os
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))

# 칸(2px) 단위. low: 뺄 줄(위아래 폭 차이 ≤ 1칸이어야 외곽선이 안 끊긴다) · ear: 귀만 있는 마지막 행 · ears: 왼/오른 귀 열 범위
SPEC = {
    'front': {'low': 35, 'ear': 4, 'ears': [(5, 13), (28, 36)]},
    'back': {'low': 36, 'ear': 5, 'ears': [(4, 12), (29, 37)]},
}


def extent(row):
    xs = np.nonzero(row[:, 3])[0]
    return xs.min(), xs.max()


def remove_row(c, row):
    keep = [y for y in range(c.shape[0]) if y != row]
    o = np.zeros_like(c)
    o[1:] = c[keep]
    return o


def ears_at_base(dst, base, s):
    o = dst.copy()
    for x0, x1 in s['ears']:
        o[0:s['ear'] + 1, x0:x1 + 1] = base[0:s['ear'] + 1, x0:x1 + 1]
    return o


def ears_lowered(base, s):
    o = base.copy()
    for x0, x1 in s['ears']:
        o[1:s['ear'] + 2, x0:x1 + 1] = base[0:s['ear'] + 1, x0:x1 + 1]
        o[0, x0:x1 + 1] = 0
    return o


for name, s in SPEC.items():
    src = np.asarray(Image.open(f'../wombat_{name}.png').convert('RGBA'))
    base = src[::2, ::2]
    (a0, a1), (b0, b1) = extent(base[s['low'] - 1]), extent(base[s['low'] + 1])
    assert abs(a0 - b0) <= 1 and abs(a1 - b1) <= 1, (name, 'outline would break')
    down = remove_row(base, s['low'])
    frames = {1: ears_at_base(down, base, s), 2: down, 3: ears_lowered(base, s)}
    for i, f in frames.items():
        Image.fromarray(np.repeat(np.repeat(f, 2, 0), 2, 1)).save(f'../wombat_{name}_{i}.png')
    print(name, src.shape[1], 'x', src.shape[0])
