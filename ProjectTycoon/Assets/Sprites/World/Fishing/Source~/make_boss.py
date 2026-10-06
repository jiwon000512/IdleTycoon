# 단계 대물 셋의 낚인 옆모습(2026-10-06 설계 50: 1단계 바위잉어 → 2단계 대왕 메기 → 3단계 황금 비단잉어, 4단계부터 되풀이).
#   물속에서는 지금 그림자(fish_boss_swim.png)이고, 낚이는 순간 이 옆모습으로 드러난다. 소식지에도 이 그림이 뜬다.
# 원본 raw/boss_<a|b|c>.png(프롬프트 raw/prompt_boss.txt): 웜뱃과 메기 아이콘을 한 칸 10px로 그려 넣은 바탕(raw/boss_template.png)의
#   위 상자에 낚인 옆모습, 아래 상자에 물속에서 위에서 본 모습을 그리게 한 한 장. 시안 셋을 사용자에게 보인 뒤 셋을 모두 쓰기로 했다.
#   위에서 본 모습은 쓰지 않는다(「물속에서는 그림자 그대로, 낚여 나올 때 드러난다」). 원본에만 남아 있다.
#   1. 오른쪽(대물 둘)만 잘라 원본 그대로 칸으로(snap_codex raw). B는 원본 픽셀이 조금 잘다(한 칸 8.6px) → 칸 수가 많아 그림이 크다
#   2. 큰 덩어리 둘 중 위쪽 = 옆모습. 떨어진 조각(수염 끝)은 가까운 덩어리에 붙인다
#   3. 수염 고리에 갇힌 흰 바탕만 투명으로(둘레의 절반 이상이 어두운 선이나 투명인 흰 덩어리). 몸 색에 둘러싸인 하이라이트 · 눈 반짝임은 남긴다
# 출력: Resources/Sprites/Bosses/<rock_carp|king_catfish|gold_koi>.png (한 칸 2px · PPU 80 · 가운데 피벗 · 오른쪽을 봄, 배율 1로 쓴다)
# 사용: python make_boss.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

OUT = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Bosses')
BOSSES = {'a': 'rock_carp', 'b': 'king_catfish', 'c': 'gold_koi'}


def holes(a):
    """수염 고리에 갇힌 흰 바탕만 투명으로: 흰 덩어리(4칸 이상) 둘레의 절반 이상이 어두운 선(합 260 아래)이나 투명이면 바탕"""
    a = a.copy()
    op = a[..., 3] > 0
    light = op & (a[..., :3].min(2) >= 235)
    dark = op & (a[..., :3].astype(int).sum(2) < 260)
    for cells in blobs(light):
        own = set(cells)
        rim = {(y + dy, x + dx) for y, x in cells for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1))} - own
        rim = [(y, x) for y, x in rim if 0 <= y < a.shape[0] and 0 <= x < a.shape[1]]
        if len(cells) >= 4 and sum(dark[y, x] or not op[y, x] for y, x in rim) * 2 >= len(rim):      # 눈 반짝임(1~3칸)은 남긴다
            for y, x in cells:
                a[y, x] = 0
    return a


def side(path):
    """원본 한 장 → 낚인 옆모습 칸 배열"""
    im = Image.open(path).convert('RGB')
    tmp = os.path.join(HERE, '_boss_tmp.png')
    try:
        im.crop((480, 0, im.width, im.height)).save(tmp)      # 왼쪽의 크기 기준 웜뱃 · 메기는 뺀다
        cells, grid = snap_codex.snap(tmp, square=True, min_hole=400, raw=True)
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
    cells = holes(cells)
    groups = sorted(blobs(cells[..., 3] > 0, diag=True), key=len, reverse=True)
    main, rest = groups[:2], groups[2:]
    for gr in rest:
        best = min(range(2), key=lambda i: min(abs(gr[0][0] - a) + abs(gr[0][1] - b) for a, b in main[i][::5]))
        main[best] = main[best] + gr
    gr = min(main, key=lambda g: min(y for y, _ in g))      # 위쪽 덩어리 = 옆모습
    ys = [y for y, _ in gr]
    xs = [x for _, x in gr]
    p = np.zeros((max(ys) - min(ys) + 1, max(xs) - min(xs) + 1, 4), np.uint8)
    for y, x in gr:
        p[y - min(ys), x - min(xs)] = cells[y, x]
    return p, grid


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for key, name in BOSSES.items():
        p, grid = side(os.path.join(HERE, 'raw', 'boss_%s.png' % key))
        Image.fromarray(np.repeat(np.repeat(p, 2, 0), 2, 1), 'RGBA').save(os.path.join(OUT, name + '.png'))
        print('%s.png %d×%dpx (%d×%d칸, 원본 한 칸 %.1fpx)' % (name, p.shape[1] * 2, p.shape[0] * 2, p.shape[1], p.shape[0], grid[0]))
