# 별 평가 별 조각(설계 40, 2026-10-02 사용자 선택 C 「메달 별」, 시안 A 유물 별 결 · B 통통한 별은 버림).
# 19×18칸: 다섯 뿔 별(바깥 반지름 8.4 · 안 3.9) + 아래 · 오른쪽 한 톤 그늘 + 안쪽에 한 톤 밝은 작은 별(돋을새김) + 꼭대기 반짝 한 칸 + 바깥 1칸 외곽선.
# 단계: 빈 별 · 갈(★1~5) · 은(6~10) · 금(11~15) · 무지개(16~, 빨 · 주 · 노 · 초 · 파 · 보 파스텔 띠). 소식지 · 평가 결과 · 카드 · 간판 · 오너 알약이 같은 그림을 쓴다.
# 출력 ../star_<단계>.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_stars.py → 메뉴 Import UI Sprites
import json
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
INK = (52, 32, 32)
TIERS = {   # 바탕 · 그늘 · 밝은 색
    'empty': ((214, 202, 188), (186, 172, 158), (232, 222, 210)),
    'brown': ((182, 122, 80), (140, 90, 58), (220, 170, 126)),
    'silver': ((192, 198, 206), (144, 152, 164), (238, 242, 246)),
    'gold': ((240, 190, 72), (198, 144, 46), (252, 228, 150)),
}
RAINBOW = [(224, 112, 100), (236, 166, 82), (238, 210, 98), (118, 182, 122), (102, 152, 214), (162, 114, 202)]
W, H = 19, 18


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def star_mask(r_out, r_in, cy):
    im = Image.new('L', (W, H), 0)
    cx = (W - 1) / 2
    pts = []
    for k in range(10):
        ang = -np.pi / 2 + k * np.pi / 5
        r = r_out if k % 2 == 0 else r_in
        pts.append((cx + r * np.cos(ang), cy + r * np.sin(ang)))
    ImageDraw.Draw(im).polygon(pts, fill=255)
    return np.asarray(im) > 0


def shade(mask):
    below = np.zeros_like(mask); below[:-1] = mask[1:]
    right = np.zeros_like(mask); right[:, :-1] = mask[:, 1:]
    return mask & (~below | ~right)


def star(tier):
    m = star_mask(8.4, 3.9, 9.6)
    a = np.zeros((H, W, 4), np.uint8)
    if tier == 'rainbow':
        ys = np.nonzero(m.any(1))[0]
        top, bot = ys.min(), ys.max()
        for y in range(H):
            a[y][m[y]] = RAINBOW[min(len(RAINBOW) - 1, int((y - top) * len(RAINBOW) / (bot - top + 1)))] + (255,)
        sh = shade(m)
        a[sh, :3] = (a[sh, :3].astype(int) * 0.78).astype(np.uint8)
    else:
        a[m] = TIERS[tier][0] + (255,)
        a[shade(m)] = TIERS[tier][1] + (255,)
    inner = star_mask(4.6, 2.2, 9.9)
    if tier == 'rainbow':
        a[inner, :3] = np.minimum(255, a[inner, :3].astype(int) + 40).astype(np.uint8)
    else:
        a[inner] = TIERS[tier][2] + (255,)
        a[inner & shade(inner)] = TIERS[tier][0] + (255,)
    a[4, 9] = (255, 255, 250, 255)
    return outline(a)


path = os.path.join(OUT, 'ui_slices.json')
slices = json.load(open(path, encoding='utf-8'))
for tier in ('empty', 'brown', 'silver', 'gold', 'rainbow'):
    Image.fromarray(star(tier)).save(os.path.join(OUT, 'star_%s.png' % tier))
    slices['star_' + tier] = {'w': W, 'h': H, 'border': None}
json.dump(slices, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('star_empty · brown · silver · gold · rainbow', W, 'x', H)
