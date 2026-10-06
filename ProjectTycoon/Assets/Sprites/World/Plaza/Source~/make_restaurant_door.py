# 광장 횟집 문 장식(2026-10-06 사용자 선택 A 「노렌 차양」, 프롬프트 raw/restaurant_door_prompt.txt).
#   낚시터 문(make_fishing_door.py)과 같은 바탕 · 같은 방법: 아치 꼭대기에 얹는 장식 한 장, 나무 들보가 이름 판이고 글은 게임이 쓴다.
#   남색 천 세 자락이 아치 윗부분을 덮는다(문 아래는 가리지 않음). 자르기 · 자리 · 이름 판 찾기는 make_fishing_door를 그대로 쓴다.
# 출력: ../restaurant_door.png(한 칸 2px · PPU 80, 피벗 아래 가운데). 자리 · 이름 판 가운데를 칸과 유닛으로 출력한다.
# 사용: python make_restaurant_door.py
import os
import numpy as np
from PIL import Image
from make_fishing_door import HERE, RAW, cut, board, snap_codex

if __name__ == '__main__':
    p, dx, dy = cut(os.path.join(RAW, 'restaurant_door_a.png'))
    assert p.shape[1] <= 68
    Image.fromarray(np.repeat(np.repeat(p, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', 'restaurant_door.png'))
    bw, bh, bx, by = board(snap_codex.merge_colors(p.astype(np.int64), p[..., 3] > 0, 12).astype(np.uint8))   # 판 자리 찾기에만 색을 합친 사본
    print('restaurant_door.png %d x %d 칸' % (p.shape[1], p.shape[0]))
    print('자리: 피벗(아래 가운데)이 아치 밑변 가운데에서 옆 %.1f칸 · 위 %.1f칸 = (%.4f, %.4f) 유닛' % (dx, dy, dx / 40, dy / 40))
    print('이름 판 %d x %d 칸, 가운데 = 아치 밑변 가운데에서 (%.1f, %.1f)칸 = (%.4f, %.4f) 유닛' % (bw, bh, dx + bx, dy + by, (dx + bx) / 40, (dy + by) / 40))
