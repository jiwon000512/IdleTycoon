# 횟집 회 접시 셋(2026-10-06 사용자 선택 B 「작은 나무 판」, 프롬프트 raw/prompt_dish.txt).
#   처음 고른 시안 raw/dish_b.png(36~41칸)는 게임에서 작아 1.5배로 키웠는데, 1.5는 정수 배가 아니라 칸이 1px · 2px로 번갈아 깨졌다
#   → 같은 디자인을 1.5배 칸 수로 다시 그렸다(사용자 선택 「새 접시 1」): raw/dish15_<어종>.png, 접시 하나에 한 장.
#   원본 그대로 옮긴다: 원본 픽셀 하나 = 텍셀 하나, 색 그대로(격자는 snap_codex가 정사각으로 찾음). 가장 큰 덩어리만 남기고
#   바깥 번짐은 make_tank.clear_fringe, 둘레 빈 칸은 자른다.
# 출력: Resources/Sprites/Dishes/dish_<minnow|crucian|catfish>.png(피라미 60×30 · 붕어 59×36 · 메기 66×38, PPU 80, 피벗 밑변 가운데, 1배로 놓음).
#   다른 폴더에 쓰려면 인자로 폴더
# 사용: python make_dishes.py [출력 폴더]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
sys.path.insert(0, HERE)
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402
from make_tank import clear_fringe  # noqa: E402

NAMES = ('minnow', 'crucian', 'catfish')

if __name__ == '__main__':
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Dishes')
    for n in NAMES:
        a, g = snap_codex.snap(os.path.join(HERE, 'raw', 'dish15_%s.png' % n), raw=True, square=True)
        keep = np.zeros(a.shape[:2], bool)
        for y, x in max(blobs(a[..., 3] > 0, diag=True), key=len):
            keep[y, x] = True
        a[~keep] = 0
        a = clear_fringe(a)
        ys, xs = np.nonzero(a[..., 3])
        a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        Image.fromarray(a, 'RGBA').save(os.path.join(out_dir, 'dish_%s.png' % n))
        print('dish_%s.png %d x %d px (격자 %.2fpx)' % (n, a.shape[1], a.shape[0], g[0]))
