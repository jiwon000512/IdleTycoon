# 횟집 회 뜨기 단계(2026-10-06 사용자 선택 C 「뼈만 남기기」, 프롬프트 raw/prompt_cut.txt).
#   도마 위 물고기가 뜨기 진행률에 따라 통째(0) → 앞쪽이 회가 된 반쯤(1) → 회 세 점 + 생선 뼈(2)로 바뀐다. 어종마다 한 장에 세 단계를 상자 셋에 그렸다.
#   원본 그대로 옮긴다: 원본 픽셀 하나 = 텍셀 하나(창고 아이콘 · 접시와 같은 칸), 색 그대로. 한 장의 그림이라 주기는 셋의 중앙값 하나,
#   시작점만 단계마다 찾는다(make_tanuki 방식). 회 · 뼈가 따로 떨어진 덩어리라 가장 큰 덩어리만 남기지 않고 8칸 아래 점만 지운다. 바깥 번짐은 make_tank.clear_fringe.
#   피라미 시안은 통째만 머리가 오른쪽이라 좌우를 뒤집어 세 단계 모두 머리를 왼쪽으로 맞춘다(FLIP).
# 출력: Resources/Sprites/Dishes/dish_<어종>_cut_<0|1|2>.png(PPU 80, 피벗 밑변 가운데, 도마 피벗 위 0.62에 1배). 다른 폴더에 쓰려면 인자로 폴더
# 사용: python make_cut.py [출력 폴더]
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
FLIP = {('minnow', 0)}


def snap_fixed(path, period, span=0.01):
    g = np.asarray(Image.open(path).convert('RGB')).astype(float).mean(2)
    px, phx = snap_codex.grid(np.abs(np.diff(g, axis=1)).sum(0), period, span)
    py, phy = snap_codex.grid(np.abs(np.diff(g, axis=0)).sum(1), period, span)
    return snap_codex.snap(path, raw=True, fixed=(px, phx, py, phy))[0]


if __name__ == '__main__':
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Dishes')
    for n in NAMES:
        sheet = Image.open(os.path.join(HERE, 'raw', 'cut_%s.png' % n)).convert('RGB')
        paths = []
        for i in range(3):
            p = os.path.join(HERE, 'raw', '_cut_%s_%d.png' % (n, i))
            sheet.crop((20 + 490 * i, 330, 500 + 490 * i, 680)).save(p)   # 상자 하나(바탕 tpl: x 60 + 490i, 384 × 160) + 둘레 여백
            paths.append(p)
        period = float(np.median([snap_codex.snap(p, raw=True, square=True)[1][0] for p in paths]))
        for i, p in enumerate(paths):
            a = clear_fringe(snap_fixed(p, period))
            os.remove(p)
            for blob in blobs(a[..., 3] > 0, diag=True):   # 떨어진 점 · 짧은 선(8칸 아래)만 지운다. 회 · 뼈 덩어리는 그대로
                if len(blob) < 8:
                    for y, x in blob:
                        a[y, x] = 0
            ys, xs = np.nonzero(a[..., 3])
            a = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
            if (n, i) in FLIP:
                a = a[:, ::-1]
            Image.fromarray(np.ascontiguousarray(a), 'RGBA').save(os.path.join(out_dir, 'dish_%s_cut_%d.png' % (n, i)))
            print('dish_%s_cut_%d.png %d x %d px (격자 %.2fpx)' % (n, i, a.shape[1], a.shape[0], period))
