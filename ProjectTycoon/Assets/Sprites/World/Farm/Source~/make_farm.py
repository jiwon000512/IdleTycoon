# 설계 25 밀 농사: 밀 재료 아이콘(창고 칸 · 거두기 팝업 · 굽기 칩). 한 칸 2px · PPU 80, 피벗 가운데(BakeryBaker).
# 밭 흙판과 밀 단계 그림은 2026-09-30부터 Codex 시안을 칸 단위로 줄인 make_farm_art.py가 만든다(옛 더미 밭·밀 그림은 지웠다).
# 2026-09-30 사용자 「밀 아이템이 예전 리소스 같다」: 다발 대신 밭의 밀(wheat_a 익음 단계)과 같은 팔레트의 통통한 이삭 하나 12×12칸.
# 출력: Resources/Sprites/Items/wheat.png. 실행: Windows Python(Pillow) make_farm.py
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites')
PX = 2

# 색은 make_farm_art.py EAR(익음 단계 팔레트) + art.md 외곽선
PAL = {'#': (52, 32, 32, 255), 'g': (228, 168, 60, 255), 'G': (204, 144, 48, 255), 'h': (240, 216, 144, 255), 's': (156, 96, 36, 255)}
ICON = ['.....##.....',
        '....#hg#....',
        '...#gGgG#...',
        '...#GghG#...',
        '..#gGgGgG#..',
        '..#GghgGg#..',
        '..#gGgGgG#..',
        '...#GghG#...',
        '...#gGgG#...',
        '....#ss#....',
        '....#ss#....',
        '.....##.....']


def save(rows, path):
    h, w = len(rows), len(rows[0])
    im = Image.new('RGBA', (w * PX, h * PX), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in PAL:
                for dy in range(PX):
                    for dx in range(PX):
                        px[x * PX + dx, y * PX + dy] = PAL[ch]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print(os.path.relpath(path, HERE), w, 'x', h)


if __name__ == '__main__':
    save(ICON, os.path.join(RES, 'Items', 'wheat.png'))
