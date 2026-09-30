# 설계 26 창고 버튼 아이콘 더미(시안 비교 전): 앞쪽 위에서 본 나무 상자 + 위로 삐져나온 밀 이삭 셋. 18×18px(UI 1px = 화면 4px, 버튼 안 72×72).
# 출력 ../icon_storage.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow) make_icon_storage.py
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PAL = {
    'o': (52, 32, 32, 255),     # 외곽선
    'h': (214, 170, 110, 255),  # 윗테 · 판자 밝은 면
    'w': (176, 128, 84, 255),   # 판자
    's': (132, 92, 58, 255),    # 판자 그늘 · 이음
    'k': (74, 48, 34, 255),     # 상자 안 어둠
    'g': (232, 196, 104, 255),  # 이삭
    'd': (184, 140, 64, 255),   # 이삭 그늘
}
ICON = [
    "..................",
    "....o...o....o....",
    "...ogo.ogo..ogo...",
    "...odo.odo..odo...",
    "...ogo.ogo..ogo...",
    "....d...d....d....",
    ".ooooooooooooooooo",
    ".ohhhhhhhhhhhhhhho",
    ".okkkkdkkkkdkkkkko",
    ".ooooooooooooooooo",
    ".owwwwwwswwwwwwwso",
    ".owwwwwwswwwwwwwso",
    ".ossssssssssssssso",
    ".owwwwwwswwwwwwwso",
    ".owwwwwwswwwwwwwso",
    ".ossssssssssssssso",
    ".ooooooooooooooooo",
    "..................",
]

im = Image.new('RGBA', (18, 18), (0, 0, 0, 0))
px = im.load()
for y, row in enumerate(ICON):
    for x, c in enumerate(row):
        if c in PAL:
            px[x, y] = PAL[c]
im.save(os.path.join(HERE, '..', 'icon_storage.png'))
print('icon_storage')
