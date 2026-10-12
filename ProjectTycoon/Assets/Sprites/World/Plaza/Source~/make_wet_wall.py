# -*- coding: utf-8 -*-
# 광장 「젖은 벽」(설계 58, 2026-10-12 사용자 시안 B 「돌 무너짐」, 프롬프트 raw/wet_wall_prompt.txt): 낚시터 문 아치 자리.
#   닫힘 = 아치 안을 둥근 돌무더기가 막고 돌 틈으로 물이 새어 웅덩이 · 열림 = 돌이 양옆 두 무더기로 비키고 가운데로 물줄기.
#   바탕 raw/wet_wall_template.png(위 지금 아치, 아래 빈 틀 둘)에 그리게 한 raw/wet_wall_b.png를 원본 그대로 옮긴다(snap_codex raw 격자, 색 합치기 없음).
#   시트 맨 위에 다시 그린 지금 아치는 버리고 아래 두 덩어리(왼쪽 닫힘 · 오른쪽 열림)만 쓴다. 버린 시안: A 진흙 막힘 · C 판자 막이.
#   Codex가 한 칸 2px로 60×49칸을 그려 PPU 40이면 지금 arch(120×98, PPU 80)와 같은 크기 · 같은 아치 틀.
# 출력: ../wet_wall.png(닫힘) · ../wet_wall_open.png(열림), PPU 40 · 피벗 아래 가운데(아치 발끝). 사용: python make_wet_wall.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
WORLD = os.path.join(HERE, '..', '..')
sys.path.insert(0, os.path.join(WORLD, 'Source~'))
sys.path.insert(0, os.path.join(WORLD, 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402


def cut(path):
    cells, _ = snap_codex.snap(path, square=True, min_hole=40, raw=True)
    on = cells[..., 3] > 0
    # 큰 덩어리를 줄(위 · 아래) → 왼쪽부터 순서로, 아래 줄 둘
    groups = sorted([gr for gr in blobs(on, diag=True) if len(gr) > 400], key=lambda gr: (np.mean([y for y, _ in gr]) // 50, np.mean([x for _, x in gr])))
    for gr in groups[-2:]:
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = cells[y, x]
        yield ic


if __name__ == '__main__':
    for name, ic in zip(('wet_wall', 'wet_wall_open'), cut(os.path.join(HERE, 'raw', 'wet_wall_b.png'))):
        Image.fromarray(ic).save(os.path.join(HERE, '..', name + '.png'))
        print('%s.png %d x %d' % (name, ic.shape[1], ic.shape[0]))
