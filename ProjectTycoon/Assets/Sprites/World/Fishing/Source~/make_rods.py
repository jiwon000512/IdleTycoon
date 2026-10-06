# 낚싯대(2026-10-05). 1차 C 「통통한 대」(raw/rod_style_*)는 사용자 「대가 너무 큼 · 말뚝과 조화가 안 됨 · 가만히 서 있음」(설계 45)으로 다시:
#   다시 시안 raw/rod_redo_style_a~c 중 사용자 선택 A 「말뚝에 맞춘 대」(대 밑동의 밧줄 깃이 말뚝 구멍에 앉음), 꽂는 자리에서 대 끝까지 약 0.9유닛.
# 원본 raw/rod_rods.png(대나무 · 쇠 · 미끼 · 월척에 휜 대) · rod_specials.png(도르래탑 · 등대 · 소용돌이 통발):
#   웜뱃과 실제 말뚝을 한 칸 10px로 그려 넣은 바탕(raw/rod_template_*.png) 위에 그리게 해서, 원본 격자 그대로 옮기면 실제 칸 크기가 된다.
#   1. 원본 그대로 칸으로(snap_codex raw: 색 합치기 · 외곽선 바꾸기 없음, 2026-10-05 사용자 「그림 퀄리티에 변형이 생기는 규칙은 다 지운다」). 크기 기준 웜뱃(맨 왼쪽 덩어리)은 뺀다
#   2. 말뚝: 발끝(맨 아랫줄)에서 17칸 위(꽂는 자리)보다 아래, 말뚝 가운데 ±9칸을 지운다
#   3. 8방향으로 이어진 덩어리로 나누고 떨어진 조각은 가까운 덩어리에, 밑동이 가까운 말뚝 순서로 이름을 붙인다
#   4. 등급: 채도 낮은 색(릴 · 띠 · 쇠)을 밝기 그대로 2등급 은(SILVER) · 3등급 금(GOLD) 띠 위로 옮긴다(단계로 뭉치지 않게 이어서), 3등급은 대 끝 아래 금방울(3×4칸)
#   5. 당김 판: 꽂는 자리 위 줄을 끝으로 갈수록 오른쪽으로 0~2칸 밀어(제곱) 살짝 휜 판(감는 동안 곧은 판과 번갈아)
# 출력(한 칸 2px · PPU 80): ../rod_<bamboo|iron|bait>_<1|2|3>.png · _pull.png 판 · ../rod_bent.png(월척) · ../rod_<pulley|lighthouse|whirlpool>.png
#   꽂는 자리(피벗) · 대 끝(줄이 나오는 점)을 그림 왼쪽 아래 기준 칸으로 출력한다.
#   대 밑동에는 원본의 말뚝 윗면 테와 밧줄 깃이 남아 있어, 게임은 대를 말뚝 앞에 그려 깃이 구멍에 꽂힌 모습이 된다. 사용: python make_rods.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

SILVER = [(240, 244, 248), (196, 206, 216), (146, 158, 172), (98, 108, 124)]
GOLD = [(252, 234, 150), (240, 196, 76), (206, 150, 44), (150, 100, 34)]
LINE = (52, 32, 32)
SOCKET_UP = 17


def cut(path, names):
    """{이름: (칸 배열, 꽂는 자리 (x, y 위에서), 대 끝 (x, y 위에서))}"""
    cells, _ = snap_codex.snap(path, square=True, min_hole=40, raw=True)
    op = cells[..., 3] > 0
    # 크기 기준으로 그려 넣은 웜뱃(맨 왼쪽 열에 닿는 덩어리)은 뺀다
    for gr in blobs(op, diag=True):
        if min(x for _, x in gr) == 0 and len(gr) > 400:
            for y, x in gr:
                cells[y, x] = 0
    op = cells[..., 3] > 0
    foot = np.nonzero(op.any(1))[0].max()
    sock = foot - SOCKET_UP
    xs = np.nonzero(op[foot - 14])[0]
    runs, s = [], xs[0]
    for i in range(1, len(xs)):
        if xs[i] - xs[i - 1] > 1:
            runs.append((s, xs[i - 1])); s = xs[i]
    runs.append((s, xs[-1]))
    # 꽂는 자리 = 말뚝 폭의 가운데 금(짝수 폭이면 가운데 두 칸 사이 = 오른쪽 칸 왼쪽 변). 게임 말뚝 피벗(아래 가운데)과 맞아야 대 밑동 테가 말뚝 테에 겹친다
    centers = sorted((a + b + 1) // 2 for a, b in sorted(runs, key=lambda r: r[1] - r[0], reverse=True)[:len(names)])
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


def recolor(a, ramp):
    """채도 낮은 색(외곽선 · 흰 빛 제외)을 밝기 그대로 ramp(밝음 → 어두움) 띠 위로: 가장 밝은 색 = ramp[0], 가장 어두운 색 = ramp[-1], 사이는 이어서"""
    a = a.copy()
    op = a[..., 3] > 0
    cols = {tuple(int(v) for v in c) for c in a[op][:, :3]}
    grey = [c for c in cols if 70 <= max(c) < 245 and (max(c) - min(c)) / max(c) < 0.3]
    if not grey:
        return a
    lum = {c: sum(c) for c in grey}
    hi, lo = max(lum.values()), min(lum.values())
    R = np.array(ramp, float)
    for c in grey:
        t = (hi - lum[c]) / max(hi - lo, 1) * (len(ramp) - 1)
        i = min(int(t), len(ramp) - 2)
        a[op & (a[..., :3] == c).all(-1), :3] = np.round(R[i] + (R[i + 1] - R[i]) * (t - i)).astype(np.uint8)
    return a


def pull(a, sock):
    """꽂는 자리 위 줄을 끝으로 갈수록 오른쪽으로 0~2칸(제곱)"""
    h, w = a.shape[:2]
    out = np.zeros((h, w + 2, 4), np.uint8)
    top = 0
    for y in range(h):
        up = sock[1] - y
        t = max(up, 0) / max(sock[1] - top, 1)
        dx = int(round(2 * t * t))
        out[y, dx:dx + w] = np.where(a[y, :, 3:4] > 0, a[y], out[y, dx:dx + w])
    return out


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
    rods = cut(os.path.join(HERE, 'raw', 'rod_rods.png'), ['bamboo', 'iron', 'bait', 'bent'])
    for n in ['bamboo', 'iron', 'bait']:
        a, sock, tip = rods[n]
        for g, img in ((1, a), (2, recolor(a, SILVER)), (3, bell(recolor(a, GOLD), tip))):
            dy = img.shape[0] - a.shape[0]
            s2, t2 = (sock[0], sock[1] + dy), (tip[0], tip[1] + dy)
            save('rod_%s_%d' % (n, g), img, s2, t2)
            p = pull(img, s2)
            tp = np.nonzero(p[0, :, 3] > 0)[0]
            save('rod_%s_%d_pull' % (n, g), p, s2, (int(tp.max()), 0))
    a, sock, _ = rods['bent']
    xr = int(np.nonzero((a[..., 3] > 0).any(0))[0].max())          # 휜 대 끝 = 맨 오른쪽 열의 가장 아래 칸(아래로 당겨진 끝)
    save('rod_bent', a, sock, (xr, int(np.nonzero(a[:, xr, 3] > 0)[0].max())))
    # 특별한 대(도르래탑 · 등대 · 통발, raw/rod_specials.png)는 만들지 않는다(2026-10-05 설계 46: 설치물 · 대물 · 대 들기 · 합치기를 없애 이 그림은 만들지 않는다). 효과는 미끼 노점 업그레이드
