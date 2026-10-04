# 점원 옷(2026-10-02 사용자): 회색 점원 웜뱃에 키 큰 요리사 모자. 시안 「앞치마 · 앞치마 + 두건 · 앞치마 + 납작 모자」 → 「빵집 모자 만들어봐」
#   → 요리사 모자 · 식빵 모자 · 크루아상 모자 중 요리사 모자, 앞치마는 「안 어울린다 하지마」로 뺐다.
#   몸 위에 따로 붙이는 겹 그림이 아니라 기본 그림 자체에 그려 넣어(별 배지가 「웜뱃 몸과 안 맞는다」) 숨쉬기 · 걷기 조립에서 몸과 같이 움직인다.
#   빵집 웜뱃 기본 그림(wombat_<방향>_base.png)의 털 5색을 회색으로 바꾸고(make_anim.GRAY), 머리 꼭대기에 모자를 씌운 뒤 모자 높이만큼 위를 늘린다.
#   앞 · 뒤: 하얀 토크(주름 셋 · 오른쪽 그늘) + 머리를 감싸는 띠 세 줄, 바깥 1칸 외곽선. 앞치마 · 얹은 모자 시안 다음 「푹 씌우기」.
#   옆: 사용자 「앞모습을 옆으로 돌렸을 때 어떻게 되겠는가, 옆모습용 모자를 뽑자」 → Codex 시안 A 「정직하게 돌린 모자」
#       (모자는 정수리 가운데에 앞과 같은 폭, 가까운 큰 귀가 띠 앞으로 나오고 먼 귀는 모자 뒤에 가린다)
# make_anim.py가 build()로 바로 만들어 쓴다(늘린 줄 수만큼 WOMBAT 좌표를 내린다). 이 파일을 직접 돌리면 확인용 clerk_<방향>_base.png를 쓴다
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402

INK = (36, 24, 24)
HAT = {'w': (250, 248, 244), 'g': (214, 210, 204), 'p': (230, 226, 220)}   # 하양 · 그늘 · 주름
PUFF = [   # 부푼 윗부분(띠 위)
    "..ww..www..ww...",
    ".wwwwwwwwwwwwww.",
    "wwwpwwwpwwwpwwwg",
    "wwwpwwwpwwwpwwwg",
    "wwwpwwwpwwwpwwgg",
    ".wwwwwwwwwwwwgg.",
]
CENTER = 21          # 모자 가운데 열(머리 꼭대기 가운데)
# 2026-10-02 사용자 「모자가 머리를 좀 가리도록 일체감 있게」: 얹지 않고 푹 씌운다.
#   띠 아랫선은 머리 꼭대기에서 SINK줄 아래, 앞쪽(가운데)이 한 줄 더 내려온 둥근 선(내려다본 머리띠)
SINK = 4
BAND = 3             # 띠 줄 수(아랫선에서 위로)
# 옆 시안 원본(shop_raw/clerk_side_hat_prompt.txt): 원본 격자 그대로 칸으로 옮기면 몸이 원래 옆 그림과 칸 모양이 똑같다
#   → 머리 윗부분(원래 그림 SIDE_CUT줄 위)만 시안에서 가져와 원래 그림 · 모자 색으로 맞춘다
SIDE_RAW = os.path.join(HERE, 'shop_raw', 'clerk_side_hat_a.png')
SIDE_GRID = (26.38, 26.25, 26.4124, 25.25)   # 가로 주기 · 시작, 세로 주기 · 시작
SIDE_CUT = 10


def head_top(a, col):
    # 모자 가운데 열에서 맨 위 불투명 칸(머리 꼭대기 · 귀 외곽선)
    return int(np.nonzero(a[:, col, 3] > 0)[0][0])


def side(a):
    s = snap_codex.snap(SIDE_RAW, 12, fixed=SIDE_GRID, min_hole=400)[0]
    pad = s.shape[0] - a.shape[0] + 1                     # 시안이 더 높은 줄 + 윗 외곽선 위 빈 줄
    dx = a.shape[1] - s.shape[1]                          # 시안은 왼쪽 빈 열이 잘려 있다(오른쪽 끝이 같다)
    assert not a[:, :dx, 3].any() and ((s[pad - 1 + SIDE_CUT:, :, 3] > 0) == (a[SIDE_CUT:, dx:, 3] > 0)).all(), '시안 몸이 원래 옆 그림과 어긋난다'
    out = np.zeros((a.shape[0] + pad, a.shape[1], 4), np.uint8)
    out[pad:] = a
    top = out[:pad + SIDE_CUT]
    top[:] = 0
    top[1:, dx:] = s[:pad - 1 + SIDE_CUT]
    # 시안 색 → 가장 가까운 원래 그림 · 모자 · 외곽선 색(머리 하이라이트는 원래 흰색으로)
    pal = np.array(sorted({tuple(c) for c in a[a[..., 3] > 0][:, :3]} | set(HAT.values()) | {INK}), int)
    m = top[..., 3] > 0
    near = pal[((top[..., None, :3].astype(int) - pal) ** 2).sum(-1).argmin(-1)]
    top[m, :3] = near[m]
    hi = (a[:SIDE_CUT, :, :3] == (252, 252, 252)).all(-1)
    top[pad:][hi & m[pad:]] = (252, 252, 252, 255)
    return out, pad


def build(view, gray):
    a = np.asarray(Image.open(os.path.join(HERE, 'wombat_%s_base.png' % view)).convert('RGBA'))[::2, ::2].copy()
    for src, dst in gray.items():
        a[(a[..., :3] == src).all(-1) & (a[..., 3] > 0), :3] = dst
    if view == 'side':
        return side(a)
    w = len(PUFF[0])
    x0 = CENTER - w // 2
    half = (w - 1) / 2
    bottom = head_top(a, CENTER) + SINK                   # 띠 아랫선(가장자리) 줄
    # 띠 아랫선: 가장자리 bottom, 가운데 bottom + 1
    low = [bottom + (1 if abs(x - half) < half * 0.6 else 0) for x in range(w)]
    top_row = bottom - BAND + 1 - len(PUFF)             # 부푼 윗부분 첫 줄
    pad = max(0, 1 - top_row)                           # 윗 외곽선까지 들어갈 줄
    out = np.zeros((a.shape[0] + pad, a.shape[1], 4), np.uint8)
    out[pad:] = a
    hat = np.zeros_like(out)
    for y, row in enumerate(PUFF):
        for x, ch in enumerate(row):
            if ch != '.':
                hat[pad + top_row + y, x0 + x] = HAT[ch] + (255,)
    for x in range(w):
        for y in range(bottom - BAND + 1, low[x] + 1):
            # 띠: 위아래 그늘 줄 사이 하양, 오른쪽 끝 열은 그늘
            col = HAT['g'] if (y == bottom - BAND + 1 or y == low[x] or x == w - 1) else HAT['w']
            hat[pad + y, x0 + x] = col + (255,)
    f = hat[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    hat[g & ~f] = INK + (255,)
    m = hat[..., 3] > 0
    out[m] = hat[m]
    return out, pad


if __name__ == '__main__':
    from make_anim import GRAY
    for view in ('front', 'back', 'side'):
        a, pad = build(view, GRAY[1])
        Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(os.path.join(HERE, 'clerk_%s_base.png' % view))
        print('clerk_%s_base' % view, a.shape[1], 'x', a.shape[0], 'pad', pad)
