# 설계 29 웜뱃 석상 → ../statue.png(아래 가운데 피벗, PPU 80, 스케일 1). 석상 버튼 아이콘(18×18)도 같이 → Resources/Sprites/Actions/statue.png.
# 석상(2026-09-30 사용자: 처음 크기의 1.5배 · 받침 봉헌 제단 원본): 1.5배를 스케일로 키우면 한 칸이 3px라 art.md(크기는 칸 수로)와 어긋나서,
#   Codex에 1.5배 칸 수(폭 약 87칸, 웜뱃 약 63칸)로 석상 전체를 다시 그리게 했다. 참조: 옛 석상(60칸)을 한 칸 16px로 키운 raw/statue15_ref16.png(웜뱃 결),
#   처음 받침 시안 raw/statue_pedestal_first_b.png(봉헌 제단 디자인). 프롬프트 raw/statue15_prompt.ps1. 세 장(raw/statue15_a~c.png) 가운데
#   크기 · 받침 무늬 · 웜뱃이 옛 석상에 가장 가까운 a를 원본 격자 그대로 옮긴다(snap_codex, 칸 크기 힌트 = 그림 폭 / 87, 정사각형 격자).
#   옛 방식(1배 석상 58~60칸, 받침만 다시 그리거나 옛 웜뱃을 얹던 것)의 시안은 raw/statue_pedestal_*에 남아 있다.
# 실행: Windows Python(Pillow · numpy) make_statue.py
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ACTIONS = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Actions')
LINE = (52, 32, 32)
STONE = [(88, 80, 78), (122, 114, 108), (156, 148, 138), (190, 182, 170), (222, 216, 204)]
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
STATUE_RAW = os.path.join(HERE, 'raw', 'statue15_a.png')
TARGET_W = 87             # 옛 석상 58칸 × 1.5

raw = np.asarray(Image.open(STATUE_RAW).convert('RGB')).astype(int)
ys, xs = np.nonzero(np.abs(raw - 255).sum(2) > 60)
cells, _ = snap_codex.snap(STATUE_RAW, 12, cell=(xs.max() - xs.min() + 1) / TARGET_W, square=True)
img = Image.fromarray(np.repeat(np.repeat(cells, 2, 0), 2, 1), 'RGBA')
img.save(os.path.join(HERE, '..', 'statue.png'))
print('statue', img.size, 'cells', cells.shape[1], 'x', cells.shape[0])

# 석상 버튼 아이콘(2026-09-30 사용자 선택 A 「돌 웜뱃 얼굴」): 행동 아이콘(UI/Source~/make_act_button.py)처럼 도형 + 바깥 1칸 진갈색 외곽선. 돌 색은 옛 석상과 같다
im = Image.new('RGBA', (18, 18))
d = ImageDraw.Draw(im)
d.ellipse([2, 1, 6, 5], fill=STONE[1] + (255,))
d.ellipse([11, 1, 15, 5], fill=STONE[1] + (255,))
d.ellipse([2, 2, 15, 13], fill=STONE[2] + (255,))
d.rectangle([4, 4, 6, 5], fill=STONE[4] + (255,))            # 머리 위 하이라이트 한 획
d.rectangle([3, 11, 14, 12], fill=STONE[1] + (255,))         # 턱 그늘
for x, y in ((5, 8), (12, 8), (8, 10), (9, 10)):              # 눈 · 코 끝
    d.point((x, y), fill=LINE + (255,))
d.rectangle([7, 8, 10, 9], fill=LINE + (255,))               # 코
d.rectangle([1, 14, 16, 16], fill=STONE[3] + (255,))         # 받침 판
d.rectangle([1, 16, 16, 16], fill=STONE[1] + (255,))
icon = np.asarray(im).copy()
filled = icon[..., 3] > 0
grown = filled.copy()
grown[1:] |= filled[:-1]; grown[:-1] |= filled[1:]; grown[:, 1:] |= filled[:, :-1]; grown[:, :-1] |= filled[:, 1:]
icon[grown & ~filled] = LINE + (255,)
Image.fromarray(icon, 'RGBA').save(os.path.join(ACTIONS, 'statue.png'))
print('icon', icon.shape)
