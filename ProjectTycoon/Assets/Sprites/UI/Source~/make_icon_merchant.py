# 설계 32 · 55 행상 너구리 얼굴 아이콘. 18×18px(한 칸 1px = UI 4px, 메뉴 줄 단추 안 72px로 다른 아이콘과 같은 칸).
#   2026-10-01 사용자 선택 A 「얼굴만」(24칸, 버린 시안: B 얼굴 + 노란 목수건 · C 돌 메달 안 얼굴)을 2026-10-09 사용자 선택(설계 55)으로 18칸에 옮김:
#   24칸 그림을 칸 가운데 표본으로 15×15칸에 줄인 뒤 눈 · 코 · 입 · 귀 속을 좌우 같게 손질, 색은 너구리 정지 그림(World/Shop/tanuki_front.png)의 것 그대로.
#   없을 때(행상이 오기 전)는 icon_merchant_away: 색을 빼고(밝기만) 크림 쪽으로 30% 밝힌 회색 그림(코드 틴트 0.45는 검은 짐승처럼 보였다).
# 출력 ../icon_merchant.png · ../icon_merchant_away.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_icon_merchant.py → 메뉴 Import UI Sprites
import json
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
INK = (52, 32, 32)
# b 짙은 털 · d 털 · e 머리 위 하이라이트 · c 분홍(귀 속 · 볼) · f 눈 탈 · g 눈 빛 · k 눈 · 코 · 입 · h 크림 주둥이
PAL = {'b': (152, 105, 84), 'd': (179, 138, 115), 'e': (208, 170, 145), 'c': (227, 152, 139),
       'f': (98, 67, 59), 'g': (255, 255, 255), 'k': INK, 'h': (240, 228, 210)}
ROWS = [
    "..bb.......bb..",
    ".bcbb.....bbcb.",
    ".bccdddddddccb.",
    ".bddeedddddddb.",
    ".ddddddddddddd.",
    ".ddddddddddddd.",
    "ddffffdddffffdd",
    "dffgfffdfffgffd",
    "dffkffhhhffkffd",
    "dddfhhkkkhhfddd",
    ".dcchhhkhhhccd.",
    ".bbdhhkhkhhdbb.",
    ".bbbhhhhhhhbbb.",
    "..bbbbhhhbbbb..",
    "....bbbbbbb....",
]


def face():
    a = np.zeros((18, 18, 4), np.uint8)
    for y, row in enumerate(ROWS):
        for x, ch in enumerate(row):
            if ch != '.':
                a[y + 1, x + 1] = PAL[ch] + (255,)
    # 가운데로(위아래 · 좌우 빈 줄을 고르게) 뒤 바깥 1칸 외곽선
    ys, xs = np.nonzero(a[..., 3])
    a = np.roll(a, ((18 - (ys.max() + 1 + ys.min())) // 2, (18 - (xs.max() + 1 + xs.min())) // 2), (0, 1))
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def away(a):
    b = a.copy()
    body = (b[..., 3] > 0) & ~(b[..., :3] == INK).all(2)
    lum = b[..., 0] * 0.299 + b[..., 1] * 0.587 + b[..., 2] * 0.114
    grey = lum * 0.7 + 240 * 0.3
    for i in range(3):
        b[..., i] = np.where(body, np.clip(grey, 0, 255), b[..., i])
    return b.astype(np.uint8)


if __name__ == '__main__':
    a = face()
    Image.fromarray(a).save(os.path.join(OUT, 'icon_merchant.png'))
    Image.fromarray(away(a)).save(os.path.join(OUT, 'icon_merchant_away.png'))
    slices = json.load(open(os.path.join(OUT, 'ui_slices.json'), encoding='utf-8'))
    slices['icon_merchant'] = {'w': 18, 'h': 18, 'border': None}
    slices['icon_merchant_away'] = {'w': 18, 'h': 18, 'border': None}
    json.dump(slices, open(os.path.join(OUT, 'ui_slices.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('icon_merchant · icon_merchant_away 18x18')
