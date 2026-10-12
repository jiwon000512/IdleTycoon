# -*- coding: utf-8 -*-
# 집 층 아래 댐 다섯 단계(설계 58, 2026-10-12 사용자 시안 C 「비버 댐」, 프롬프트 raw/beaver_dam_prompt.txt): 가는 나뭇가지를 엮고 진흙으로 메운 둔덕 + 앞 조약돌.
#   낚싯대로 한 조각씩 끌어내면 위 가지 묶음부터 하나씩 빠져 둔덕이 낮아진다(같은 바닥선). 버린 시안: A 지금 잔해 댐 + 두 단 · B 통나무 쌓기.
#   바탕 raw/beaver_dam_template.png(위 지금 잔해 댐 세 단계, 아래 빈 틀 다섯)에 그리게 한 raw/beaver_dam_c.png를 원본 그대로 옮긴다
#   (snap_codex raw 격자, 칸 크기 6.5px로 주고 · 색 합치기 없음). 큰 덩어리 다섯 + 물결 줄 조각은 가장 가까운 덩어리에 붙인다.
#   예전 잔해 댐(make_wide.py, dam_1~3)을 같은 이름으로 덮어쓴다(guid 그대로).
# 출력(한 칸 2px · PPU 80, 피벗 아래 가운데 = 물결 줄 아래): ../dam_1~5.png(1 = 마지막 한 조각 · 5 = 처음). 사용: python make_beaver_dam.py
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


def stages(path):
    cells, _ = snap_codex.snap(path, cell=6.5, square=True, min_hole=40, raw=True)
    on = cells[..., 3] > 0
    bl = blobs(on, diag=True)
    big = sorted([b for b in bl if len(b) > 300], key=len, reverse=True)[:5]
    big.sort(key=lambda b: (round(np.mean([y for y, _ in b]) / 40), np.mean([x for _, x in b])))   # 위 줄 5 · 4 · 3, 아래 줄 2 · 1
    centers = [(np.mean([y for y, _ in b]), np.mean([x for _, x in b])) for b in big]
    groups = [list(b) for b in big]
    for b in bl:
        if any(b is x for x in big):
            continue
        my, mx = np.mean([y for y, _ in b]), np.mean([x for _, x in b])
        groups[int(np.argmin([(my - cy) ** 2 + (mx - cx) ** 2 for cy, cx in centers]))] += b
    for gr in groups:
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = cells[y, x]
        yield ic


if __name__ == '__main__':
    for i, ic in enumerate(stages(os.path.join(HERE, 'raw', 'beaver_dam_c.png'))):
        Image.fromarray(np.repeat(np.repeat(ic, 2, 0), 2, 1)).save(os.path.join(HERE, '..', 'dam_%d.png' % (5 - i)))
        print('dam_%d.png %dx%d칸' % (5 - i, ic.shape[1], ic.shape[0]))
