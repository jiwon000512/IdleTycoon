# 낚싯대(2026-10-05 사용자 선택 C 「통통한 대」, 프롬프트 raw/prompt_rod.txt).
# 원본 raw/rod_rods.png(대나무 · 쇠 · 미끼) · rod_specials.png(도르래탑 · 등대 · 소용돌이 통발) · rod_bent.png(월척에 휜 대, 공용):
#   실제 말뚝을 그려 넣은 바탕(raw/rod_template_*.png) 위에 그리게 해서, 원본 격자 그대로 옮기면 말뚝이 실제와 같은 16칸이 된다.
#   1. 원본 격자 그대로 칸으로(snap_codex). 2. 말뚝: 발끝(맨 아랫줄)에서 17칸 위(꽂는 자리)보다 아래, 말뚝 가운데 ±9칸을 지운다
#   3. 8방향으로 이어진 덩어리로 나누고 떨어진 조각(대 끝 휨 · 물방울)은 가까운 덩어리에, 밑동이 가까운 말뚝 순서로 이름을 붙인다
#   4. 등급: 릴 · 띠 4색(FIT)을 2등급 은(SILVER) · 3등급 금(GOLD)으로, 3등급은 대 끝 아래에 금방울(3×4칸)
# 출력: ../rod_<bamboo|iron|bait>_<1|2|3>.png · ../rod_<pulley|lighthouse|whirlpool>.png · ../rod_bent.png(한 칸 2px · PPU 80)
#   꽂는 자리(밑동, 피벗으로 쓸 점)와 대 끝(줄이 나오는 점)을 그림 왼쪽 아래 기준 칸으로 출력한다.
#   대는 말뚝 뒤에 그린다(말뚝이 앞에서 밑동 · 남은 말뚝 테를 덮어 구멍에 꽂힌 모습). 사용: python make_rods.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

FIT = [(237, 207, 177), (196, 169, 142), (154, 126, 104), (110, 82, 67)]
SILVER = [(240, 244, 248), (196, 206, 216), (146, 158, 172), (98, 108, 124)]
GOLD = [(252, 234, 150), (240, 196, 76), (206, 150, 44), (150, 100, 34)]
LINE = (52, 32, 32)
SOCKET_UP = 17


def cut(path, names):
    """{이름: (칸 배열, 꽂는 자리 (x, y 위에서), 대 끝 (x, y 위에서))}"""
    cells, _ = snap_codex.snap(path, 12, square=True, min_hole=40)
    op = cells[..., 3] > 0
    foot = np.nonzero(op.any(1))[0].max()
    sock = foot - SOCKET_UP
    xs = np.nonzero(op[foot - 14])[0]
    runs, s = [], xs[0]
    for i in range(1, len(xs)):
        if xs[i] - xs[i - 1] > 1:
            runs.append((s, xs[i - 1])); s = xs[i]
    runs.append((s, xs[-1]))
    centers = sorted((a + b) // 2 for a, b in sorted(runs, key=lambda r: r[1] - r[0], reverse=True)[:len(names)])
    clean = cells.copy()
    for c in centers:
        clean[sock + 1:, max(c - 9, 0):c + 10] = 0
    groups = sorted(blobs(clean[..., 3] > 0, diag=True), key=len, reverse=True)
    main, rest = groups[:len(names)], groups[len(names):]
    for gr in rest:
        best = min(range(len(main)), key=lambda i: min(abs(gr[0][0] - a) + abs(gr[0][1] - b) for a, b in main[i][::3]))
        main[best] = main[best] + gr
    owner = {}
    for gr in main:
        yb = max(y for y, _ in gr)
        xb = np.mean([x for y, x in gr if y >= yb - 1])
        owner[min(range(len(centers)), key=lambda i: abs(centers[i] - xb))] = gr
    out = {}
    for i, c in enumerate(centers):
        gr = owner[i]
        ys = [y for y, _ in gr]; xs2 = [x for _, x in gr]
        crop = np.zeros((max(ys) - min(ys) + 1, max(xs2) - min(xs2) + 1, 4), np.uint8)
        for y, x in gr:
            crop[y - min(ys), x - min(xs2)] = clean[y, x]
        top = np.nonzero(crop[0, :, 3] > 0)[0]
        out[names[i]] = (crop, (c - min(xs2), sock - min(ys)), (int(top.max()), 0))
    return out


def recolor(a, to):
    a = a.copy()
    for f, t in zip(FIT, to):
        a[(a[..., :3] == f).all(-1) & (a[..., 3] > 0), :3] = t
    return a


def bell(a, tip):
    tx = tip[0]
    pad = np.zeros((a.shape[0] + 6, a.shape[1] + 2, 4), np.uint8)
    pad[:a.shape[0], :a.shape[1]] = a
    pad[1, tx] = LINE + (255,)
    for dy, row in enumerate(['.A.', 'AYA', 'AYA', 'AAA', '.Y.']):
        for dx, ch in enumerate(row):
            if ch != '.':
                pad[2 + dy, tx - 1 + dx] = (LINE if ch == 'A' else GOLD[1]) + (255,)
    ys, xs = np.nonzero(pad[..., 3] > 0)
    return pad[:ys.max() + 1, :xs.max() + 1]


def save(name, a, sock, tip):
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', name + '.png'))
    h = a.shape[0]
    print('%-18s %2d×%2d칸  꽂는 자리(왼쪽 아래 기준) %4.1f, %4.1f  대 끝 %4.1f, %4.1f' % (name, a.shape[1], h, sock[0], h - 1 - sock[1], tip[0], h - 1 - tip[1]))


if __name__ == '__main__':
    rods = cut(os.path.join(HERE, 'raw', 'rod_rods.png'), ['bamboo', 'iron', 'bait'])
    for n, (a, sock, tip) in rods.items():
        save('rod_%s_1' % n, a, sock, tip)
        save('rod_%s_2' % n, recolor(a, SILVER), sock, tip)
        g = bell(recolor(a, GOLD), tip)
        save('rod_%s_3' % n, g, (sock[0], sock[1] + g.shape[0] - a.shape[0]), (tip[0], tip[1] + g.shape[0] - a.shape[0]))
    for n, (a, sock, tip) in cut(os.path.join(HERE, 'raw', 'rod_specials.png'), ['pulley', 'lighthouse', 'whirlpool']).items():
        save('rod_' + n, a, sock, tip)
    a, sock, _ = cut(os.path.join(HERE, 'raw', 'rod_bent.png'), ['bent'])['bent']
    xr = int(np.nonzero((a[..., 3] > 0).any(0))[0].max())          # 휜 대 끝 = 맨 오른쪽 열의 가장 아래 칸(아래로 당겨진 끝)
    save('rod_bent', a, sock, (xr, int(np.nonzero(a[:, xr, 3] > 0)[0].max())))
