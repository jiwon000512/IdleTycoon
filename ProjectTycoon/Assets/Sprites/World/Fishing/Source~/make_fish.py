# 낚시터 물고기(2026-10-05 사용자 선택 B 「둥근 귀염」, 프롬프트 raw/prompt_fish.txt).
#   피라미 · 붕어 · 메기: raw/fish_final.png(웜뱃을 칸 크기 그대로 그려 넣은 바탕에 그리게 함). Codex가 물고기만 반 칸 픽셀로 그려서
#     반 칸 격자로 옮긴 뒤(색은 넉넉히 48) 2×2를 한 칸으로 줄인다: 불투명 2칸 이상이면 불투명, 색은 가장 많은 색(가장자리에 외곽선이 섞이면 외곽선).
#   대물 바위잉어: raw/fish_style_b.png(방향 시안 B)의 대물을 원본 격자 그대로(최종 바탕에서는 대물 상자가 잘려 작게 나왔다).
#   그림마다 12색 안(snap_codex.merge_colors).
# 출력(한 칸 2px · PPU 80, 오른쪽을 본다):
#   ../fish_<minnow|crucian|catfish|boss>_swim.png  물속에서 위로 본 모습(짙은 청록, 외곽선 없음). 왼쪽으로 헤엄치면 좌우 뒤집기
#   Resources/Sprites/Items/fish_<minnow|crucian|catfish|boss>.png  옆모습 = 창고 아이콘 겸 둑으로 낚여 튄 모습
# 사용: python make_fish.py
import os
import sys
from collections import Counter
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
ITEMS = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Items')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402


def pieces(cells, n):
    out = []
    for gr in sorted(blobs(cells[..., 3] > 0, diag=True), key=len, reverse=True)[:n]:
        ys = [y for y, _ in gr]; xs = [x for _, x in gr]
        p = np.zeros((max(ys) - min(ys) + 1, max(xs) - min(xs) + 1, 4), np.uint8)
        for y, x in gr:
            p[y - min(ys), x - min(xs)] = cells[y, x]
        out.append((p, min(ys), min(xs)))
    return out


def halve(cells):
    H, W = cells.shape[0] // 2 * 2, cells.shape[1] // 2 * 2
    out = np.zeros((H // 2, W // 2, 4), np.uint8)
    for y in range(H // 2):
        for x in range(W // 2):
            blk = cells[2 * y:2 * y + 2, 2 * x:2 * x + 2].reshape(-1, 4)
            op = blk[blk[:, 3] > 0]
            if len(op) < 2:
                continue
            cols = Counter(tuple(int(v) for v in c[:3]) for c in op)
            dark = [c for c in cols if max(c) < 70]
            out[y, x, :3] = dark[0] if len(op) < 4 and dark else cols.most_common(1)[0][0]
            out[y, x, 3] = 255
    return out


def limit12(p):
    return snap_codex.merge_colors(p.copy(), p[..., 3] > 0, 12)


def save(path, p):
    Image.fromarray(np.repeat(np.repeat(p, 2, 0), 2, 1), 'RGBA').save(path)
    print(os.path.basename(path), p.shape[1], 'x', p.shape[0], '칸', len(np.unique(p[p[..., 3] > 0][:, :3], axis=0)), '색')


if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(RAW, 'fish_final.png'), 48, square=True, min_hole=40)
    parts = pieces(halve(cells), 9)                         # 웜뱃 + 물고기 셋 × (옆 · 위)
    parts = sorted(parts, key=lambda t: t[2])[1:]          # 맨 왼쪽 웜뱃은 뺀다
    mid = np.mean([t[1] for t in parts])
    side = sorted([t for t in parts if t[1] < mid], key=lambda t: t[2])
    top = sorted([t for t in parts if t[1] >= mid], key=lambda t: t[2])
    big, _ = snap_codex.snap(os.path.join(RAW, 'fish_style_b.png'), 48, square=True, min_hole=40)
    bp = pieces(big, 8)
    bmid = big.shape[0] / 2
    boss_side = max([t for t in bp if t[1] < bmid], key=lambda t: t[2])[0]
    boss_top = max([t for t in bp if t[1] >= bmid], key=lambda t: t[2])[0]
    for name, s, t in zip(['minnow', 'crucian', 'catfish'], side, top):
        save(os.path.join(ITEMS, 'fish_%s.png' % name), limit12(s[0]))
        save(os.path.join(HERE, '..', 'fish_%s_swim.png' % name), limit12(t[0]))
    save(os.path.join(ITEMS, 'fish_boss.png'), limit12(boss_side))
    save(os.path.join(HERE, '..', 'fish_boss_swim.png'), limit12(boss_top))
