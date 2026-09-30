# 농장 입구 기본 장식(2026-09-30 사용자 선택): 입구 아치 왼쪽 씨앗 자루 · 웜뱃 허수아비(시안 B), 오른쪽 모종 작업대 · 삽과 쇠스랑(시안 C).
# 원본: raw/props_a~c.png(Codex 시트, 프롬프트 raw/prompt_props.txt)에서 고른 물체를 잘라 둔 raw/prop_<이름>_part.png.
# 칸으로 옮기기: make_pixel.py(가로 칸 수 고정, 팔레트 문턱을 낮게 해 모종 싹 · 모종삽 날 같은 작은 색도 남김)
#   → Lab 거리(색상 차이 2배)로 가장 가까운 두 색을 합치기를 되풀이해 MAX색 → 가장 어두운 색 = 외곽선 (52,32,32).
#   드문 색을 버리는 방식은 싹 · 날 · 허수아비 눈이 갈색으로 뭉개져서 쓰지 않는다.
# 허수아비 얼굴은 옮기면 코와 눈이 한 덩어리가 되고 얼굴 그늘이 셔츠 올리브와 합쳐져, 칸 단위로 다시 칠한다(scarecrow_face).
# 출력: ../farm_prop_<이름>.png(한 칸 2px · PPU 80, 피벗 아래 가운데). 실행: Windows Python(Pillow · numpy) make_farm_props.py
import os
import subprocess
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
FARM = os.path.join(HERE, '..')
MAKE_PIXEL = os.path.join(HERE, '..', '..', 'Source~', 'make_pixel.py')
PX = 2
LINE = (52, 32, 32)
# (이름, 가로 칸 수, 최대 색 수)
PROPS = [('sack', 43, 7), ('scarecrow', 48, 8), ('bench', 66, 7), ('tools', 25, 7)]
# 허수아비 얼굴 손질 색
FACE, FACE_SHADE, OLIVE, HIGHLIGHT = (168, 144, 132), (136, 112, 104), (120, 108, 72), (228, 204, 168)
FACE_ROWS = (13, 22)


def merge_colors(a, limit):
    mask = a[..., 3] > 0
    while True:
        cols, counts = np.unique(a[mask][:, :3], axis=0, return_counts=True)
        if len(cols) <= limit:
            return a
        lab = np.asarray(Image.fromarray(cols.reshape(1, -1, 3).astype(np.uint8), 'RGB').convert('LAB')).reshape(-1, 3).astype(float)
        lab[:, 1:] = (lab[:, 1:] - 128) * 2.0
        d = ((lab[:, None, :] - lab[None, :, :]) ** 2).sum(2)
        np.fill_diagonal(d, 1e18)
        i, j = np.unravel_index(d.argmin(), d.shape)
        src, dst = (i, j) if counts[i] < counts[j] else (j, i)
        a[mask & (a[..., :3] == cols[src]).all(2), :3] = cols[dst]


def to_cells(name, cells_w, limit):
    mid = os.path.join(RAW, name + '_cells.png')
    subprocess.run([sys.executable, MAKE_PIXEL, os.path.join(RAW, 'prop_%s_part.png' % name), mid, '--px=1', '--cellsw=%d' % cells_w, '--th=0.002'], check=True, capture_output=True)
    a = np.asarray(Image.open(mid).convert('RGBA')).astype(int).copy()
    os.remove(mid)
    a = merge_colors(a, limit)
    mask = a[..., 3] > 0
    cols = np.unique(a[mask][:, :3], axis=0)
    darkest = cols[cols.sum(1).argmin()]
    a[mask & (a[..., :3] == darkest).all(2), :3] = LINE
    ys, xs = np.nonzero(mask)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].astype(np.uint8)


def scarecrow_face(a):
    def is_(y, x, c):
        return a[y, x, 3] > 0 and tuple(a[y, x, :3]) == c

    def put(y, x, c):
        a[y, x, :3] = c
        a[y, x, 3] = 255

    for y in range(FACE_ROWS[0], FACE_ROWS[1] + 1):
        xs = [x for x in range(a.shape[1]) if is_(y, x, FACE)]
        if not xs:
            continue
        for x in range(min(xs), max(xs) + 1):
            if is_(y, x, LINE):
                put(y, x, FACE)
        for x in range(max(0, min(xs) - 3), min(a.shape[1], max(xs) + 4)):
            if is_(y, x, OLIVE):
                put(y, x, FACE_SHADE)
    row = [x for x in range(a.shape[1]) if is_(17, x, FACE)]
    cx = (min(row) + max(row) + 1) // 2
    for dx in (-6, 5):                                          # 눈
        put(16, cx + dx, LINE)
        put(17, cx + dx, LINE)
    for y, (l, r) in zip((17, 18, 19), ((-2, 1), (-3, 2), (-2, 1))):   # 코
        for x in range(cx + l, cx + r + 1):
            put(y, x, LINE)
    put(17, cx - 1, HIGHLIGHT)
    for x in (cx - 3, cx + 2):                                  # 웃는 입
        put(21, x, LINE)
    for x in range(cx - 2, cx + 2):
        put(22, x, LINE)
    # 얼굴 그늘이 한 색 늘었으니 셔츠 밝은 그늘을 올리브에 합쳐 8색으로
    return merge_colors(a.astype(int), 8).astype(np.uint8)


def save(a, path):
    Image.fromarray(np.repeat(np.repeat(a, PX, axis=0), PX, axis=1)).save(path)
    n = len(np.unique(a[a[..., 3] > 0][:, :3], axis=0))
    print(os.path.relpath(path, HERE), 'cells', a.shape[1], 'x', a.shape[0], 'colors', n)


if __name__ == '__main__':
    for name, cells_w, limit in PROPS:
        cells = to_cells(name, cells_w, limit)
        if name == 'scarecrow':
            cells = scarecrow_face(cells)
        save(cells, os.path.join(FARM, 'farm_prop_%s.png' % name))
