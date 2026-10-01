# 설계 31 · 32 유물 행상의 수레(2026-10-01 사용자: 좌판 C 「유리 진열장 수레」). 프롬프트 raw/relic_cart_prompt.txt
#   펼침(좌판): raw/relic_cart_c.png를 원본 격자 그대로 옮긴다(World/Source~/snap_codex.py). 수레는 색이 많아(나무 · 벨벳 · 유리 · 유물)
#   12색으로 줄일 때 오차가 가장 작은 짝부터 합치고(mean_merge), 흰 하이라이트는 구멍으로 뚫지 않는다(min_hole).
#   피벗 = 손잡이를 뺀 몸통 바닥 가운데: 손잡이 반대쪽에 투명 열을 붙여 그림 아래 가운데가 피벗이 되게 한다.
#   접힘(설계 32, 사용자 선택 A 「천 덮개」): raw/relic_cart_folded_a.png 한 장에 옆(손잡이 오른쪽) | 끝면(손잡이가 아래 = 행상 쪽).
#   한 시트는 픽셀 크기가 같아 두 그림 주기의 중앙값 하나로, 시작점만 그림마다 찾는다.
#   끝면은 손잡이 끝이 바퀴 바닥보다 2칸 아래라 피벗(바퀴 바닥 가운데)이 그림 바닥에서 2칸 위다(프로그래밍방이 피벗을 그 높이로 임포트).
# 출력: ../relic_cart_open.png · ../relic_cart_folded_side.png · ../relic_cart_folded_end.png(한 칸 2px · PPU 80)
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
OUT = os.path.join(HERE, '..')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2


def body_centered(cells, rows=12):
    # 바닥 rows줄(바퀴)의 가로 범위 가운데가 그림 가운데가 되게 투명 열을 붙인다. (그림, 몸통 폭) 반환
    cols = np.nonzero((cells[-rows:, :, 3] > 0).any(0))[0]
    mid = cols.min() + cols.max() + 1          # 몸통 가운데 × 2(칸 경계)
    left, right = mid, 2 * cells.shape[1] - mid
    pad = abs(left - right) // 2
    widths = ((0, 0), (pad, 0), (0, 0)) if right > left else ((0, 0), (0, pad), (0, 0))
    return np.pad(cells, widths), cols.max() - cols.min() + 1


def ink_runs(im):
    # 잉크가 있는 열 구간(왼쪽부터). 40px 안으로 붙은 조각은 하나로
    cols = (np.asarray(im.convert('L')) < 235).any(0)
    runs, x = [], 0
    while x < len(cols):
        if cols[x]:
            s = x
            while x < len(cols) and cols[x]:
                x += 1
            if runs and s - runs[-1][1] < 40:
                runs[-1] = (runs[-1][0], x)
            else:
                runs.append((s, x))
        x += 1
    return runs


def folded():
    im = Image.open(os.path.join(RAW, 'relic_cart_folded_a.png')).convert('RGB')
    runs = ink_runs(im)
    assert len(runs) == 2, runs
    tmp = os.path.join(HERE, '_cart_tmp_%d.png')
    paths = []
    for i, (x0, x1) in enumerate(runs):
        paths.append(tmp % i)
        im.crop((max(x0 - 20, 0), 0, min(x1 + 20, im.width), im.height)).save(paths[-1])
    try:
        period = float(np.median([snap_codex.snap(p, 12, square=True, min_hole=40, mean_merge=True)[1][0] for p in paths]))
        out = []
        for p in paths:
            g = np.asarray(Image.open(p).convert('RGB')).astype(float).mean(2)
            px, phx = snap_codex.grid(np.abs(np.diff(g, axis=1)).sum(0), period, 0.01)
            py, phy = snap_codex.grid(np.abs(np.diff(g, axis=0)).sum(1), period, 0.01)
            out.append(snap_codex.snap(p, 12, fixed=(px, phx, py, phy), min_hole=40, mean_merge=True)[0])
        return out
    finally:
        for p in paths:
            os.remove(p)


def save(cells, name):
    Image.fromarray(np.repeat(np.repeat(cells, PX, 0), PX, 1)).save(os.path.join(OUT, name + '.png'))
    print(name, 'cells', cells.shape[1], 'x', cells.shape[0])


if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(RAW, 'relic_cart_c.png'), 12, square=True, min_hole=40, mean_merge=True)
    cells, body = body_centered(cells)
    save(cells, 'relic_cart_open')
    print('  body width cells', body)
    side, end = folded()
    for cells, name in ((side, 'relic_cart_folded_side'), (end, 'relic_cart_folded_end')):
        cells, body = body_centered(cells)
        save(cells, name)
        print('  wheels width cells', body)
