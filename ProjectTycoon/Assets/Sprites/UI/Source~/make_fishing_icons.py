# 낚시 행동 아이콘 다섯(2026-10-05 사용자 선택 A 「물건 하나」, 프롬프트 raw/fishing_icons_prompt.txt).
#   소환 = 리본 상자에서 삐죽 나온 대 · 합치기 = 밧줄로 묶은 대 셋 · 대 들기 = 대 손잡이를 쥔 앞발 · 털썩 = 갈고리에 걸린 큰 물고기 · 쿵 = 갈라지는 땅 + 충격 고리.
#   바탕 raw/fishing_icons_template.png(한 칸 12px, 윗줄 지금 아이콘 18×18칸)의 아랫줄 빈 칸 다섯에 그리게 해 지금 아이콘과 같은 픽셀 크기로 나왔다.
#   원본 그대로 옮긴다(snap_codex raw 격자, 칸 가운데 중앙값 그대로, 색 합치기 없음). 그림이 칸 밖으로 조금 나와(흙먼지 · 고리)
#   덩어리마다 가운데가 가장 가까운 칸에 붙여 자른다 → 16~22칸. 버튼 안에서는 그림 크기 × 4로 둔다(줄이지 않음).
# 출력: Resources/Sprites/Actions/<summon|merge|carry|haul|thump>.png(원본 픽셀 하나 = UI 1px) → 메뉴 ZooTycoon/Bake/Import UI Sprites
# 사용: python make_fishing_icons.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
WORLD = os.path.join(HERE, '..', '..', 'World')
sys.path.insert(0, os.path.join(WORLD, 'Source~'))
sys.path.insert(0, os.path.join(WORLD, 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

OUT = os.path.join(HERE, '..', '..', '..', 'Resources', 'Sprites', 'Actions')
NAMES = ['summon', 'merge', 'carry', 'haul', 'thump']
P, N, X0, GAP, BOT = 12, 18, 78, 60, 600       # 바탕(raw/fishing_icons_template.png) 한 칸 · 아이콘 칸 수 · 아랫줄 칸 자리


def slot(i):
    return X0 + i * (N * P + GAP)


def icons(path):
    _, (px, phx, py, phy) = snap_codex.snap(path, cell=P, square=True, min_hole=40, raw=True)
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    h, w = a.shape[:2]
    xs, ys = np.arange(phx + 1, w, px), np.arange(phy + 1, h, py)
    cj = [k for k in range(len(ys) - 1) if BOT - 5 * P <= ys[k] and ys[k + 1] <= BOT + (N + 5) * P]
    c = np.zeros((len(cj), len(xs) - 1, 4), np.uint8)
    for jj, j in enumerate(cj):
        for k in range(len(xs) - 1):
            y0, y1, x0, x1 = ys[j], ys[j + 1], xs[k], xs[k + 1]
            col = np.median(a[int(y0 + (y1 - y0) * 0.25):int(y1 - (y1 - y0) * 0.25) + 1, int(x0 + (x1 - x0) * 0.25):int(x1 - (x1 - x0) * 0.25) + 1].reshape(-1, 3), 0).round()
            c[jj, k, :3] = col
            frame = col.max() - col.min() < 12 and col.min() > 170       # 회색 틀 자국
            c[jj, k, 3] = 0 if (col.min() > 238 or frame) else 255
    centers = [(slot(i) + N * P / 2 - (xs[0] + px / 2)) / px for i in range(5)]
    groups = [[] for _ in range(5)]
    for gr in blobs(c[..., 3] > 0, diag=True):
        if len(gr) >= 2:
            groups[int(np.argmin([abs(np.mean([x for _, x in gr]) - m) for m in centers]))] += gr
    for name, gr in zip(NAMES, groups):
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = c[y, x]
        yield name, ic


if __name__ == '__main__':
    for name, ic in icons(os.path.join(HERE, 'raw', 'fishing_icons_a.png')):
        Image.fromarray(ic, 'RGBA').save(os.path.join(OUT, name + '.png'))
        print('%s.png %d x %d' % (name, ic.shape[1], ic.shape[0]))
