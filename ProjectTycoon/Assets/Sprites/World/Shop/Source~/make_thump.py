# -*- coding: utf-8 -*-
# 낚시터 웜뱃 동작 「엉덩이 쿵」 · 「털썩 앉기」(2026-10-05 사용자 선택 A 「통통 폴짝 · 곰인형」, 프롬프트 shop_raw/thump_prompt.txt).
#   시트 shop_raw/thump_sheet_a.png: 3줄(앞 · 옆 · 뒤) × 6칸(웅크림 · 폴짝 · 쿵 · 앉는 중 · 앉음 · 서기). 여섯째 칸 = 지금 웜뱃(크기 기준, 쓰지 않음).
#   털썩 두 칸은 「하체가 길어지는 느낌, 짜리몽땅한 상태로 다리가 나오게」(사용자)로 A를 참조로 그 두 칸만 다시 그린 shop_raw/thump_sit_fix.png에서 쓴다.
#   원본 그대로 옮긴다(snap_codex 격자, 칸 가운데 중앙값 그대로, 색 바꾸기 없음):
#   1. 줄은 그림 사이 가장 넓은 빈틈 둘에서 나누고, 한 시트 한 주기(snap_codex로 찾은 5px 근처)로 줄마다 시작점만 찾는다.
#   2. 바탕 = 띠 가장자리에서 이어진 흰 칸(틀 자국 연회색 포함)과 몸 사이에 갇힌 넓은 흰 칸. 머리 위 흰 하이라이트는 남는다.
#   3. 몸통(150칸 넘는 덩어리)은 칸 자리로, 흙먼지 같은 작은 조각은 가장 가까운 몸통에 붙인다.
#   4. 바닥선 = 폴짝을 뺀 다섯 그림 발끝의 가운데값. 바닥에 선 자세는 바닥선에 붙이고(3칸 안), 폴짝만 그 칸 안에서 위로 뜬다.
#      가로는 발(맨 아래 세 줄) 가운데 = 칸 가운데. 털썩 두 칸은 몸에서 떨어진 조각(옆 칸 흙먼지)을 뺀다.
#   5. 칸 단위 손질 하나: 털썩 「앉는 중」 앞모습 왼쪽 눈이 격자 경계에 걸려 한 칸이 빠져(사용자 「왼쪽 눈 하나가 깨져있음」) 오른쪽 눈과 같은 2×3칸으로(FIX_EYE).
# 결과(한 칸 2px, 가로 1행, 발끝 = 아래 끝, 칸 가운데 = 발 가운데):
#   ../wombat_<front|side|back>_thump.png  웅크림 · 폴짝 · 쿵 세 칸. 순서 THUMP(웅크림 2 → 폴짝 3 → 쿵 3, 0.5초 = FishingConfigTable thumpSeconds ÷ 8)
#   ../wombat_<front|side|back>_sit.png    앉는 중 · 앉음 두 칸. 털썩은 앉는 중 2칸 → 앉음, 앉아 있는 동안 앉음 칸 그대로
# 사용: python make_thump.py   (Windows Python · Pillow · numpy)
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'shop_raw')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, HERE)
import snap_codex  # noqa: E402
from make_dig import blobs, split  # noqa: E402

VIEWS = ['front', 'side', 'back']
CW = 256                                  # 바탕(shop_raw/thump_ref_sheet.png) 칸 폭 px
THUMP = [0, 0, 1, 1, 1, 2, 2, 2]
SIT = [0, 0, 1]
FIX_EYE = ('front', 3, [(14, 13, 29), (14, 14, 28), (15, 13, 29), (15, 14, 28), (16, 13, 29), (16, 14, 28)])   # (줄, 고칠 열, 본뜰 오른쪽 눈 열)


def band_cells(img, period):
    g = img.mean(2)
    px, phx = snap_codex.grid(np.abs(np.diff(g, axis=1)).sum(0), period, 0.01)
    py, phy = snap_codex.grid(np.abs(np.diff(g, axis=0)).sum(1), period, 0.01)
    h, w = g.shape
    xs, ys = np.arange(phx + 1, w, px), np.arange(phy + 1, h, py)
    c = np.zeros((len(ys) - 1, len(xs) - 1, 4), np.uint8)
    for j in range(len(ys) - 1):
        for k in range(len(xs) - 1):
            y0, y1, x0, x1 = ys[j], ys[j + 1], xs[k], xs[k + 1]
            c[j, k, :3] = np.median(img[int(y0 + (y1 - y0) * 0.25):int(y1 - (y1 - y0) * 0.25) + 1, int(x0 + (x1 - x0) * 0.25):int(x1 - (x1 - x0) * 0.25) + 1].reshape(-1, 3), 0).round()
            c[j, k, 3] = 255
    rgb = c[..., :3].astype(int)
    light = (rgb.min(-1) > 238) | ((rgb.max(-1) - rgb.min(-1) < 10) & (rgb.min(-1) > 190))
    bg = np.zeros_like(light)
    H, W = light.shape
    stack = [(j, k) for j in range(H) for k in (0, W - 1) if light[j, k]] + [(j, k) for k in range(W) for j in (0, H - 1) if light[j, k]]
    while stack:
        j, k = stack.pop()
        if bg[j, k]:
            continue
        bg[j, k] = True
        for nj, nk in ((j - 1, k), (j + 1, k), (j, k - 1), (j, k + 1)):
            if 0 <= nj < H and 0 <= nk < W and light[nj, nk] and not bg[nj, nk]:
                stack.append((nj, nk))
    for gr in blobs(light & ~bg, diag=False):
        if len(gr) > 40:
            for j, k in gr:
                bg[j, k] = True
    c[bg, 3] = 0
    return c, xs[:-1] + px / 2


def poses(path):
    """시트 → {방향: [포즈 여섯 dict(cells, lift, foot, mid)]}"""
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    period = snap_codex.snap(path, cell=5, square=True, min_hole=40, raw=True)[1][0]
    out = {}
    for (y0b, y1b), view in zip(split((a.min(2) < 235).any(1), 3), VIEWS):
        c, cx = band_cells(a[max(y0b - 8, 0):y1b + 8], period)
        parts = [gr for gr in blobs(c[..., 3] > 0, diag=True) if len(gr) >= 3]
        slots = [[] for _ in range(6)]
        for gr in sorted((g for g in parts if len(g) >= 150), key=len, reverse=True):
            slots[min(5, int(np.mean([cx[x] for _, x in gr]) // CW))].append(gr)
        for gr in parts:
            if len(gr) < 150:
                gy, gx = np.mean([y for y, _ in gr]), np.mean([x for _, x in gr])
                d = [min(abs(gy - y) + abs(gx - x) for y, x in s[0][::7]) if s else 1e9 for s in slots]
                slots[int(np.argmin(d))].append(gr)
        base = int(np.median([max(y for y, _ in slots[i][0]) for i in (0, 2, 3, 4, 5)]))
        res = []
        for s in slots:
            ys = [y for gr in s for y, _ in gr]; xs = [x for gr in s for _, x in gr]
            y0, x0 = min(ys), min(xs)
            p = np.zeros((max(ys) - y0 + 1, max(xs) - x0 + 1, 4), np.uint8)
            for gr in s:
                for y, x in gr:
                    p[y - y0, x - x0] = c[y, x]
            by = max(y for y, _ in s[0])
            bx = [x for y, x in s[0] if y >= by - 2]
            lift = base - by
            res.append(dict(cells=p, lift=lift if lift > 3 else 0, foot=by - y0, mid=(min(bx) + max(bx) + 1) / 2 - x0))
        out[view] = res
    return out


def grounded(q):
    p = q['cells'].copy()
    parts = sorted(blobs(p[..., 3] > 0, diag=True), key=len, reverse=True)
    ys = [y for y, _ in parts[0]]; xs = [x for _, x in parts[0]]
    for gr in parts[1:]:
        if not any(min(ys) - 2 <= y <= max(ys) + 2 and min(xs) - 2 <= x <= max(xs) + 2 for y, x in gr):
            for y, x in gr:
                p[y, x, 3] = 0
    yy, xx = np.nonzero(p[..., 3])
    return dict(cells=p[yy.min():yy.max() + 1, xx.min():xx.max() + 1], lift=0, foot=q['foot'] - yy.min(), mid=q['mid'] - xx.min())


def frame(q, W, H):
    f = np.zeros((H, W, 4), np.uint8)
    p = q['cells']
    x0, y0 = int(round(W / 2 - q['mid'])), H - 1 - q['lift'] - q['foot']
    for y, x in zip(*np.nonzero(p[..., 3])):
        if 0 <= y0 + y < H and 0 <= x0 + x < W:
            f[y0 + y, x0 + x] = p[y, x]
    return f


def size(qs):
    W = max(int(np.ceil(max(q['mid'], q['cells'].shape[1] - q['mid']) * 2)) for q in qs) + 2
    H = max(q['foot'] + 1 + q['lift'] for q in qs) + 1        # 발끝 아래(흙먼지 끝)는 자른다(굴 파기와 같다)
    return W + W % 2, H


def main():
    a = poses(os.path.join(RAW, 'thump_sheet_a.png'))
    fix = poses(os.path.join(RAW, 'thump_sit_fix.png'))
    view, i, cells = FIX_EYE
    q = grounded(fix[view][i])
    for y, x, src in cells:
        q['cells'][y, x] = q['cells'][y, src]
    sits = {v: [grounded(f) for f in fix[v][3:5]] for v in VIEWS}
    sits[view][i - 3] = q
    for name, sets in (('thump', {v: a[v][:3] for v in VIEWS}), ('sit', sits)):
        W, H = size([q for v in VIEWS for q in sets[v]])
        for v in VIEWS:
            sheet = np.concatenate([frame(q, W, H) for q in sets[v]], 1)
            Image.fromarray(np.repeat(np.repeat(sheet, 2, 0), 2, 1), 'RGBA').save(os.path.join(HERE, '..', 'wombat_%s_%s.png' % (v, name)))
        print('wombat_<view>_%s.png  %d칸 × 칸 %d×%d칸 = 칸 폭 %dpx' % (name, len(sets['front']), W, H, W * 2))


if __name__ == '__main__':
    main()
