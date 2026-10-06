# -*- coding: utf-8 -*-
# 낚싯줄 찌(칸 편집, AI 없음): 대가 감는 물고기 위 줄 끝에 뜬다. 한 칸 = 2px(PPU 80)
# 2026-10-06 사용자 선택 A 「동글 찌」(시안 B 막대 찌 · C 오리 찌 중). 빨강 · 흰 공 + 위 막대
# 두 칸: 0 = 떠 있음, 1 = 까딱 잠김(두 칸 내려가 물 아래는 지우고 물결이 한 칸 넓어짐). 대가 당길 때 1
# 13×12칸(26×24px), 피벗 = 맨 위 가운데(줄이 닿는 막대 끝 → (0.5, 1))
# 사용: make_bobber.py [<출력 폴더>=..] → bobber_0.png · bobber_1.png
import os, sys
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
H = lambda h: np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)] + [255], np.uint8)
COL = {
    'o': H('#342020'), 'k': H('#5C4C42'),
    'r': H('#D0503C'), 'R': H('#F08070'),
    'w': H('#F8F4EC'), 'W': H('#D9C8B4'),
}
RIPPLE = H('#CCEADE')
ROWS = [
    '....o....',
    '...oko...',
    '...oko...',
    '..ooooo..',
    '.orRrrro.',
    '.orrrrro.',
    '.owwwwwo.',
    '.owwwwWo.',
    '..owwWo..',
    '...ooo...',
]
WATER = 8  # 물 높이 줄(이 줄 아래는 물속이라 그리지 않는다)


def frame(dip):
    h, w = len(ROWS), len(ROWS[0])
    pad = 2  # 물결이 몸보다 양옆으로 넓게
    c = np.zeros((h + 2, w + pad * 2, 4), np.uint8)
    for y, row in enumerate(ROWS):
        for x, ch in enumerate(row):
            if ch in COL and y + dip <= WATER:
                c[y + dip, x + pad] = COL[ch]
    # 물결: 물 높이 줄에 몸 양옆으로 한 칸(잠기면 두 칸) + 그 아래 한 줄
    body = [x for x in range(c.shape[1]) if c[WATER, x, 3]]
    spread = 1 + dip // 2
    for x in list(range(max(0, body[0] - spread), body[0])) + list(range(body[-1] + 1, min(c.shape[1], body[-1] + 1 + spread))):
        c[WATER, x] = RIPPLE
    c[WATER + 1, max(0, body[0] - spread + 1):min(c.shape[1], body[-1] + spread)] = RIPPLE
    return c


if __name__ == '__main__':
    for i, dip in enumerate((0, 2)):
        Image.fromarray(np.repeat(np.repeat(frame(dip), 2, 0), 2, 1), 'RGBA').save(os.path.join(OUT, 'bobber_%d.png' % i))
