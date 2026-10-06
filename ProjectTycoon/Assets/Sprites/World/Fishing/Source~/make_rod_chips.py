# 시트 칩 아이콘 여섯(2026-10-05 설계 46: 빈 말뚝 · 오두막 시트에서 계열 · 설치물을 고르는 칩). 프롬프트 raw/prompt_rod_chips.txt.
#   바탕 raw/rod_chips_template.png(한 칸 9px, 윗줄에 굽기 칩 빵 아이콘 셋을 크기 기준으로)의 아랫줄 빈 칸 여섯에 그리게 해 빵 아이콘과 같은 칸 크기로 나왔다.
#   에이전트 선택 A(비스듬한 대 · 설치물, raw/rod_chips_a.png), B(말뚝에 꽂힌 짧은 대)는 기록.
#   원본 그대로 옮긴다(snap_codex raw 격자, 칸 가운데 중앙값 그대로). 칸 밖으로 나온 조각도 덩어리 가운데가 가장 가까운 칸에 붙여 자른다.
# 그물 계열 칩(2026-10-06 설계 49): 바탕 raw/rod_chip_net_template.png(rod_chips_a.png의 넷째 칸 도르래탑을 지우고 빈 틀로)에 그리게 한 raw/rod_chip_net.png의 넷째 칸.
#   이웃 칩과 같은 줄에 그리게 해야 크기 · 결이 맞았다(빈 바탕 첫 칸에 그리게 하면 한 칸 5.5px로 잘게 나왔다). 테가 틀 위로 올라가 윗줄을 8칸 더 본다(rise).
# 출력: Resources/Sprites/Rods/<bamboo|iron|bait|net>.png(한 칸 2px = 빵 아이콘과 같은 결, 칩은 그림 크기 × 2로 보인다)
# 사용: python make_rod_chips.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

OUT = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Rods')
NAMES = ['bamboo', 'iron', 'bait', 'pulley', 'lighthouse', 'whirlpool']
P, N, X0, GAP, BOT = 9, 26, 15, 20, 640        # 바탕 한 칸 · 칸 수 · 아랫줄 칸 자리(raw/rod_chips_template.png)


def slot(i):
    return X0 + i * (N * P + GAP)


def chips(path, names, rise=4):
    _, (px, phx, py, phy) = snap_codex.snap(path, square=True, min_hole=40, raw=True)
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    h, w = a.shape[:2]
    xs, ys = np.arange(phx + 1, w, px), np.arange(phy + 1, h, py)
    cj = [k for k in range(len(ys) - 1) if BOT - rise * P <= ys[k] and ys[k + 1] <= BOT + (N + 4) * P]
    c = np.zeros((len(cj), len(xs) - 1, 4), np.uint8)
    for jj, j in enumerate(cj):
        for k in range(len(xs) - 1):
            y0, y1, x0, x1 = ys[j], ys[j + 1], xs[k], xs[k + 1]
            col = np.median(a[int(y0 + (y1 - y0) * 0.25):int(y1 - (y1 - y0) * 0.25) + 1, int(x0 + (x1 - x0) * 0.25):int(x1 - (x1 - x0) * 0.25) + 1].reshape(-1, 3), 0).round()
            c[jj, k, :3] = col
            frame = col.max() - col.min() < 12 and col.min() > 170        # 회색 틀 자국
            c[jj, k, 3] = 0 if (col.min() > 238 or frame) else 255
    centers = [(slot(i) + N * P / 2 - (xs[0] + px / 2)) / px for i in range(6)]
    groups = [[] for _ in range(6)]
    for gr in blobs(c[..., 3] > 0, diag=True):
        if len(gr) >= 2:
            groups[int(np.argmin([abs(np.mean([x for _, x in gr]) - m) for m in centers]))] += gr
    for name, gr in zip(names, groups):
        if not name:
            continue
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = c[y, x]
        yield name, ic


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    # 설치물 셋(NAMES 뒤 셋)은 말뚝에서 빠져 칩이 없다(2026-10-05 설계 46: 설치물 · 대물 · 대 들기 · 합치기를 없애 이 그림은 만들지 않는다)
    for src, names in (('rod_chips_a.png', NAMES[:3]), ('rod_chip_net.png', [None, None, None, 'net'])):
        for name, ic in chips(os.path.join(HERE, 'raw', src), names, 8 if 'net' in names else 4):
            Image.fromarray(np.repeat(np.repeat(ic, 2, 0), 2, 1), 'RGBA').save(os.path.join(OUT, name + '.png'))
            print('%s.png %d x %d 칸' % (name, ic.shape[1], ic.shape[0]))
