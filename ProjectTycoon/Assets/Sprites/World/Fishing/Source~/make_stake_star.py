# 말뚝 밑 등급 별(2026-10-05 품질 루프: 노란 네모 셋이 「저게 뭐지?」 → 작은 별). 기하 도형이라 칸 무늬로 그린다(한 칸 2px).
#   색은 낚싯대 금 등급(make_rods.py GOLD)과 같은 금빛 + 진갈색 외곽선. 등급 수만큼 나란히(코드).
# 출력: ../stake_star.png(7×7칸, PPU 80, 가운데 피벗). 사용: python make_stake_star.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
COLORS = {'#': (52, 32, 32), 'H': (252, 234, 150), 'Y': (240, 196, 76), 'D': (206, 150, 44)}
STAR = ['...#...',
        '..#H#..',
        '###HY##',
        '#HYYYD#',
        '.#YYD#.',
        '.#Y#D#.',
        '.##.##.']

if __name__ == '__main__':
    a = np.zeros((len(STAR), len(STAR[0]), 4), np.uint8)
    for y, row in enumerate(STAR):
        for x, ch in enumerate(row):
            if ch in COLORS:
                a[y, x] = COLORS[ch] + (255,)
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', 'stake_star.png'))
    print('stake_star.png %d x %d 칸' % (a.shape[1], a.shape[0]))
