# 설계 31 유물 아이콘 아홉(2026-10-01 사용자 선택 C 「금빛 테두리」): 팝업(카드 4배 · 상단 2배)과 월드(오븐 옆 · 계산대 위 · 계단 들보 아래)에 같은 그림.
#   Codex 3×3 시트 raw/relics_c_gold.png(돌 메달 시트 raw/relics_b_medal.png에서 메달을 지우고 금빛 테두리를 두르게 편집, 프롬프트 raw/relics_prompt.txt).
#   원본 격자 그대로 옮긴다(World/Source~/snap_codex.py). 편집본은 메달 시트와 픽셀 크기가 같아 메달 시트의 주기(중앙값)를 쓰고 시작점만 아이콘마다 찾는다
#   (편집본에서 자동으로 찾으면 더 잘게 잡는다). 풍경이 꼬리표까지 세로 26칸이라 칸은 26×26(축복 아이콘 24보다 2칸 큼), 가운데에 놓는다.
# 출력: Assets/Resources/Sprites/Relics/<id>.png(한 칸 1px). 실행: Windows Python(Pillow · numpy) make_relic_icons.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
OUT = os.path.join(HERE, '..', '..', '..', 'Resources', 'Sprites', 'Relics')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'World', 'Source~'))
import snap_codex  # noqa: E402
IDS = ['bellows', 'bell', 'watering_can', 'chime', 'lucky_coin', 'abacus', 'scoop', 'basket', 'clock']   # 시트 왼쪽 위부터
N = 26


def bands(ink, axis):
    # 잉크 구간을 가장 큰 빈 틈 둘로 셋으로 나눈다(3×3 시트의 줄 · 칸)
    idx = np.nonzero(ink.any(axis))[0]
    gaps = sorted(((b - a, a, b) for a, b in zip(idx[:-1], idx[1:]) if b - a > 1), reverse=True)[:2]
    cuts = sorted((a + b) // 2 for _, a, b in gaps)
    edges = [idx.min()] + cuts + [idx.max() + 1]
    return list(zip(edges[:-1], edges[1:]))


def crops(name):
    im = Image.open(os.path.join(RAW, name)).convert('RGB')
    ink = np.asarray(im.convert('L')) < 235
    paths = []
    for r0, r1 in bands(ink, 1):
        for c0, c1 in bands(ink, 0):
            p = os.path.join(HERE, '_relic_tmp_%d.png' % len(paths))
            im.crop((max(c0 - 12, 0), max(r0 - 12, 0), min(c1 + 12, im.width), min(r1 + 12, im.height))).save(p)
            paths.append(p)
    return paths


def main():
    os.makedirs(OUT, exist_ok=True)
    medal = crops('relics_b_medal.png')
    try:
        period = float(np.median([snap_codex.snap(p, 12, square=True, min_hole=40)[1][0] for p in medal]))
    finally:
        for p in medal:
            os.remove(p)
    gold = crops('relics_c_gold.png')
    try:
        for k, p in zip(IDS, gold):
            g = np.asarray(Image.open(p).convert('RGB')).astype(float).mean(2)
            px, phx = snap_codex.grid(np.abs(np.diff(g, axis=1)).sum(0), period, 0.02)
            py, phy = snap_codex.grid(np.abs(np.diff(g, axis=0)).sum(1), period, 0.02)
            cells, _ = snap_codex.snap(p, fixed=(px, phx, py, phy), min_hole=40, raw=True)   # 원본 그대로(2026-10-05)
            h, w = cells.shape[:2]
            assert h <= N and w <= N, (k, w, h)
            out = np.zeros((N, N, 4), np.uint8)
            out[(N - h) // 2:(N - h) // 2 + h, (N - w) // 2:(N - w) // 2 + w] = cells
            Image.fromarray(out).save(os.path.join(OUT, k + '.png'))
            print(k, w, 'x', h)
    finally:
        for p in gold:
            os.remove(p)


if __name__ == '__main__':
    main()
