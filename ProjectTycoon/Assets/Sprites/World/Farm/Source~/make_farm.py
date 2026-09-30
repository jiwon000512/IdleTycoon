# 설계 25 밀 농사: 밀 재료 아이콘(창고 칸 · 거두기 팝업 · 굽기 칩). 한 칸 2px · PPU 80, 피벗 가운데(BakeryBaker).
# 밭 흙판과 밀 단계 그림은 2026-09-30부터 Codex 시안을 칸 단위로 줄인 make_farm_art.py가 만든다(옛 더미 밭·밀 그림은 지웠다).
# 2026-09-30 사용자 「밭에 심긴 것과 같은 것으로」: 줄 그림에서는 포기들의 잎이 서로 닿아 한 포기를 못 오리므로, 밭 밀 줄(raw/wheat_a.png 익음 띠 + 게임 그림)을 참조로 주고
#   Codex에 한 포기만 그리게 했다(raw/wheat_icon_a~c.png, 프롬프트 raw/prompt_wheat_icon.txt). 사용자 선택 A → make_pixel.py로 CELLS_W칸 폭 격자 → 밭 밀(wheat_2_0) 팔레트 7색으로 맞춤 → 투명 여백 잘라 저장.
#   창고 칸 등은 InventoryView가 상자에 드는 정수 배로 보인다(13×24칸이라 칸에서 2배 = 밭과 같은 결. 10칸 폭 3배는 2026-09-30 사용자가 되돌림).
# 출력: Resources/Sprites/Items/wheat.png · manure.png · gem.png. 실행: Windows Python(Pillow · numpy) make_farm.py
import os
import subprocess
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites')
MAKE_PIXEL = os.path.join(HERE, '..', '..', 'Source~', 'make_pixel.py')
RAW = os.path.join(HERE, 'raw', 'wheat_icon_a.png')
PX = 2
CELLS_W = 13


def icon():
    mid = os.path.join(HERE, 'raw', 'wheat_icon_a_cells.png')
    subprocess.run([sys.executable, MAKE_PIXEL, RAW, mid, '--px=1', '--cellsw=%d' % CELLS_W], check=True, capture_output=True)
    field = np.asarray(Image.open(os.path.join(RES, 'Farm', 'wheat_2_0.png')).convert('RGBA'))[::PX, ::PX]
    pal = np.unique(field[field[..., 3] > 0][:, :3], axis=0).astype(int)
    a = np.asarray(Image.open(mid).convert('RGBA')).astype(int)
    os.remove(mid)
    mask = a[..., 3] > 0
    dist = ((a[..., None, :3] - pal[None, None, :, :]) ** 2).sum(3)
    out = np.zeros(a.shape, np.uint8)
    out[..., :3] = pal[dist.argmin(2)]
    out[..., 3] = np.where(mask, 255, 0)
    ys, xs = np.nonzero(mask)
    out = out[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    path = os.path.join(RES, 'Items', 'wheat.png')
    Image.fromarray(np.repeat(np.repeat(out, PX, axis=0), PX, axis=1)).save(path)
    print(os.path.relpath(path, HERE), 'cells', out.shape[1], 'x', out.shape[0], 'palette', len(pal))


# 설계 28 거름 · 반짝돌 아이콘: Codex 시안(raw/manure_icon_a~c · gem_icon_a~c, 프롬프트 raw/prompt_items.txt) → make_pixel.py 격자(가로 칸 수 · 팔레트 문턱 고정)
#   → 가까운 색을 합쳐 7색 안(art.md) → 한 칸 2px. 2026-09-30 사용자 선택: 거름 C(네모 거름 셋 + 새싹, 웜뱃의 네모 똥) · 반짝돌 B(민트 결정 + 반짝임)
ITEMS = [('manure', 'manure_icon_c.png', 20, 0.01), ('gem', 'gem_icon_b.png', 22, 0.012)]
MAX_COLORS = 7


def merge_colors(a, limit):
    # 불투명 칸의 색이 limit개를 넘으면 가장 적게 쓰인 색을 가장 가까운 다른 색으로 바꾸기를 되풀이한다(외곽선 · 하이라이트처럼 많이 쓰인 색은 남는다)
    while True:
        mask = a[..., 3] > 0
        colors, counts = np.unique(a[mask][:, :3], axis=0, return_counts=True)
        if len(colors) <= limit:
            return a
        i = counts.argmin()
        d = ((colors - colors[i]) ** 2).sum(1)
        d[i] = 1 << 30
        j = d.argmin()
        hit = mask & (a[..., :3] == colors[i]).all(2)
        a[hit, :3] = colors[j]


def item_icon(name, raw, cells_w, th):
    mid = os.path.join(HERE, 'raw', name + '_cells.png')
    subprocess.run([sys.executable, MAKE_PIXEL, os.path.join(HERE, 'raw', raw), mid, '--px=1', '--cellsw=%d' % cells_w, '--th=%s' % th], check=True, capture_output=True)
    a = np.asarray(Image.open(mid).convert('RGBA')).astype(int).copy()
    os.remove(mid)
    a = merge_colors(a, MAX_COLORS)
    ys, xs = np.nonzero(a[..., 3] > 0)
    out = a[ys.min():ys.max() + 1, xs.min():xs.max() + 1].astype(np.uint8)
    path = os.path.join(RES, 'Items', name + '.png')
    Image.fromarray(np.repeat(np.repeat(out, PX, axis=0), PX, axis=1)).save(path)
    print(os.path.relpath(path, HERE), 'cells', out.shape[1], 'x', out.shape[0], 'colors', len(np.unique(out[out[..., 3] > 0][:, :3], axis=0)))


if __name__ == '__main__':
    icon()
    for item in ITEMS:
        item_icon(*item)
