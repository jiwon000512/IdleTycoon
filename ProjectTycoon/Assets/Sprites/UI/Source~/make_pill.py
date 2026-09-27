# -*- coding: utf-8 -*-
# 공용 알약(2026-09-28, UI 작업 규칙 v1.0): Codex 시안 B 「들어간 칸」 → pill(값·HUD·토스트), 시안 C 「주황 테두리」 → pill_tag(이름표).
# 시안의 외곽선 상자를 칸 수로 나눠 칸 가운데 색을 뽑고(격자 측정이 흔들려 칸 수를 직접 준다), 바깥 흰 바탕만 투명으로, 비슷한 색을 한 색으로 모은다.
# 9-slice 경계는 좌우만(둥근 끝 전체), 위아래 0: 알약 높이를 원본 칸 수의 정수 배로 두면 가로로만 늘어나고 둥근 끝 비율이 그대로다.
# 사용: make_pill.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'


def cells(path, wc, hc):
    a = np.asarray(Image.open(path).convert('RGB')).astype(int)
    ys, xs = np.nonzero(a.sum(2) < 250)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    pw, ph = (x1 - x0) / wc, (y1 - y0) / hc
    r = max(1, int(min(pw, ph) * 0.2))
    out = np.zeros((hc, wc, 4), np.uint8)
    for j in range(hc):
        for i in range(wc):
            cy, cx = int(y0 + (j + 0.5) * ph), int(x0 + (i + 0.5) * pw)
            out[j, i, :3] = np.median(a[cy - r:cy + r + 1, cx - r:cx + r + 1].reshape(-1, 3), axis=0)
    out[..., 3] = 255
    white = out[..., :3].astype(int).min(2) > 235
    seen = np.zeros_like(white)
    q = [(y, x) for y in range(hc) for x in (0, wc - 1)] + [(y, x) for x in range(wc) for y in (0, hc - 1)]
    while q:
        y, x = q.pop()
        if 0 <= y < hc and 0 <= x < wc and white[y, x] and not seen[y, x]:
            seen[y, x] = True
            q += [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
    out[seen] = 0
    return out


def quantize(a, th=28):
    rgb = a[..., :3].astype(float); on = a[..., 3] > 0
    centers = []
    for c in rgb[on]:
        for k in centers:
            if np.sqrt(((k[0] - c) ** 2).sum()) < th:
                k[1].append(c); break
        else:
            centers.append([c, [c]])
    centers = [np.mean(k[1], axis=0) for k in centers]
    for idx in zip(*np.nonzero(on)):
        a[idx][:3] = np.round(min(centers, key=lambda k: ((k - rgb[idx]) ** 2).sum()))
    return a


slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
for name, path, wc, hc in (('pill', 'raw/pill_b.png', 55, 15), ('pill_tag', 'raw/pill_c.png', 62, 18)):
    a = quantize(cells(path, wc, hc))
    # 늘어나는 가운데(좌우 둥근 끝 6칸 사이)는 줄마다 가장 많은 색 하나로: 잡티가 가로로 늘어나지 않게
    for y in range(hc):
        mid = a[y, 6:wc - 6]
        colors, counts = np.unique(mid.reshape(-1, 4), axis=0, return_counts=True)
        a[y, 6:wc - 6] = colors[counts.argmax()]
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': wc, 'h': hc, 'border': [6, 0, 6, 0]}
    print(name, wc, 'x', hc)
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
