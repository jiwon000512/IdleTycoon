# 횟집 수조(2026-10-06 사용자 선택 A 「나무 장 수조」, 프롬프트 raw/prompt_tank.txt).
#   시안 raw/tank_a.png는 웜뱃을 한 칸 10px로 그린 바탕(raw/tank_template.png) 위에 한 칸 8.92px로 그려졌다. 원본 그대로 옮긴다: 원본 픽셀 하나 = 2텍셀, 색 그대로.
#   격자는 고정: 자동으로 찾으면 세로가 9.87px로 잡혀 원본보다 납작해진다(비율 1.39, 원본 1.25). 원본 픽셀은 정사각이라 세로도 8.92px,
#   시작점 0이 문 손잡이(네모 칸)를 살린다(1 · 2 · 3에서는 「U」자로 깨짐).
#   바깥 번짐: 장 양옆에 흰 바탕 한 줄이 세로로 붙어 나와(위아래로 이어져 snap_codex가 못 벗김) 바깥과 닿은 거의 흰 칸을 지우고,
#   외곽선과 바탕이 반씩 걸친 회색 칸(바깥과 닿고 안쪽 이웃이 진한 외곽선)도 지운다. 칸 단위로 한 뒤 2배로 늘린다.
# 출력: ../tank.png(PPU 80, 피벗 밑변 가운데 = 장 다리 발끝). 물 창 사각형(물고기 그림자가 헤엄칠 곳)을 함께 찍는다
# 사용: python make_tank.py [출력.png]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402


def near(m):
    p = np.pad(m, 1)
    return p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]


def clear_fringe(a):
    a = a.copy()
    rgb = a[..., :3].astype(int)
    lum, sat = rgb.mean(-1), rgb.max(-1) - rgb.min(-1)
    white = (rgb.min(-1) >= 225) & (sat <= 20)
    while True:
        out = a[..., 3] == 0
        kill = white & near(np.pad(out, 1, constant_values=True))[1:-1, 1:-1] & ~out
        if not kill.any():
            break
        a[kill] = 0
    out = a[..., 3] == 0
    grey = (lum >= 95) & (sat <= 30) & ~out & near(np.pad(out, 1, constant_values=True))[1:-1, 1:-1] & near((lum < 80) & ~out)
    a[grey] = 0
    ys, xs = np.nonzero(a[..., 3])
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def water_rect(a):
    # 청록 칸의 가장 큰 덩어리에서 위 수면 띠(3칸) · 아래 모래 · 수초 줄을 뺀 사각형
    r, g, b = (a[..., i].astype(int) for i in range(3))
    teal = (a[..., 3] > 0) & (g > 130) & (b > 120) & (r < 140) & (g - r > 40)
    m = np.zeros(teal.shape, bool)
    for y, x in max(blobs(teal), key=len):
        m[y, x] = True
    ys, xs = np.nonzero(m)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    while y1 > y0 and m[y1 - 1, x0:x1].mean() < 0.7:
        y1 -= 1
    return x0 + 1, y0 + 3, x1 - 1, y1 - 1


if __name__ == '__main__':
    src = Image.open(os.path.join(HERE, 'raw', 'tank_a.png')).convert('RGB').crop((530, 0, 1536, 1024))   # 웜뱃을 뺀 오른쪽
    tmp = os.path.join(HERE, 'raw', '_tank_crop.png')
    src.save(tmp)
    cells, _ = snap_codex.snap(tmp, raw=True, fixed=(8.92, 7.5, 8.92, 0.0))
    os.remove(tmp)
    out = np.repeat(np.repeat(clear_fringe(cells), 2, 0), 2, 1)
    Image.fromarray(out, 'RGBA').save(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'tank.png'))
    h, w = out.shape[:2]
    x0, y0, x1, y1 = water_rect(out)
    print('tank.png %d x %d px, %.3f x %.3f 유닛' % (w, h, w / 80, h / 80))
    print('물 창 px(왼쪽 위 기준) %d,%d ~ %d,%d (%d x %d)' % (x0, y0, x1, y1, x1 - x0, y1 - y0))
    print('물 창 유닛(피벗 기준) x %.3f ~ %.3f, y %.3f ~ %.3f' % ((x0 - w / 2) / 80, (x1 - w / 2) / 80, (h - y1) / 80, (h - y0) / 80))
