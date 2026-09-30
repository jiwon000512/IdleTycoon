# 설계 25 밀 농사: 밀 재료 아이콘(창고 칸 · 거두기 팝업 · 굽기 칩). 한 칸 2px · PPU 80, 피벗 가운데(BakeryBaker).
# 밭 흙판과 밀 단계 그림은 2026-09-30부터 Codex 시안을 칸 단위로 줄인 make_farm_art.py가 만든다(옛 더미 밭·밀 그림은 지웠다).
# 2026-09-30 사용자 「밭에 심긴 것과 같은 것으로」: 줄 그림에서는 포기들의 잎이 서로 닿아 한 포기를 못 오리므로, 밭 밀 줄(raw/wheat_a.png 익음 띠 + 게임 그림)을 참조로 주고
#   Codex에 한 포기만 그리게 했다(raw/wheat_icon_a~c.png, 프롬프트 raw/prompt_wheat_icon.txt). 사용자 선택 A → make_pixel.py로 13칸 폭 격자 → 밭 밀(wheat_2_0) 팔레트 7색으로 맞춤 → 투명 여백 잘라 저장.
#   창고 칸 등은 InventoryView가 상자에 드는 정수 배로 보인다(13×24칸이라 칸에서 2배 = 밭과 같은 결).
# 출력: Resources/Sprites/Items/wheat.png. 실행: Windows Python(Pillow · numpy) make_farm.py
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


if __name__ == '__main__':
    icon()
