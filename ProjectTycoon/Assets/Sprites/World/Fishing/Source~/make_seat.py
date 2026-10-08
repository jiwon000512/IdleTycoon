# -*- coding: utf-8 -*-
# 좌대 다시(2026-10-07 사용자 「의자 디자인 새로 뽑자」): 설계 53 둑 시트(make_wide.py)의 A 접이 의자는 임시로 두고, 새 디자인 셋을 한 시트에 뽑는다.
#   쓰임: 둑 위 광장 손님(토끼 등, 키 0.9유닛)이 뒤에서 뛰어 윗판에 앉아 물 쪽(앞)을 본다. 손님 몸이 윗판과 위쪽을 가리므로 아래 실루엣(다리 · 받침)이 읽혀야 한다. 놓기 전엔 같은 그림을 35% 반투명으로.
#   규격: 앞모습, 피벗 아래 가운데, 폭 0.5유닛(20칸) 안팎, 앉는 면 0.3유닛(12칸) 안팎. 넘길 때 앉는 면 높이(px · 유닛)를 메모에.
#   바탕 · 떼기는 make_wide와 같다(raw/seat_template.png: 둑 띠 A + 웜뱃 + 빨간 발끝 표시 셋). 시안: a 통나무 토막 + 널 / b 네 다리 낮은 걸상 / c 돌 둘 위 널빤지. 사용자 선택 PICK.
# 출력(한 칸 2px · PPU 80): ../seat.png
# 사용: python make_seat.py template <폴더> / props <폴더> / (없음) 고른 시안을 ../seat.png로(앉는 면 높이를 찍는다)
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_bank as mb  # noqa: E402
import make_wide as mw  # noqa: E402

NAMES = ['seat_a', 'seat_b', 'seat_c']
mw.SHEETS['seat'] = dict(cw=150, ch=100, bank_row=60, bank_x=(70, 100, 130), water_x=(), water_y=0, names=NAMES)
PICK = 'a'
PROMPT = (mw.COMMON +
          "Paint THREE small wooden angler's seats standing on the bank, one at each red line, feet exactly on the red line, rising upward over the white area, nothing drawn below the line. "
          "Each seat is about 20 art pixels wide and 14 tall. The FLAT SEAT TOP is about 12 art pixels above the ground and must be wide and clearly readable, because a small round animal "
          "will sit on it facing the viewer and hide the top, so the legs or base under the seat must read clearly on their own. From left to right:\n"
          "1) a short cut log stump standing upright with a flat plank nailed on top, bark on the side, a few growth rings showing at the plank's edge.\n"
          "2) a low four-legged wooden stool with a thick top plank and splayed legs joined by a cross brace.\n"
          "3) a plank laid across two flat grey stones, like a riverside bench." +
          mw.STYLE % 'seat')


def seat_height(a):
    """앉는 면(윗판 윗변) = 위에서 첫 넓은 줄(폭의 60% 넘는 첫 줄)의 다음 줄(외곽선 아래) → 피벗(아래 끝)에서 칸"""
    op = a[..., 3] > 0
    width = op.any(0).sum()
    rows = [y for y in range(a.shape[0]) if op[y].sum() >= width * 0.6]
    return a.shape[0] - rows[0] - 1


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pick'
    if cmd == 'template':
        out = sys.argv[2]; os.makedirs(out, exist_ok=True)
        mw.template('seat', os.path.join(out, 'seat_template.png'))
        open(os.path.join(out, 'prompt_seat.txt'), 'w', encoding='utf-8').write(PROMPT)
        print('seat_template.png + prompt_seat.txt')
    elif cmd == 'props':
        out = sys.argv[2]
        for name, ic in mw.extract(os.path.join(out, 'seat.png'), NAMES, [], min_cells=24):
            mb.save(ic, os.path.join(out, name + '.png'))
            print('%s.png %dx%d칸 앉는 면 %d칸' % (name, ic.shape[1], ic.shape[0], seat_height(ic)))
    else:
        for name, ic in mw.extract(os.path.join(HERE, 'raw', 'seat.png'), NAMES, [], min_cells=24):
            if name == 'seat_' + PICK:
                mb.save(ic, os.path.join(HERE, '..', 'seat.png'))
                h = seat_height(ic)
                print('seat.png %dx%d칸 시안 %s · 앉는 면 피벗에서 %d칸 = %dpx = %.3f유닛' % (ic.shape[1], ic.shape[0], PICK, h, h * 2, h / 40))
