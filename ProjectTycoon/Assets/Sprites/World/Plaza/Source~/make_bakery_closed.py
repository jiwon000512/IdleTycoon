# -*- coding: utf-8 -*-
# 광장 빵집 문 「닫힘」(설계 59, 2026-10-12 사용자 시안 A 「판자 덧댐」, 프롬프트 raw/bakery_closed_prompt.txt): 밀 5로 열기 전 빵집 문 아치 자리.
#   어두운 굴 위에 낡은 판자 넷을 엇갈려 못 박고 위 구석에 거미줄. 버린 시안: B 자물쇠 나무 문 · C 줄무늬 커튼.
#   raw/bakery_closed_a.png를 원본 그대로 옮긴다(snap_codex raw 격자, 색 합치기 없음). 63×50칸(지금 arch 60×49칸보다 양옆 · 위로 조금 크다).
# 출력: ../bakery_closed.png, PPU 40 · 피벗 아래 가운데(아치 발끝, 젖은 벽과 같다). 사용: python make_bakery_closed.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402

if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(HERE, 'raw', 'bakery_closed_a.png'), square=True, min_hole=40, raw=True)
    ys, xs = np.nonzero(cells[..., 3])
    ic = cells[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    Image.fromarray(ic).save(os.path.join(HERE, '..', 'bakery_closed.png'))
    print('bakery_closed.png %d x %d' % (ic.shape[1], ic.shape[0]))
