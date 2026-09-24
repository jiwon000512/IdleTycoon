# -*- coding: utf-8 -*-
# 굴 환경 A2(2026-09-24): 윗벽 지층 띠가 보이는 방의 재료 세 장을 Codex 재료 시트에서 잘라 칸 격자로 만든다.
#   시트: shop_raw/burrow_mat_{b,c}.png (프롬프트 burrow_mat_prompt_{b,c}.txt). 위 = 긴 지층 띠, 아래 왼쪽 = 바깥 흙, 아래 오른쪽 = 입구
#   사용자 선택: 입구 = B, 지층 띠·바깥 흙 = C
#   ../wall_face.png 64×40  한 칸 = 1px(PPU 40). 긴 띠에서 좌우 이음이 가장 매끄러운 64칸 구간, 맨 아래 한 줄은 바닥에 닿는 그늘. 가로로만 이어 붙음
#   ../wall_tile.png 32×32  한 칸 = 1px. 바깥 흙(흙 배경 Tiled 스프라이트로도 씀)
#   ../arch.png      60×49칸 × 2px(PPU 80). 선 1 + 턱 5~6 + 선 1, 밑변 = 띠 밑변(피벗 아래 가운데는 ShopBaker)
# 방 둘레 턱은 BurrowPainter.k_Ledge가 칠하고, 색은 입구 턱 (168,120,96)에 맞춘다
# 각 재료는 7색 이하(드문 색부터 가장 가까운 색에 합침). 실행은 Windows Python(Pillow·numpy)
import os, subprocess, sys, tempfile
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'shop_raw')
TMP = tempfile.mkdtemp()
MAKE_PIXEL = os.path.join(HERE, '..', '..', 'Source~', 'make_pixel.py')


def crops(sheet):
    # 흰 바탕에서 세 덩어리: 위 띠(행 구간), 아래 두 개(열 구간으로 나눔)
    im = Image.open(sheet).convert('RGB')
    ink = np.asarray(im).astype(int).sum(2) < 720
    rows = np.where(ink.sum(1) > 5)[0]
    cut = np.where(np.diff(rows) > 20)[0][0]
    top, bot = (rows[0], rows[cut] + 1), (rows[cut + 1], rows[-1] + 1)
    cols = np.where(ink[top[0]:top[1]].sum(0) > 0)[0]
    out = [(cols[0], top[0], cols[-1] + 1, top[1])]
    cols = np.where(ink[bot[0]:bot[1]].sum(0) > 0)[0]
    gap = np.where(np.diff(cols) > 20)[0][0]
    for c0, c1 in ((cols[0], cols[gap] + 1), (cols[gap + 1], cols[-1] + 1)):
        rr = np.where(ink[bot[0]:bot[1], c0:c1].sum(1) > 0)[0]
        out.append((c0, bot[0] + rr[0], c1, bot[0] + rr[-1] + 1))
    return im, out


def pixel(im, box, name, grid):
    src, dst = os.path.join(TMP, name + '_raw.png'), os.path.join(TMP, name + '_px.png')
    im.crop(box).save(src)
    subprocess.run([sys.executable, MAKE_PIXEL, src, dst, '--px=1', grid, '--th=0.001'], check=True, stdout=subprocess.DEVNULL)
    return np.asarray(Image.open(dst).convert('RGBA')).copy()


def reduce(a, n=7):
    opaque = a[..., 3] > 0
    while True:
        cols, cnt = np.unique(a[opaque][:, :3], axis=0, return_counts=True)
        if len(cols) <= n:
            return a
        i = np.argmin(cnt)
        rest = np.delete(cols, i, 0)
        near = rest[np.argmin(((rest.astype(int) - cols[i].astype(int)) ** 2).sum(1))]
        a[opaque & (a[..., :3] == cols[i]).all(2), :3] = near


imb, (_, _, arch_box) = crops(os.path.join(RAW, 'burrow_mat_b.png'))
imc, (strip_box, earth_box, _) = crops(os.path.join(RAW, 'burrow_mat_c.png'))

strip = pixel(imc, strip_box, 'c_strip', '--cells=40')
c0 = min(range(strip.shape[1] - 64), key=lambda c: np.abs(strip[:, c + 64, :3].astype(int) - strip[:, c, :3]).sum())
face = strip[:, c0:c0 + 64]
face[39, :, :3] = (84, 48, 36)
face[..., 3] = 255
Image.fromarray(reduce(face)).save(os.path.join(HERE, '..', 'wall_face.png'))

earth = pixel(imc, earth_box, 'c_earth', '--cellsw=32')
earth[..., 3] = 255
Image.fromarray(reduce(earth)).save(os.path.join(HERE, '..', 'wall_tile.png'))

arch = reduce(pixel(imb, arch_box, 'b_arch', '--cellsw=60'))
Image.fromarray(np.repeat(np.repeat(arch, 2, 0), 2, 1)).save(os.path.join(HERE, '..', 'arch.png'))
print('wall_face 64x40 (window', c0, ') wall_tile 32x32 arch', arch.shape[1], 'x', arch.shape[0], 'cells')
