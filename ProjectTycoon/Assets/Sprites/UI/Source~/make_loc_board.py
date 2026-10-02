# 위치 팝업 판(2026-10-02 사용자 선택 C2 「반투명 판자」, 시안 A HUD 알약 · B 주황 리본 · C1 작은 판자 · C3 작고 반투명은 버림).
# 광장 빵집 간판 판자(World/Plaza/sign.png, 월드 한 칸 2px)를 UI 한 칸 1px로 옮긴다: 43×15칸, 9-slice 가로만(양끝 6칸, 못 넷이 끝 안에 든다).
# 늘어나는 가운데는 줄마다 가장 많은 색 하나로 고른다(나무결 얼룩이 가로로 늘어나지 않게). 판은 불투명 그림이고 55% 비침은 Image 색 알파로 준다(글자는 진하게).
# 출력 ../loc_board.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_loc_board.py → 메뉴 Import UI Sprites
import json
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
SIGN = os.path.join(HERE, '..', '..', 'World', 'Plaza', 'sign.png')
END = 6

a = np.asarray(Image.open(SIGN).convert('RGBA'))[::2, ::2].copy()
h, w = a.shape[:2]
for y in range(h):
    mid = a[y, END:w - END]
    colors, counts = np.unique(mid.reshape(-1, 4), axis=0, return_counts=True)
    a[y, END:w - END] = colors[counts.argmax()]
Image.fromarray(a).save(os.path.join(OUT, 'loc_board.png'))

path = os.path.join(OUT, 'ui_slices.json')
slices = json.load(open(path, encoding='utf-8'))
slices['loc_board'] = {'w': w, 'h': h, 'border': [END, 0, END, 0]}
json.dump(slices, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('loc_board', w, 'x', h)
