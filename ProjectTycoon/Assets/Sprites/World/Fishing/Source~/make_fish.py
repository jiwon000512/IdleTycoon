# 낚시터 물고기(2026-10-05 사용자 선택 B 「둥근 귀염」, 프롬프트 raw/prompt_fish.txt).
#   피라미 · 붕어 · 메기: raw/fish_final.png(웜뱃을 칸 크기 그대로 그려 넣은 바탕에 그리게 함). Codex가 물고기만 반 칸 픽셀로 그렸다.
#   대물 바위잉어: raw/fish_style_b.png(방향 시안 B)의 대물(최종 바탕에서는 대물 상자가 잘려 작게 나왔다).
#   원본 그대로 옮긴다(2026-10-05 사용자 「그림 퀄리티에 변형이 생기는 규칙은 다 지운다」, snap_codex raw):
#   반 칸 그림은 원본 픽셀 하나 = 텍셀 하나(옛 판은 2×2를 한 칸으로 줄이고 12색으로 합쳐 뭉개졌다), 대물은 원본 픽셀 하나 = 한 칸(2텍셀).
#   시안의 위에서 본 모습(아랫줄)은 아트 규칙(앞쪽 위에서 본 모습)에 어긋나 쓰지 않는다. 물속은 크기만 다른 그림자(make_fish_shadow.py).
# 출력(PPU 80, 오른쪽을 본다): Resources/Sprites/Items/fish_<minnow|crucian|catfish|boss>.png  옆모습 = 창고 아이콘 겸 둑으로 낚여 튄 모습
# 사용: python make_fish.py
import os
import sys
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


def save(path, p, px):
    Image.fromarray(np.repeat(np.repeat(p, px, 0), px, 1), 'RGBA').save(path)
    print(os.path.basename(path), p.shape[1] * px, 'x', p.shape[0] * px, 'px', len(np.unique(p[p[..., 3] > 0][:, :3], axis=0)), '색')


if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(RAW, 'fish_final.png'), square=True, min_hole=40, raw=True)
    parts = pieces(cells, 9)                                # 웜뱃 + 물고기 넷 × (옆 · 위), 대물은 따로(아래)
    parts = sorted(parts, key=lambda t: t[2])[1:]          # 맨 왼쪽 웜뱃은 뺀다
    mid = np.mean([t[1] for t in parts])
    side = sorted([t for t in parts if t[1] < mid], key=lambda t: t[2])
    big, _ = snap_codex.snap(os.path.join(RAW, 'fish_style_b.png'), square=True, min_hole=40, raw=True)
    bp = pieces(big, 8)
    bmid = big.shape[0] / 2
    boss_side = max([t for t in bp if t[1] < bmid], key=lambda t: t[2])[0]
    for name, s in zip(['minnow', 'crucian', 'catfish'], side):
        save(os.path.join(ITEMS, 'fish_%s.png' % name), s[0], 1)
    save(os.path.join(ITEMS, 'fish_boss.png'), boss_side, 2)
