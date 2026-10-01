# 설계 31 유물 행상 너구리(2026-10-01 사용자: 종 B 너구리 → 턴어라운드 B, 옆모습 「좀 더 통통하게」로 다시 그림). 프롬프트 shop_raw/tanuki_prompt.txt
#   앞 · 뒤: shop_raw/tanuki_sheet_raw_b.png(여우 완성 시트를 크기 기준으로 준 턴어라운드), 옆: shop_raw/tanuki_side_raw_b.png 가운데
#   원본 격자 그대로 옮긴다(World/Source~/snap_codex.py). 한 시트는 픽셀 크기가 같아 세 그림 주기의 중앙값 하나로, 시작점만 그림마다 찾는다.
#   흰 하이라이트를 구멍으로 뚫지 않게 min_hole. 발 가운데가 그림 가운데(피벗)가 되게 투명 여백을 붙인다
#   (앞모습은 꼬리 때문에 54칸 → 손님 시트 칸 폭 56칸(112px), make_anim.py CELLS).
# 출력: tanuki_front.png · tanuki_back.png · tanuki_side.png(한 칸 2px). 다음: make_anim.py(anim_specs.py 'tanuki')
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'shop_raw')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2
MIN_HOLE = 40


def ink_columns(im):
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


def crop(im, run, tmp):
    im.crop((max(run[0] - 20, 0), 0, min(run[1] + 20, im.width), im.height)).save(tmp)
    return tmp


def snap_fixed(path, period, span):
    g = np.asarray(Image.open(path).convert('RGB')).astype(float).mean(2)
    px, phx = snap_codex.grid(np.abs(np.diff(g, axis=1)).sum(0), period, span)
    py, phy = snap_codex.grid(np.abs(np.diff(g, axis=0)).sum(1), period, span)
    return snap_codex.snap(path, 12, fixed=(px, phx, py, phy), min_hole=MIN_HOLE)[0]


def centered(cells):
    # 발(맨 아래 두 줄) 가운데가 그림 가운데가 되게 왼쪽이나 오른쪽에 투명 열을 붙인다
    feet = np.nonzero((cells[-3:, :, 3] > 0).any(0))[0]
    mid = feet.min() + feet.max() + 1          # 발 가운데 × 2(칸 경계)
    left, right = mid, 2 * cells.shape[1] - mid
    pad = abs(left - right) // 2
    widths = ((0, 0), (pad, 0), (0, 0)) if right > left else ((0, 0), (0, pad), (0, 0))
    return np.pad(cells, widths)


def save(cells, name):
    Image.fromarray(np.repeat(np.repeat(cells, PX, 0), PX, 1)).save(os.path.join(HERE, name + '.png'))
    print(name, 'cells', cells.shape[1], 'x', cells.shape[0])


def main():
    tmp = os.path.join(HERE, '_tanuki_tmp.png')
    try:
        sheet = Image.open(os.path.join(RAW, 'tanuki_sheet_raw_b.png')).convert('RGB')
        runs = ink_columns(sheet)
        assert len(runs) == 3, runs
        periods = [snap_codex.snap(crop(sheet, r, tmp), 12, square=True, min_hole=MIN_HOLE)[1][0] for r in runs]
        period = float(np.median(periods))
        for name, run in (('front', runs[0]), ('back', runs[2])):
            save(centered(snap_fixed(crop(sheet, run, tmp), period, 0.01)), 'tanuki_' + name)
        side = Image.open(os.path.join(RAW, 'tanuki_side_raw_b.png')).convert('RGB')
        runs = ink_columns(side)
        assert len(runs) == 3, runs
        # 옆모습은 한 칸 10px로 준 입력 시트(tanuki_side_sheet_in_b.png)의 가운데를 채운 그림
        save(centered(snap_fixed(crop(side, runs[1], tmp), side.width / 1536 * 10, 0.03)), 'tanuki_side')
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


if __name__ == '__main__':
    main()
