# 빵집 입구 기본 장식(2026-10-01 사용자 선택): 농장처럼 입구 아치 양옆 벽 밑에 넷. 왼쪽 밀 다발 통(바깥) · 웜뱃 제빵사(아치 옆),
#   오른쪽 칠판(아치 옆) · 빵 식힘대(바깥). 원본은 shop_raw/bakery_props_a~c.png(Codex 시트 셋, 프롬프트 shop_raw/bakery_props_prompt.txt)에서 하나씩 골랐다.
# 칸으로 옮기기: 시트 하나를 한 격자로(World/Source~/snap_codex.py, 정사각 칸) 옮긴 뒤 빈 열로 물체를 나눠 자르고 물체마다 12색 안으로 합친다(한 시트 같은 주기).
#   흰 하이라이트는 남기고 큰 흰 틈만 뚫는다(min_hole 40). 손질 없음.
# 출력: ../bakery_prop_<이름>.png(한 칸 2px · PPU 80, 피벗 아래 가운데). 실행: Windows Python(Pillow · numpy) make_bakery_props.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'shop_raw')
OUT = os.path.join(HERE, '..')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2
# (이름, 시트, 시트 안 몇 번째 물체(왼쪽부터))
PROPS = [('wheat_barrel', 'a', 1), ('baker', 'b', 0), ('chalkboard', 'c', 0), ('bread_rack', 'c', 1)]


def split(cells, gap=2):
    # 빈 열(gap칸 넘게)로 물체를 나눈다. gap칸 안 틈은 한 물체로 합치고, 6칸보다 좁게 떨어진 조각은 버린다
    ink = (cells[..., 3] > 0).any(0)
    runs, start = [], None
    for x, on in enumerate(ink):
        if on and start is None:
            start = x
        if not on and start is not None:
            runs.append([start, x])
            start = None
    if start is not None:
        runs.append([start, len(ink)])
    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] <= gap:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    out = []
    for x0, x1 in merged:
        sub = cells[:, x0:x1]
        ys = np.nonzero((sub[..., 3] > 0).any(1))[0]
        if x1 - x0 >= 6:
            out.append(sub[ys.min():ys.max() + 1])
    return out


def sheet(v):
    cells, _ = snap_codex.snap(os.path.join(RAW, 'bakery_props_%s.png' % v), 256, square=True, min_hole=40)
    return split(cells)


if __name__ == '__main__':
    sheets = {}
    for name, v, i in PROPS:
        if v not in sheets:
            sheets[v] = sheet(v)
        o = snap_codex.merge_colors(sheets[v][i].copy(), sheets[v][i][..., 3] > 0, 12)
        path = os.path.join(OUT, 'bakery_prop_%s.png' % name)
        Image.fromarray(np.repeat(np.repeat(o, PX, axis=0), PX, axis=1)).save(path)
        print(os.path.relpath(path, HERE), 'cells', o.shape[1], 'x', o.shape[0], 'colors', len(np.unique(o[o[..., 3] > 0][:, :3], axis=0)))
