# -*- coding: utf-8 -*-
# 손에 드는 낚싯대(2026-10-07 아트방). 설계 52 손맛 낚시: 웜뱃이 앞발에 대를 들고 강에 던진다(FishingView가 손 자리에 그리고, 왼쪽을 보면 flipX).
#   옛 대(2026-10-05, 말뚝에 꽂는 대: 밑동 말뚝 테 · 밧줄 깃 · 릴, Codex 원본 raw/rod_*.png)는 손에 들면 손잡이가 손에 안 오고 밑동이 잘려 버렸다. 그림은 raw에 남기고 스크립트는 새로 썼다.
#   가는 막대는 아트 규칙대로 Codex 대신 칸 무늬로 그린다: 대 축을 호 길이로 따라가며 칸 가운데가 축에서 w 안이면 대, w + 1 안이면 외곽선.
#   디자인: 가늘고 긴 대나무 대(마디 몇 개), 밑동 8칸은 짙은 끈 감기(2칸 줄무늬), 진갈색 외곽선 1칸, 파스텔 평면(왼쪽 위 밝은 면 · 오른쪽 아래 한 톤 어두운 면).
#   오른쪽 위로 기운 대각선(수평에서 65~70°), 밑동에서 끝까지 40~48칸. 릴 · 말뚝 테 없음 — 앞 · 옆 어디서 봐도 같은 막대.
#   피벗 = 손이 쥐는 점 = 밑동에서 위로 5칸(GRIP_UP), 축 위. 세 장(곧음 · 당김 · 휨) 모두 밑동~손잡이는 같고 그 위만 휜다.
#   시안 셋(기울기 · 굵기 · 손잡이): A 68° · 2→1칸 가늘어지는 대 · 끈 감기 / B 65° · 1칸 가는 대 · 두꺼운 끈 손잡이 / C 70° · 2칸 굵은 대 · 코르크 손잡이. 아트방 선택 PICK.
# 출력(한 칸 2px · PPU 80): ../rod_bamboo_1.png(곧음) · ../rod_bamboo_1_pull.png(감을 때 살짝 휨) · ../rod_bent.png(물고기가 달릴 때 크게 휘어 끝이 물 쪽으로)
#   장마다 피벗 칸(그림 왼쪽 아래 기준 x, y)과 대 끝(줄이 나오는 점, 피벗 기준 x, y 칸)을 찍는다 → BakeryBaker.RodGrips · FishingView.k_RodTips
# 사용: python make_rods.py [미리보기 폴더]   미리보기 폴더를 주면 세 시안 × 세 장을 6배로 나란히(피벗 빨간 점)
import math
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
LINE = (52, 32, 32)
BAMBOO = {'hi': (232, 212, 160), 'mid': (206, 180, 122), 'lo': (166, 138, 88), 'node': (128, 98, 60)}
CORD = ((92, 60, 46), (144, 104, 74))         # 끈 감기 두 색(2칸 줄무늬)
CORK = ((214, 172, 124), (176, 132, 92))      # 코르크 손잡이 두 색
GRIP_UP = 5
BEND = {'': 0.0, '_pull': 10.0, 'bent': 88.0}  # 끝까지 도는 각(°): 곧음 · 당김 · 휨. 손잡이 위에서 끝으로 갈수록(1.6제곱) 돈다
VARIANTS = {
    'a': dict(angle=68, length=44, w0=1.0, w1=0.5, grip=8, grip_w=1.0, nodes=(15, 25, 35), cord=CORD, label='A 가늘어지는 대 · 끈 손잡이 68°'),
    'b': dict(angle=65, length=46, w0=0.5, w1=0.5, grip=8, grip_w=1.0, nodes=(14, 23, 32, 41), cord=CORD, label='B 가는 대 · 두꺼운 끈 손잡이 65°'),
    'c': dict(angle=70, length=42, w0=1.0, w1=1.0, grip=8, grip_w=1.0, nodes=(13, 20, 27, 34), cord=CORK, label='C 굵은 대 · 코르크 손잡이 70°'),
}
PICK = 'a'


def axis(v, bend_deg):
    """대 축을 호 길이 0.05칸마다: (점 (x, y), 호 길이 s, 방향 단위 벡터). 밑동 = (0, 0), y는 위가 +"""
    th = math.radians(v['angle'])
    L, s0 = v['length'], v['grip']
    pts, ss, dirs = [], [], []
    x = y = 0.0
    ds = 0.05
    s = 0.0
    while s <= L + 1e-9:
        t = max(s - s0, 0.0) / (L - s0)
        phi = th - math.radians(bend_deg) * t ** 1.6
        d = (math.cos(phi), math.sin(phi))
        pts.append((x, y)); ss.append(s); dirs.append(d)
        x += d[0] * ds; y += d[1] * ds; s += ds
    return np.array(pts), np.array(ss), np.array(dirs)


def width(v, s):
    if s <= v['grip']:
        return v['grip_w']
    t = (s - v['grip']) / (v['length'] - v['grip'])
    return v['w0'] + (v['w1'] - v['w0']) * t


def draw(v, bend_deg):
    """(칸 배열 RGBA, 피벗 칸 (x, y 아래 기준), 대 끝 칸 (x, y 아래 기준))"""
    pts, ss, dirs = axis(v, bend_deg)
    m = 3.0
    x0, y0 = pts[:, 0].min() - m, pts[:, 1].min() - m
    W = int(math.ceil(pts[:, 0].max() + m - x0)); H = int(math.ceil(pts[:, 1].max() + m - y0))
    cy, cx = np.mgrid[0:H, 0:W]
    cen = np.stack([cx + 0.5 + x0, cy + 0.5 + y0], -1).reshape(-1, 2)          # 칸 가운데(축 좌표, y 위가 +)
    d2 = ((cen[:, None, :] - pts[None, :, :]) ** 2).sum(-1)
    i = d2.argmin(1)
    d = np.sqrt(d2[np.arange(len(cen)), i])
    s = ss[i]
    rel = cen - pts[i]
    side = dirs[i][:, 0] * rel[:, 1] - dirs[i][:, 1] * rel[:, 0]                # +: 진행 방향 왼쪽(왼쪽 위 = 빛 받는 면)
    w = np.array([width(v, q) for q in s])
    fill = d <= w + 1e-6
    line = ~fill & (d <= w + 1.0)
    img = np.zeros((H * W, 4), np.uint8)
    img[line] = LINE + (255,)
    for k in np.nonzero(fill)[0]:
        q = s[k]
        if q <= v['grip']:
            c = v['cord'][int(q // 2) % 2]
        elif any(abs(q - n) <= 0.5 for n in v['nodes']):
            c = BAMBOO['node']
        elif w[k] < 0.9:
            c = BAMBOO['mid']
        elif side[k] < 0:
            c = BAMBOO['lo']
        elif v['grip'] + 2 <= q <= v['grip'] + 7:
            c = BAMBOO['hi']                                                    # 손잡이 위 빛 한 획
        else:
            c = BAMBOO['mid']
        img[k] = c + (255,)
    img = img.reshape(H, W, 4)[::-1]                                             # 그림 좌표(위가 0)
    def cell(p):
        return int(math.floor(p[0] - x0)), int(math.floor(p[1] - y0))
    pivot = cell(pts[np.abs(ss - GRIP_UP).argmin()])
    tip = cell(pts[-1])
    # 여백 자르기
    ys, xs = np.nonzero(img[..., 3])
    t, b, l, r = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    img = img[t:b, l:r]
    hh = img.shape[0]
    fix = lambda c: (c[0] - l, c[1] - (H - b))                                   # 아래 기준 y는 아래 여백만 뺀다
    return img, fix(pivot), fix(tip)


def save(a, path):
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1), 'RGBA').save(path)


def make(k):
    v = VARIANTS[k]
    out = {}
    for suffix, bend in BEND.items():
        name = 'rod_bent' if suffix == 'bent' else 'rod_bamboo_1' + suffix
        out[name] = draw(v, bend)
    return out


def report(name, a, pivot, tip):
    print('%-18s %2d×%2d칸  피벗(왼쪽 아래 기준) %2d, %2d  대 끝(피벗 기준) %+3d, %+3d' % (name, a.shape[1], a.shape[0], pivot[0], pivot[1], tip[0] - pivot[0], tip[1] - pivot[1]))


def preview(folder):
    os.makedirs(folder, exist_ok=True)
    S = 6
    for k in VARIANTS:
        rods = make(k)
        W = sum(a.shape[1] + 4 for a, _, _ in rods.values())
        H = max(a.shape[0] for a, _, _ in rods.values()) + 4
        canvas = Image.new('RGBA', (W * S, H * S), (120, 160, 200, 255))
        x = 2
        for name, (a, pivot, tip) in rods.items():
            im = Image.fromarray(a, 'RGBA').resize((a.shape[1] * S, a.shape[0] * S), Image.NEAREST)
            y = H - 2 - a.shape[0]
            canvas.alpha_composite(im, (x * S, y * S))
            px, py = (x + pivot[0]) * S, (y + a.shape[0] - 1 - pivot[1]) * S
            tx, ty = (x + tip[0]) * S, (y + a.shape[0] - 1 - tip[1]) * S
            arr = np.asarray(canvas).copy()
            arr[py + 1:py + S - 1, px + 1:px + S - 1] = (220, 40, 40, 255)
            arr[ty + 1:ty + S - 1, tx + 1:tx + S - 1] = (40, 200, 60, 255)
            canvas = Image.fromarray(arr, 'RGBA')
            save(a, os.path.join(folder, '%s_%s.png' % (k, name)))
            x += a.shape[1] + 4
        canvas.save(os.path.join(folder, 'rods_%s.png' % k))
        print(k, VARIANTS[k]['label'])
        for name, (a, pivot, tip) in rods.items():
            report(name, a, pivot, tip)


if __name__ == '__main__':
    if len(sys.argv) > 1:
        preview(sys.argv[1])
    else:
        for name, (a, pivot, tip) in make(PICK).items():
            save(a, os.path.join(HERE, '..', name + '.png'))
            report(name, a, pivot, tip)
