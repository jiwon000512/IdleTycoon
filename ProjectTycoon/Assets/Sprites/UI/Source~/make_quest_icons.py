# -*- coding: utf-8 -*-
# 할 일 카드 그림 칸 「행동 그림」 열여섯(설계 55, 2026-10-12 사용자 화풍 A 「물건 하나」, 프롬프트 raw/quest_icons_prompt.txt).
#   걸음 25개를 행동끼리 묶었다: 굽기 · 내놓기(진열 · 회 내기) · 계산 · 가기(농장 · 낚시터) · 심기 · 거두기 · 파기(굴 · 바위 · 댐) · 갈기 · 놓기(오븐 · 좌대) ·
#   뽑기(점원) · 별 평가(평가 · 별 모으기) · 빌기 · 유물 · 치우기 · 열기(새 맛 셋 · 횟집) · 낚기. 버린 시안: B 웜뱃 앞발 · C 작은 장면(받침).
#   바탕 raw/quest_icons_template.png(윗줄 지금 행동 아이콘, 아래 24×24칸 빈 틀 4×4)에 그리게 한 raw/quest_icons_a.png를
#   원본 그대로 옮긴다(snap_codex raw 격자, 칸 가운데 중앙값 그대로, 색 합치기 없음). 덩어리를 4×4 자리에 붙여 자른다 → 23~31칸.
#   Codex가 잘게 그려 줄이지 않고, 카드에서 3배로 놓는다: PPU 100/3(한 텍셀 = 3px).
# 출력: Assets/Resources/Sprites/Quests/<이름>.png (QuestTable icon). 사용: python make_quest_icons.py → 임포트 PPU 33.333 · Point · 비압축
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
WORLD = os.path.join(HERE, '..', '..', 'World')
sys.path.insert(0, os.path.join(WORLD, 'Source~'))
sys.path.insert(0, os.path.join(WORLD, 'Shop', 'Source~'))
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402

OUT = os.path.join(HERE, '..', '..', '..', 'Resources', 'Sprites', 'Quests')
NAMES = ['bake', 'serve', 'checkout', 'go', 'plant', 'harvest', 'dig', 'till', 'place', 'hire', 'rate', 'pray', 'draw', 'clean', 'unlock', 'fish']


def icons(path):
    _, (px, phx, py, phy) = snap_codex.snap(path, square=True, min_hole=40, raw=True)
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    h, w = a.shape[:2]
    xs, ys = np.arange(phx + 1, w, px), np.arange(phy + 1, h, py)
    c = np.zeros((len(ys) - 1, len(xs) - 1, 4), np.uint8)
    for j in range(len(ys) - 1):
        for k in range(len(xs) - 1):
            y0, y1, x0, x1 = ys[j], ys[j + 1], xs[k], xs[k + 1]
            col = np.median(a[int(y0 + (y1 - y0) * .25):int(y1 - (y1 - y0) * .25) + 1, int(x0 + (x1 - x0) * .25):int(x1 - (x1 - x0) * .25) + 1].reshape(-1, 3), 0).round()
            c[j, k, :3] = col
            c[j, k, 3] = 0 if col.min() > 238 else 255
    on = c[..., 3] > 0
    yy, xx = np.nonzero(on)
    bx0, bx1, by0, by1 = xx.min(), xx.max() + 1, yy.min(), yy.max() + 1
    groups = [[] for _ in NAMES]
    # 덩어리 가운데가 든 4×4 자리로(떨어진 불꽃 · 먼지 · 반짝임도 제 그림에)
    for gr in blobs(on, diag=True):
        if len(gr) < 2:
            continue
        my = np.mean([y for y, _ in gr]); mx = np.mean([x for _, x in gr])
        r = min(3, int((my - by0) / (by1 - by0) * 4)); q = min(3, int((mx - bx0) / (bx1 - bx0) * 4))
        groups[r * 4 + q] += gr
    for name, gr in zip(NAMES, groups):
        gy = [y for y, _ in gr]; gx = [x for _, x in gr]
        ic = np.zeros((max(gy) - min(gy) + 1, max(gx) - min(gx) + 1, 4), np.uint8)
        for y, x in gr:
            ic[y - min(gy), x - min(gx)] = c[y, x]
        yield name, ic


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name, ic in icons(os.path.join(HERE, 'raw', 'quest_icons_a.png')):
        Image.fromarray(ic, 'RGBA').save(os.path.join(OUT, name + '.png'))
        print('%s.png %d x %d' % (name, ic.shape[1], ic.shape[0]))
