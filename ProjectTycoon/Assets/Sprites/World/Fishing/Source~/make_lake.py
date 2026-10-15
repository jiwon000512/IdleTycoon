# -*- coding: utf-8 -*-
# 어두운 호수 물 타일(2026-10-14 아트방, 설계 61 지하 호수의 섬들, 리드 요청 4 「여유 있으면」): 지금은 물길 때의 강물 타일 water_tile_0~3을 그대로 쓴다.
#   물결 · 얼룩 · 반짝 무늬와 네 프레임은 make_stream.water_tile 그대로 두고 색만 호수답게 어둡게 바꾼다(무늬를 바꾸면 흐름 · 이음새를 다시 검사해야 한다).
#   시안: A 깊은 청록(지금보다 한 톤 아래) · B 어두운 남빛(동굴 호수) · C 먹빛 호수(가장 어둡다. 물고기 그림자와 대비가 줄어든다).
#   WaterPainter의 거품 줄 k_Light(110,182,170) · 옆 그늘 k_Deep(48,120,114)은 코드 상수라, 고르면 그 시안의 LIGHT · DEEP 값으로 맞추게 리드에게 넘긴다.
# 출력(32×32, 한 텍셀 = 한 칸): ../water_tile_0~3.png (고른 시안 PICK)
# 사용: python make_lake.py <폴더>   세 시안을 <폴더>/set_<a|b|c>/water_tile_<0~3>.png로
#       python make_lake.py          고른 시안을 ../water_tile_<0~3>.png로
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_stream as ms  # noqa: E402

PICK = 'b'
# 이름: (WATER 바탕, DEEP 얼룩 · 옆 그늘, LIGHT 물결 · 거품, SHINE 반짝)
PALETTES = {
    'now': (ms.WATER, ms.DEEP, ms.LIGHT, ms.SHINE),
    'a': ((46, 112, 108), (36, 94, 92), (94, 160, 152), (196, 226, 218)),
    'b': ((40, 90, 108), (30, 72, 90), (84, 140, 154), (184, 214, 220)),
    'c': ((34, 74, 80), (26, 58, 64), (70, 120, 122), (170, 204, 200)),
}


def tiles(k):
    water, deep, light, shine = PALETTES[k]
    pairs = [(ms.WATER, water), (ms.DEEP, deep), (ms.LIGHT, light), (ms.SHINE, shine)]
    out = []
    for frame in range(4):
        t = ms.water_tile(frame=frame).copy()
        rgb = t[..., :3].copy()
        for src, dst in pairs:
            m = (t[..., 0] == src[0]) & (t[..., 1] == src[1]) & (t[..., 2] == src[2])
            rgb[m] = dst
        t[..., :3] = rgb
        out.append(t)
    return out


if __name__ == '__main__':
    if len(sys.argv) > 1:
        for k in ('a', 'b', 'c'):
            out = os.path.join(sys.argv[1], 'set_' + k)
            os.makedirs(out, exist_ok=True)
            for i, t in enumerate(tiles(k)):
                Image.fromarray(t, 'RGBA').save(os.path.join(out, 'water_tile_%d.png' % i))
        print(sys.argv[1])
    else:
        for i, t in enumerate(tiles(PICK)):
            Image.fromarray(t, 'RGBA').save(os.path.join(HERE, '..', 'water_tile_%d.png' % i))
        print('water_tile_0~3.png <-', PICK, PALETTES[PICK])
