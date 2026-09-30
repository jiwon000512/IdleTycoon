# 설계 29 웜뱃 석상 더미(시안 셋 → 선택 전까지): 웜뱃 앞모습(Shop/wombat_front, 한 칸 2px)을 돌 색 다섯으로 바꿔 받침돌 위에 올린다 → ../statue.png(아래 가운데 피벗, PPU 80)
# 석상 버튼 아이콘(18×18, 행동 아이콘 팔레트)도 같이 → Resources/Sprites/Actions/statue.png. 실행: Windows Python(Pillow · numpy) make_statue.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..', '..', 'Shop')
ACTIONS = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Actions')
LINE = (52, 32, 32)
STONE = [(88, 80, 78), (122, 114, 108), (156, 148, 138), (190, 182, 170), (222, 216, 204)]
PLINTH_W, PLINTH_H = 58, 14

wombat = np.asarray(Image.open(os.path.join(SHOP, 'wombat_front.png')).convert('RGBA'))[::2, ::2]
h, w = wombat.shape[:2]
rgb = wombat[..., :3].astype(int)
alpha = wombat[..., 3] > 0
lum = rgb @ np.array([0.3, 0.59, 0.11])
outline = alpha & (np.abs(rgb - LINE).sum(2) < 60)
body = alpha & ~outline
lo, hi = lum[body].min(), lum[body].max()
level = np.clip(((lum - lo) / max(1, hi - lo) * (len(STONE) - 1)).round().astype(int), 0, len(STONE) - 1)

W = max(w, PLINTH_W)
H = h + PLINTH_H - 2
out = np.zeros((H, W, 4), np.uint8)
x0 = (W - w) // 2
for y in range(h):
    for x in range(w):
        if outline[y, x]:
            out[y, x0 + x] = LINE + (255,)
        elif body[y, x]:
            out[y, x0 + x] = STONE[level[y, x]] + (255,)

# 받침돌: 윗면(밝게, 3칸) · 앞면(어둡게) · 외곽선 1칸, 웜뱃 발밑 2칸과 겹친다
px0 = (W - PLINTH_W) // 2
top = h - 2
for y in range(PLINTH_H):
    for x in range(PLINTH_W):
        edge = y in (0, PLINTH_H - 1) or x in (0, PLINTH_W - 1)
        color = LINE if edge else STONE[3] if y <= 3 else STONE[1] if y < PLINTH_H - 3 else STONE[0]
        if y == 4 and not edge:
            color = LINE
        yy, xx = top + y, px0 + x
        if yy < H and (out[yy, xx, 3] == 0 or y >= 2):
            out[yy, xx] = color + (255,)

img = Image.fromarray(out, 'RGBA').resize((W * 2, H * 2), Image.NEAREST)
img.save(os.path.join(HERE, '..', 'statue.png'))
print('statue', img.size)

ICON = ['..................',
        '....##......##....',
        '...#mm#....#mm#...',
        '...#mm######mm#...',
        '..#llllllllllll#..',
        '..#llllllllllll#..',
        '.#ll#llllllll#ll#.',
        '.#llllll##llllll#.',
        '.#lllll####lllll#.',
        '.#mllllllllllllm#.',
        '..#mmmmmmmmmmmm#..',
        '...############...',
        '..#llllllllllll#..',
        '..#mmmmmmmmmmmm#..',
        '..#dddddddddddd#..',
        '..#dddddddddddd#..',
        '..##############..',
        '..................']
PAL = {'#': LINE, 'l': (244, 224, 160), 'm': (232, 196, 104), 'd': (184, 140, 64)}
icon = np.zeros((18, 18, 4), np.uint8)
for y, row in enumerate(ICON):
    for x, ch in enumerate(row):
        if ch in PAL:
            icon[y, x] = PAL[ch] + (255,)
Image.fromarray(icon, 'RGBA').save(os.path.join(ACTIONS, 'statue.png'))
print('icon', icon.shape)
