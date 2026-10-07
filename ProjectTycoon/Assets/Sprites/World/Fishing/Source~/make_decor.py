# -*- coding: utf-8 -*-
# 둑 · 물 소품 넷(2026-10-07 아트방, 설계 52 사용자 「낚시 맵이 휑함」): bank_reeds 갈대 다발(~0.9유닛) · bank_bucket 나무 양동이(~0.4) · water_rock 물 위로 솟은 바위(물결 테, ~0.6) · water_lily 연잎 뭉치(~0.7 폭).
#   Codex가 바탕(raw/decor_template.png, 한 칸 10px: 흰 바탕 + 둑 띠 A + 흙 면 + 물(평면 k_Light) + 웜뱃 + 빨간 발끝 표시 넷) 위에 그린다. 물 소품은 물 위에 그리게 해 물 색 위에서 보이는지 그 자리에서 본다.
#   Codex가 바탕째 다시 그려(격자가 몇 px 밀리고 소품도 표시가 아니라 모래 띠 위에 섰다) make_bank.py의 바탕 빼기(diff)가 안 맞아, 원본 격자 그대로 칸으로 옮긴 뒤(snap_codex raw)
#   바탕을 색 · 줄로 지운다: 흰 바탕(snap) · 물 색 칸 · 모래 띠부터 물 윗줄까지의 줄. 남은 덩어리 가운데 둑 위 둘(웜뱃 뺌) · 물 위 둘을 왼쪽부터 이름 붙인다(테두리가 겹치는 덩어리는 한 소품: 바위 + 물결 테).
#   모래 띠에 걸쳐 잘린 둑 소품 아랫줄은 외곽선으로 닫는다. 시안 a · b · c(raw/decor_<k>.png, 프롬프트 raw/prompt_decor.txt), 소품마다 아트방이 골랐다(PICK).
# 출력(한 칸 2px · PPU 80): ../bank_reeds.png · ../bank_bucket.png · ../water_rock.png · ../water_lily.png
# 사용: python make_decor.py template <폴더>   Codex용 바탕
#       python make_decor.py props <폴더>      <폴더>/decor_<k>.png에서 소품을 떼어 <폴더>/out_<k>/<이름>.png
#       python make_decor.py                   고른 시안을 ../<이름>.png로
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
import make_bank as mb  # noqa: E402
from make_dig import blobs  # noqa: E402
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402

CW, CH, BANK_ROW = 153, 120, 60
NAMES = ['bank_reeds', 'bank_bucket', 'water_rock', 'water_lily']
MARKS = [(52, BANK_ROW - mb.SH + 3), (88, BANK_ROW - mb.SH + 3), (52, 112), (104, 112)]   # 발끝 표시 (x, y): 둑 위 둘 · 물 위 둘(바탕용, 떼는 데는 안 쓴다)
PICK = {'bank_reeds': 'a', 'bank_bucket': 'b', 'water_rock': 'a', 'water_lily': 'a'}   # 아트방 선택: 갈대 A(4 머리 · 0.93유닛) · 양동이 B(물고기 꼬리가 낚시 도구를 말한다) · 바위 A(매끈 · 멀리서 잘 읽힘) · 연잎 A(세 잎 + 분홍 꽃)


def template(path):
    sc = mb.scene(CW, CH, BANK_ROW, mb.STRIPS['a'](), wombat=False, floor_on=False)
    a = np.asarray(sc).copy()
    F = mb.tex('Fishing/stream_face.png').shape[0]
    a[BANK_ROW + F + 1:, :, :3] = mb.LIGHT                      # 물은 평면 한 색(소품을 떼기 쉽게)
    sc = Image.fromarray(a, 'RGBA')
    wb = Image.open(os.path.join(mb.W, 'Shop', 'wombat_front.png')).convert('RGBA')
    wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
    sc.alpha_composite(wb, (6, BANK_ROW - 10 - wb.height))
    big = sc.resize((CW * 10, CH * 10), Image.NEAREST).convert('RGB')
    d = ImageDraw.Draw(big)
    for fx, fy in MARKS:
        d.line((fx * 10 - 60, fy * 10, fx * 10 + 60, fy * 10), fill=(220, 40, 40), width=4)
    big.save(path)


def groups_of(mask, min_cells=8, big=None):
    """8방향 덩어리를 테두리(1칸 여유)가 겹치는 것끼리 묶는다 → 칸 수 많은 순. big을 주면 그보다 큰 덩어리는 서로 묶지 않는다(나란히 선 바위벽 · 좌대, make_wide)"""
    gs = sorted([g for g in blobs(mask, diag=True) if len(g) >= min_cells], key=len, reverse=True)
    box = lambda g: (min(y for y, _ in g) - 1, min(x for _, x in g) - 1, max(y for y, _ in g) + 1, max(x for _, x in g) + 1)
    merged = []
    while gs:
        keep, gs = [gs.pop(0)], gs
        if big:
            seeds, gs = [g for g in gs if len(g) >= big], [g for g in gs if len(g) < big]
        changed = True
        while changed:
            changed = False
            y0, x0, y1, x1 = box([c for g in keep for c in g])
            for g in gs[:]:
                b = box(g)
                if b[2] >= y0 and b[0] <= y1 and b[3] >= x0 and b[1] <= x1:
                    keep.append(g); gs.remove(g); changed = True
        merged.append([c for g in keep for c in g])
        if big:
            gs = seeds + gs
    return sorted(merged, key=len, reverse=True)


def crop(cells, gr, close_bottom=None):
    gy = [y for y, _ in gr]; gx = [x for _, x in gr]
    ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
    for y, x in gr:
        ic[y - min(gy), x - min(gx)] = cells[y, x]
    if close_bottom is not None and max(gy) == close_bottom:
        row = ic[-1]
        row[(row[:, 3] > 0) & (row[:, :3].max(1) >= 110), :3] = mb.LINE       # 모래 띠에 잘린 아랫줄을 외곽선으로
    return ic


def extract(raw_path):
    """Codex 결과 → (이름, 칸 배열) 넷. 바탕(흰 · 물 · 둑 띠 줄)을 지우고 둑 위 두 덩어리(웜뱃 뺌) · 물 위 두 덩어리를 왼쪽부터"""
    cells, _ = snap_codex.snap(raw_path, square=True, min_hole=40, raw=True)
    rgb = cells[..., :3].astype(int)
    water = (np.abs(rgb - mb.LIGHT).max(-1) <= 28) & (cells[..., 3] > 0)
    wtop = int(np.nonzero(water.mean(1) > 0.5)[0].min())
    sandish = (np.abs(rgb - mb.SAND).max(-1) <= 30) | (np.abs(rgb - mb.SAND_DARK).max(-1) <= 30)
    stop = int(np.nonzero(sandish[:wtop].mean(1) > 0.4)[0].min())
    keep = cells[..., 3] > 0
    keep[stop:wtop] = False
    keep[wtop:] &= ~water[wtop:]
    above = keep & (np.arange(keep.shape[0])[:, None] < stop)
    for y, x in max(blobs(above), key=len):                                     # 웜뱃(4방향 가장 큰 덩어리)은 뺀다(갈대가 바로 옆이라 모서리로 닿기도 한다)
        above[y, x] = False
    bank = groups_of(above, 12)[:2]                                             # 둑의 조약돌 조각(10칸 아래)은 안 묶는다
    sea = groups_of(keep & (np.arange(keep.shape[0])[:, None] >= wtop))[:2]
    assert len(bank) == 2 and len(sea) == 2, '덩어리가 모자란다 둑 %d 물 %d' % (len(bank), len(sea))
    for names, gs, cb in ((NAMES[:2], bank, stop - 1), (NAMES[2:], sea, None)):
        gs.sort(key=lambda g: np.mean([x for _, x in g]))
        for name, g in zip(names, gs):
            yield name, crop(cells, g, cb)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pick'
    if cmd == 'template':
        out = sys.argv[2]; os.makedirs(out, exist_ok=True)
        template(os.path.join(out, 'decor_template.png'))
        print('decor_template.png', CW * 10, 'x', CH * 10)
    elif cmd == 'props':
        out = sys.argv[2]
        for k in 'abc':
            raw = os.path.join(out, 'decor_%s.png' % k)
            if not os.path.exists(raw):
                continue
            od = os.path.join(out, 'out_' + k); os.makedirs(od, exist_ok=True)
            for name, ic in extract(raw):
                mb.save(ic, os.path.join(od, name + '.png'))
                print('%s %s.png %dx%d칸' % (k, name, ic.shape[1], ic.shape[0]))
    else:
        done = set()
        for k in sorted(set(PICK.values())):
            for name, ic in extract(os.path.join(HERE, 'raw', 'decor_%s.png' % k)):
                if PICK[name] == k:
                    mb.save(ic, os.path.join(HERE, '..', name + '.png'))
                    print('%s.png %dx%d칸 시안 %s' % (name, ic.shape[1], ic.shape[0], k))
                    done.add(name)
        assert done == set(NAMES), '못 뗀 소품: %s' % (set(NAMES) - done)
