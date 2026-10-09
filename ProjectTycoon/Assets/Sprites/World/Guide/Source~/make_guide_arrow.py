# -*- coding: utf-8 -*-
# 길잡이 화살표(설계 54, 2026-10-09 사용자 선택 A 금빛 세모). AI 없이 칸 무늬(한 칸 = 2px, PPU 80)로 그린다.
# 아래를 가리키는 세모 19×21칸(38×42px = 높이 0.525유닛), 코인 팔레트, 진갈색 외곽선 1칸. 옆선 2:1 · 위 두 귀 둥글게 · 위 흰 한 획 · 왼쪽 밝은 선 · 오른쪽 그늘.
# 폭이 홀수라 끝 칸 가운데 = 그림 가운데 → 피벗 아래 가운데(0.5, 0) = 화살 끝. GuideView가 90도 단위로만 돌린다.
# 버린 시안: B 주황 물방울 핀 · C 크림 겹쉐브론(2026-10-08 비교).
# 사용: python make_guide_arrow.py → ../guide_arrow.png
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
INK = (52, 32, 32)
GOLD = {'h': (255, 244, 200), 'l': (248, 216, 120), 'm': (232, 176, 72), 's': (192, 128, 56)}
ROWS = [
    '..###############..',
    '.#lhhhhhlllllllll#.',
    '#llmmmmmmmmmmmmmms#',
    '#lmmmmmmmmmmmmmmss#',
    '.#lmmmmmmmmmmmmss#.',
    '.#lmmmmmmmmmmmmss#.',
    '..#lmmmmmmmmmmss#..',
    '..#lmmmmmmmmmmss#..',
    '...#lmmmmmmmmss#...',
    '...#lmmmmmmmmss#...',
    '....#lmmmmmmss#....',
    '....#lmmmmmmss#....',
    '.....#lmmmmss#.....',
    '.....#lmmmmss#.....',
    '......#lmmss#......',
    '......#lmmss#......',
    '.......#lms#.......',
    '.......#lms#.......',
    '........#s#........',
    '........#s#........',
    '.........#.........',
]


def grid(rows, pal):
    w = len(rows[0])
    assert all(len(r) == w for r in rows), [len(r) for r in rows]
    a = np.zeros((len(rows), w, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                a[y, x, :3] = INK if ch == '#' else pal[ch]
                a[y, x, 3] = 255
    return a


if __name__ == '__main__':
    a = grid(ROWS, GOLD)
    sil = a[..., 3] > 0
    w = a.shape[1]
    assert (sil == sil[:, ::-1]).all(), 'not symmetric'
    assert w % 2 == 1 and sil[-1].nonzero()[0].tolist() == [w // 2], 'tip not centered'
    out = os.path.join(HERE, '..', 'guide_arrow.png')
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(out)
    print('guide_arrow.png %dx%d칸 = %dx%dpx · 높이 %.3f유닛' % (w, a.shape[0], w * 2, a.shape[0] * 2, a.shape[0] * 2 / 80))
