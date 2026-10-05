# 낚시터 말뚝(2026-10-05 사용자 선택 A 「나무 말뚝」, 원본 raw/stake_a.png · 프롬프트 raw/prompt_stake.txt).
# 한 장에 그린 두 그림(왼쪽 열린 말뚝 · 오른쪽 잠긴 말뚝)을 원본 그대로 옮겨(같은 격자, snap_codex raw: 색 합치기 · 외곽선 바꾸기 없음) 빈 열에서 나눈다.
# 출력: ../stake.png · ../stake_locked.png(한 칸 2px · PPU 80, 피벗 아래 가운데 = 발끝). 낚싯대는 윗면 구멍(SOCKET, 발끝에서 칸)에 꽂는다
# 사용: python make_stakes.py   (Windows Python · Pillow · numpy)
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402


def halves(cells):
    """불투명 열 사이 가장 넓은 빈틈에서 둘로, 위아래 빈 줄은 자른다"""
    xs = np.nonzero(cells[..., 3].any(0))[0]
    gap = max(range(len(xs) - 1), key=lambda i: xs[i + 1] - xs[i])
    out = []
    for a, b in ((xs[0], xs[gap] + 1), (xs[gap + 1], xs[-1] + 1)):
        p = cells[:, a:b]
        ys = np.nonzero(p[..., 3].any(1))[0]
        out.append(p[ys.min():ys.max() + 1])
    return out


def socket(p):
    """윗면 구멍: 위쪽 절반에서 둘레가 모두 불투명한(안쪽) 가장 어두운 칸 덩어리의 가운데. (가로는 그림 가운데 기준, 세로는 발끝 = 아래 끝에서 위로 칸)"""
    op = p[..., 3] > 0
    pad = np.pad(op, 1)
    inner = op & pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    lum = p[..., :3].astype(int).sum(-1)
    top = inner.copy()
    top[p.shape[0] // 2:] = False
    hole = top & (lum < lum[top].min() + 60)          # 위쪽 절반 안쪽에서 가장 어두운 칸 둘레(구멍 바닥)
    ys, xs = np.nonzero(hole)
    near = ys <= ys.min() + 2          # 맨 위 구멍 덩어리만(아래 나무결 무늬의 어두운 칸은 뺀다)
    ys, xs = ys[near], xs[near]
    return (xs.mean() - (p.shape[1] - 1) / 2, p.shape[0] - 1 - ys.mean())


if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(HERE, 'raw', 'stake_a.png'), square=True, min_hole=40, raw=True)
    stake, locked = halves(cells)
    for name, p in (('stake', stake), ('stake_locked', locked)):
        Image.fromarray(np.repeat(np.repeat(p, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', name + '.png'))
        print(name, p.shape[1], 'x', p.shape[0], 'cells')
    sx, sy = socket(stake)
    print('socket from foot: x %+.1f cells, up %.1f cells' % (sx, sy))
