# 설계 24 웜뱃 똥: 앞쪽 위에서 내려다본 네모 똥 한 덩이 + 위로 오르는 냄새 김 2프레임. 한 칸 2px · PPU 80, 피벗 아래 가운데(BakeryBaker).
# 2026-10-01 사용자 선택 A(한 덩이): Codex 시안 shop_raw/poop_a~c.png(프롬프트 shop_raw/poop_prompt.txt, 거름 아이콘과 같은 결)을
#   원본 격자 그대로 옮긴다(World/Source~/snap_codex.py, 정사각형 격자). 15×14칸. 김은 칸 무늬로 그려 물체 위 가운데에 얹는다.
# 출력 ../poop_0.png · ../poop_1.png(김만 다르다). 실행: Windows Python(Pillow · numpy) make_poop.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
RAW = os.path.join(HERE, 'shop_raw', 'poop_a.png')
PX = 2
STINK_COLOR = (150, 164, 112)
STINK = [
    [
        "...g....g...",
        "..g....g....",
        "...g....g...",
        "..g....g....",
        "............",
    ],
    [
        "..g....g....",
        "...g....g...",
        "..g....g....",
        "...g....g...",
        "............",
    ],
]
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402


def with_stink(cells, stink):
    sh, sw = len(stink), len(stink[0])
    h, w = cells.shape[:2]
    width = max(w, sw)
    out = np.zeros((sh + h, width, 4), np.uint8)
    out[sh:, (width - w) // 2:(width - w) // 2 + w] = cells
    left = (width - sw) // 2
    for y, row in enumerate(stink):
        for x, ch in enumerate(row):
            if ch == 'g':
                out[y, left + x] = STINK_COLOR + (255,)
    return out


if __name__ == '__main__':
    cells, _ = snap_codex.snap(RAW, square=True, raw=True)
    for i, stink in enumerate(STINK):
        a = with_stink(cells, stink)
        Image.fromarray(np.repeat(np.repeat(a, PX, 0), PX, 1)).save(os.path.join(OUT, 'poop_%d.png' % i))
        print('poop_%d' % i, 'cells', a.shape[1], 'x', a.shape[0])
