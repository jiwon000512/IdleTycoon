# -*- coding: utf-8 -*-
# 설계 59 편지 보낸 이 도장(편지 카드 FishingNewsView 그림 칸, 경로 Sprites/Letters/<from>). 2026-10-12 사용자 선택 테 B 「우표」
#   (버린 테: A 소인 동그란 잉크 두 줄 · C 물결 귤색 꽃테). 테는 칸 무늬로 그리고(도형), 안 그림은 Codex 시안을 원본 그대로 옮긴다(snap_codex raw 격자).
#   그림: raw/stamp_pics_b.png(왼쪽 점원은 크기 참조, 가운데 큰이모 · 오른쪽 마을 회관) · raw/stamp_agency_c.png(오른쪽 일자리 알선소, 시안 없이 리드 요청).
#   시안 격자 둘레의 흐린 회색 칸(안티에일리어싱 찌꺼기)만 걷는다. 그림은 창 가운데.
# 출력(한 칸 = 2px, 58×58칸 = 116px, 코드가 ×2): ../aunt.png · ../hall.png · ../agency.png(테까지 한 장) · ../stamp_ring.png(빈 테, 점원 편지는 코드가 점원 그림을 얹는다)
# 사용: Windows Python(Pillow · numpy) make_stamps.py → 에디터에서 Point · 비압축으로 가져오기
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
sys.path.insert(0, os.path.join(HERE, '..', '..', '..', '..', 'Sprites', 'World', 'Source~'))
import snap_codex  # noqa: E402

N = 58
INK = (52, 32, 32, 255)
PAPER = (255, 253, 246, 255)
WINDOW_LINE = (143, 183, 168, 255)
WINDOW = (220, 235, 226, 255)
# (출력, 시안, 가로 자리(비율), 격자 주기 px)
PICS = [
    ('aunt', 'stamp_pics_b.png', (0.33, 0.65), 12.97),
    ('hall', 'stamp_pics_b.png', (0.65, 1.0), 12.97),
    ('agency', 'stamp_agency_c.png', (0.67, 1.0), None),
]


def ring():
    # 우표: 네모 종이 + 가장자리 톱니 구멍(4칸마다 2칸) + 진갈색 둘레 + 안쪽 민트 창(4칸 안)
    img = np.zeros((N, N, 4), np.uint8)
    img[:, :] = PAPER
    for i in range(2, N - 2, 4):
        for e in (0, N - 1):
            img[e, i:i + 2] = 0
            img[i:i + 2, e] = 0
    on = img[..., 3] > 0
    pad = np.pad(on, 1)
    img[on & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])] = INK
    img[3:N - 3, 3:N - 3] = WINDOW_LINE
    img[4:N - 4, 4:N - 4] = WINDOW
    return img


def halo(c):
    while True:
        on = c[..., 3] > 0
        pad = np.pad(~on, 1)
        touch = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
        rgb = c[..., :3].astype(int)
        bad = on & touch & (rgb.min(2) > 150) & (rgb.max(2) - rgb.min(2) < 16)
        if not bad.any():
            return c
        c[bad] = 0


def picture(src, span, cell):
    im = Image.open(os.path.join(HERE, 'raw', src))
    tmp = os.path.join(HERE, 'raw', '_cut.png')
    im.crop((int(span[0] * im.width), 0, int(span[1] * im.width), im.height)).save(tmp)
    c = halo(snap_codex.snap(tmp, cell=cell, square=True, min_hole=40, raw=True)[0])
    os.remove(tmp)
    ys, xs = np.nonzero(c[..., 3])
    return c[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def put(base, pic):
    img = base.copy()
    h, w = pic.shape[:2]
    y0, x0 = (N - h) // 2, (N - w) // 2
    on = pic[..., 3] > 0
    img[y0:y0 + h, x0:x0 + w][on] = pic[on]
    return img


def save(name, cells):
    Image.fromarray(np.repeat(np.repeat(cells, 2, 0), 2, 1)).save(os.path.join(OUT, name + '.png'))


if __name__ == '__main__':
    base = ring()
    save('stamp_ring', base)
    for name, src, span, cell in PICS:
        pic = picture(src, span, cell)
        save(name, put(base, pic))
        print('%s.png 그림 %d x %d칸' % (name, pic.shape[1], pic.shape[0]))
