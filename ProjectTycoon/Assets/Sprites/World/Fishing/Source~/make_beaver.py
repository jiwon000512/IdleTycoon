# -*- coding: utf-8 -*-
# 낚시터 비버(설계 60, 2026-10-12 사용자 앞모습 시안 B 「공사장 비버」, 프롬프트 raw/beaver_prompt.txt): 층마다 물 오른쪽 둑에 앉아 물고기를 받는다.
#   노란 안전모 + 공구 띠, 앞을 보고 앉고 넓적 꼬리가 오른쪽 뒤로. 버린 시안: A 그냥 비버 · C 나무꾼 비버(옆모습 셋은 보류).
#   raw/beaver_b.png 한 장의 왼쪽 앉기 · 오른쪽 냠(물고기를 물고 눈 감음)을 반씩 나눠 원본 그대로 옮긴다(snap_codex raw, 주기는 둘의 중앙값 하나).
#   두 장은 오른쪽 · 아래(꼬리 끝 · 발끝) 맞춤으로 같은 캔버스(42×44칸)라 그대로 바꿔 끼운다.
# 출력: ../beaver.png · ../beaver_eat.png, PPU 40 · 피벗 아래 가운데. 사용: python make_beaver.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402


def trim(c):
    ys, xs = np.nonzero(c[..., 3])
    return c[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


if __name__ == '__main__':
    im = Image.open(os.path.join(HERE, 'raw', 'beaver_b.png'))
    paths = []
    for i in range(2):
        p = os.path.join(HERE, 'raw', '_half%d.png' % i)
        im.crop((i * im.width // 2, 0, (i + 1) * im.width // 2, im.height)).save(p)
        paths.append(p)
    cell = float(np.median([snap_codex.snap(p, square=True, min_hole=40, raw=True)[1][0] for p in paths]))
    cs = [trim(snap_codex.snap(p, cell=cell, square=True, min_hole=40, raw=True)[0]) for p in paths]
    for p in paths:
        os.remove(p)
    H = max(c.shape[0] for c in cs); W = max(c.shape[1] for c in cs)
    for name, c in zip(('beaver', 'beaver_eat'), cs):
        out = np.zeros((H, W, 4), np.uint8)
        out[H - c.shape[0]:, W - c.shape[1]:] = c
        Image.fromarray(out).save(os.path.join(HERE, '..', name + '.png'))
        print('%s.png %d x %d' % (name, W, H))
