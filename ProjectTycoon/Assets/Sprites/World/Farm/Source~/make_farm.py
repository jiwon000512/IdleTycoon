# 설계 25 밀 농사: 밀 재료 아이콘(창고 칸 · 거두기 팝업 · 굽기 칩). 한 칸 2px · PPU 80, 피벗 가운데(BakeryBaker).
# 밭 흙판과 밀 단계 그림은 2026-09-30부터 Codex 시안을 칸 단위로 줄인 make_farm_art.py가 만든다(옛 더미 밭·밀 그림은 지웠다).
# 2026-09-30 사용자 「밭에 심긴 것과 같은 것으로」: 익음 단계 줄 그림(Resources/Sprites/Farm/wheat_2_0.png)의 가운데 포기에서 이삭(EAR 창)을 그대로 오려 붙이고,
#   줄 그림에서는 포기들의 잎이 서로 겹쳐 따로 오릴 수 없어 줄기 · 잎 두 장만 같은 팔레트(줄기 e · 잎 e/d · 외곽선 a)로 아래에 그린다. 12×20칸.
#   창고 칸 등은 InventoryView가 상자에 맞는 정수 배로 보인다.
# 출력: Resources/Sprites/Items/wheat.png. 실행: Windows Python(Pillow · numpy) make_farm.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites')
PX = 2
# 이삭 창(칸): wheat_2_0의 가운데 포기(52~62열, 1~11줄). 줄이면서 깨진 곳을 손본다: 이웃 잎 조각 두 칸은 비우고(CLEAR), 끊긴 외곽선 두 칸은 잇는다(FILL). 창 안 좌표
EAR_X, EAR_Y, EAR_W, EAR_ROWS = 52, 1, 11, 11
EAR_CLEAR = [(5, 0), (10, 10)]
EAR_FILL = [(5, 1), (7, 9)]
W, H = 12, 19
PAL = {'#': (60, 24, 24), 'e': (156, 96, 36), 'd': (204, 144, 48)}
LOWER = ['....#ee#....',
         '...##ee##...',
         '..#e#ee#e#..',
         '.#de#ee#ed#.',
         '#de##ee##ed#',
         '#e#.#ee#.#e#',
         '.#..#ee#..#.',
         '....####....']


def icon():
    field = np.asarray(Image.open(os.path.join(RES, 'Farm', 'wheat_2_0.png')).convert('RGBA'))[::PX, ::PX]
    ear = field[EAR_Y:EAR_Y + EAR_ROWS, EAR_X:EAR_X + EAR_W].copy()
    for y, x in EAR_CLEAR:
        ear[y, x] = 0
    for y, x in EAR_FILL:
        ear[y, x] = PAL['#'] + (255,)
    out = np.zeros((H, W, 4), np.uint8)
    out[:EAR_ROWS, (W - EAR_W) // 2:(W - EAR_W) // 2 + EAR_W] = ear
    for y, row in enumerate(LOWER):
        for x, ch in enumerate(row):
            if ch in PAL:
                out[EAR_ROWS + y, x] = PAL[ch] + (255,)
    path = os.path.join(RES, 'Items', 'wheat.png')
    Image.fromarray(np.repeat(np.repeat(out, PX, axis=0), PX, axis=1)).save(path)
    print(os.path.relpath(path, HERE), W, 'x', H)


if __name__ == '__main__':
    icon()
