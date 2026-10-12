# -*- coding: utf-8 -*-
# 설계 59 우편함 타일 아이콘(왼쪽 소식 줄, 행상 타일과 같은 칸). 2026-10-12 사용자 선택 B 「하트 봉투」(버린 시안: A 빨간 우체통 · C 나무 우편함).
#   Codex 시안 raw/mail_abc.png(프롬프트 raw/mail_prompt.txt, 세 아이콘을 가로로 한 장)를 원본 그대로 옮긴다(snap_codex raw 격자, 색 합치기 없음).
#   덩어리를 가로 세 자리(A · B · C)에 붙이고 가운데(B)만 쓴다. 23×21칸(한 칸 1px = UI 4px, PPU 25), 18칸 메뉴 아이콘보다 조금 크다.
# 출력 ../icon_mail.png + ui_slices.json 항목. 실행: Windows Python(Pillow · numpy) make_icon_mail.py → 메뉴 Import UI Sprites
import json
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
WORLD = os.path.join(HERE, '..', '..', 'World')
sys.path.insert(0, os.path.join(WORLD, 'Source~'))
sys.path.insert(0, os.path.join(WORLD, 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402


def cut(path):
    cells, _ = snap_codex.snap(path, square=True, min_hole=40, raw=True)
    on = cells[..., 3] > 0
    xs = np.nonzero(on.any(0))[0]
    x0, x1 = xs.min(), xs.max() + 1
    groups = [[], [], []]
    for b in blobs(on, diag=True):
        mx = np.mean([x for _, x in b])
        groups[min(2, int((mx - x0) / (x1 - x0) * 3))] += b
    gr = groups[1]
    gy = [y for y, _ in gr]; gx = [x for _, x in gr]
    ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
    for y, x in gr:
        ic[y - min(gy), x - min(gx)] = cells[y, x]
    return ic


if __name__ == '__main__':
    ic = cut(os.path.join(HERE, 'raw', 'mail_abc.png'))
    Image.fromarray(ic).save(os.path.join(OUT, 'icon_mail.png'))
    slices = json.load(open(os.path.join(OUT, 'ui_slices.json'), encoding='utf-8'))
    slices['icon_mail'] = {'w': int(ic.shape[1]), 'h': int(ic.shape[0]), 'border': None}
    json.dump(slices, open(os.path.join(OUT, 'ui_slices.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('icon_mail.png %d x %d' % (ic.shape[1], ic.shape[0]))
