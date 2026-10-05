# 낚시 점원 오두막(2026-10-05 사용자 선택 B 「미끼 노점」, 프롬프트 raw/prompt_hut.txt).
#   시안 raw/hut_b.png는 웜뱃을 한 칸 10px로 그려 넣은 바탕(raw/hut_template.png) 위에 그렸고, 노점은 반 칸(5px) 픽셀로 그려졌다.
#   원본 그대로 옮긴다(사용자 「그림 퀄리티에 변형이 생기는 규칙은 다 지운다」): 원본 픽셀 하나 = 텍셀 하나, 색 그대로.
#   (게임 칸으로 2×2씩 줄인 12색 · 16색 판은 판자 선 · 병 속 물고기가 뭉개져 버렸다.)
#   흰 바탕만 투명, 가장 큰 덩어리(노점)만 남긴다(웜뱃 · 잡티는 버림).
# 출력: ../fishing_hut.png(PPU 80이면 2.2 × 1.86유닛, 피벗 아래 가운데 = 기둥 발끝)
# 사용: python make_hut.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

if __name__ == '__main__':
    c, grid = snap_codex.snap(os.path.join(HERE, 'raw', 'hut_b.png'), cell=5, square=True, min_hole=40, raw=True)
    big = max(blobs(c[..., 3] > 0, diag=True), key=len)
    ys = [y for y, _ in big]; xs = [x for _, x in big]
    out = np.zeros((max(ys) - min(ys) + 1, max(xs) - min(xs) + 1, 4), np.uint8)
    for y, x in big:
        out[y - min(ys), x - min(xs)] = c[y, x]
    Image.fromarray(out, 'RGBA').save(os.path.join(HERE, '..', 'fishing_hut.png'))
    print('fishing_hut.png %d x %d px (격자 %.2fpx), %.3f x %.3f 유닛' % (out.shape[1], out.shape[0], grid[0], out.shape[1] / 80, out.shape[0] / 80))
