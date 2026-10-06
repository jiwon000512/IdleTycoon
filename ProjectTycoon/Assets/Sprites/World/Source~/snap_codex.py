# -*- coding: utf-8 -*-
# Codex 시안 → 게임 칸 그림(원본 그대로, 2026-09-30 사용자 결정). 원본 그림의 픽셀 격자(주기 · 시작점)를 찾아 칸마다 가운데 색을 그대로 가져온다.
# 칸 수를 짐작해 주지 않고, 드문 색을 버리지 않는다(한 칸짜리 눈 · 입 · 싹이 살아남는다). 손으로 다시 그리지 않는다.
#   1. 격자: 외곽선 두께 = 한 칸이라 진한 구간 길이의 최빈값으로 대략의 칸 크기를 잡고(원본마다 10~20px로 다름), 그 ±15% 안에서
#      칸 경계(밝기가 크게 바뀌는 곳)를 가장 잘 짚는 (주기, 시작점)을 찾는다. 넓은 범위를 그냥 찾으면 절반 · 두 배 주기에 속는다.
#   2. 칸 색: 칸 가운데 50%의 중앙값.
#   3. 흰 바탕: 가장자리에서 이어진 흰 칸 + 안쪽에 갇힌 순백 칸(구멍)은 투명.
#   --raw(새 그림, 2026-10-05 사용자 「그림 퀄리티에 변형이 생기는 규칙은 다 지운다」): 외곽선 밖으로 번진 흰 바탕 칸(밝고 이웃이 모두 어두운 가장자리 칸)만 더 지우고 끝. 원본 픽셀 크기 · 색 그대로.
#     원본이 잘게(반 칸) 그려졌어도 줄이지 않는다: 바탕 한 칸(10px) = 2텍셀이므로 원본 픽셀 하나 = 2 × 주기/10 텍셀(반 칸이면 --px=1).
#   raw가 아닐 때(이미 들어간 옛 그림을 그대로 다시 만들 때만): 투명과 맞닿은 흰빛 번짐 칸을 두 번 벗기고, 거리 24 안은 한 색,
#     --colors보다 많으면 Lab 거리로 가장 가까운 두 색을 합치고, 가장 어두운 색과 맨 바깥 칸을 외곽선 (52,32,32)으로.
# 사용: snap_codex.py <원본.png> <출력.png> [--raw] [--square] [--colors=12] [--px=2] [--cell=가로[,세로] 대략 칸 크기 px]   출력은 원본 픽셀 하나 = px 픽셀, 여백을 잘라 저장
import sys
import numpy as np
from PIL import Image

LINE = (52, 32, 32)


def outline_cell(dark):
    # 외곽선 두께 = 한 칸: 진한 칸이 이어지는 길이(3~60px)의 최빈값을 대략의 칸 크기로
    runs = []
    for row in dark[::3]:
        x = 0
        while x < len(row):
            if row[x]:
                start = x
                while x < len(row) and row[x]:
                    x += 1
                if 3 <= x - start <= 60:
                    runs.append(x - start)
            x += 1
    counts = np.bincount(runs)
    # 이웃 길이까지 묶어서(13 · 14처럼 갈라지는 것) 가장 많은 곳
    smooth = counts + np.concatenate([[0], counts[:-1]]) + np.concatenate([counts[1:], [0]])
    return float(smooth.argmax())


def grid(edges, around, span=0.15):
    # 대략의 칸 크기 ±span 안에서, 칸 경계(밝기가 크게 바뀌는 곳)를 가장 잘 짚는 (주기, 시작점)
    top = (0.0, around, 0.0)
    for period in np.arange(around * (1 - span), around * (1 + span), 0.02):
        for phase in np.arange(0, period, 0.25):
            idx = np.round(np.arange(phase, len(edges), period)).astype(int)
            score = edges[idx[idx < len(edges)]].mean()
            if score > top[0]:
                top = (score, period, phase)
    return top[1], top[2]


def merge_colors(a, mask, limit, mean=False):
    # mean: 합쳤을 때 늘어나는 오차(거리 × 두 색 칸 수의 조화 평균, Ward)가 가장 작은 짝을 가중 평균한 색으로 합친다.
    #   기본(가장 가까운 짝, 적은 색을 많은 색으로)은 나무결처럼 톤이 여럿인 넓은 면에서 되풀이될수록 한쪽으로 쏠리고, 색이 많은 그림(수레)에서 벨벳 같은 면이 사라진다
    while True:
        cols, counts = np.unique(a[mask][:, :3], axis=0, return_counts=True)
        if len(cols) <= limit:
            return a
        lab = np.asarray(Image.fromarray(cols.reshape(1, -1, 3).astype(np.uint8), 'RGB').convert('LAB')).reshape(-1, 3).astype(float)
        lab[:, 1:] = (lab[:, 1:] - 128) * 2.0
        d = ((lab[:, None, :] - lab[None, :, :]) ** 2).sum(2)
        if mean:
            d = d * (counts[:, None] * counts[None, :]) / (counts[:, None] + counts[None, :])
        np.fill_diagonal(d, 1e18)
        i, j = np.unravel_index(d.argmin(), d.shape)
        if mean:
            c = np.round((cols[i] * counts[i] + cols[j] * counts[j]) / (counts[i] + counts[j]))
            for k in (i, j):
                a[mask & (a[..., :3] == cols[k]).all(2), :3] = c
            continue
        src, dst = (i, j) if counts[i] < counts[j] else (j, i)
        a[mask & (a[..., :3] == cols[src]).all(2), :3] = cols[dst]


def snap(path, max_colors=12, cell=None, fixed=None, square=False, min_hole=1, mean_merge=False, raw=False):
    # raw: 원본 그대로(2026-10-05 사용자 「그림 퀄리티에 변형이 생기는 규칙은 다 지운다」). 칸 색은 칸 가운데 중앙값 그대로,
    #   밝은 가장자리 벗기기 · 색 합치기(max_colors · mean_merge) · 외곽선 색 바꾸기를 하지 않는다. 바탕(가장자리에서 이어진 흰 칸) · 구멍만 투명.
    #   새 그림은 raw=True. 기본값(raw=False)은 이미 들어간 그림을 그대로 다시 만들 때의 옛 동작
    # min_hole: 안쪽에 갇힌 순백 덩이가 이 칸 수 이상이면 구멍(투명). 흰 하이라이트 한 획이 있는 그림(수레 덮개 등)은 크게 준다
    #   (하이라이트가 뚫리면 옆의 밝은 천까지 번짐으로 벗겨져 흰 얼룩이 된다). mean_merge: merge_colors의 mean
    # square: 원본 픽셀이 정사각형인 그림(한 장에 큰 물체 하나). 세로 주기를 가로 주기 ±2% 안에서만 찾아, 가로 · 세로가 따로 잡혀 찌그러지는 것을 막는다
    a = np.asarray(Image.open(path).convert('RGB')).astype(float)
    h, w = a.shape[:2]
    g = a.mean(2)
    dark = a.sum(2) < 250
    # cell: 대략의 칸 크기를 직접 줄 때(외곽선이 얇게 그려져 최빈값이 틀린 원본, 또는 이미 고른 크기를 지킬 때)
    if isinstance(cell, (int, float)):
        cell = (cell,)
    cx, cy = (cell * 2)[:2] if cell else (None, None)
    if fixed:
        # 이미 고른 그림을 그대로 다시 만들 때: (가로 주기, 가로 시작, 세로 주기, 세로 시작)
        px_, phx, py_, phy = fixed
    else:
        px_, phx = grid(np.abs(np.diff(g, axis=1)).sum(0), cx or outline_cell(dark))
        if square:
            py_, phy = grid(np.abs(np.diff(g, axis=0)).sum(1), px_, 0.02)
        else:
            py_, phy = grid(np.abs(np.diff(g, axis=0)).sum(1), cy or outline_cell(dark.T))
    xs, ys = np.arange(phx + 1, w, px_), np.arange(phy + 1, h, py_)
    cw, ch = len(xs) - 1, len(ys) - 1
    cells = np.zeros((ch, cw, 3))
    for j in range(ch):
        for i in range(cw):
            x0, x1, y0, y1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            block = a[int(y0 + (y1 - y0) * 0.25):int(y1 - (y1 - y0) * 0.25) + 1, int(x0 + (x1 - x0) * 0.25):int(x1 - (x1 - x0) * 0.25) + 1]
            cells[j, i] = np.median(block.reshape(-1, 3), 0)
    ink = np.abs(cells - 255).sum(2) > 45
    bg = np.zeros_like(ink)
    stack = [(j, i) for j in range(ch) for i in (0, cw - 1) if not ink[j, i]] + [(j, i) for i in range(cw) for j in (0, ch - 1) if not ink[j, i]]
    while stack:
        j, i = stack.pop()
        if bg[j, i]:
            continue
        bg[j, i] = True
        for nj, ni in ((j - 1, i), (j + 1, i), (j, i - 1), (j, i + 1)):
            if 0 <= nj < ch and 0 <= ni < cw and not ink[nj, ni] and not bg[nj, ni]:
                stack.append((nj, ni))
    mask = ~bg
    # 안쪽에 갇힌 흰 바탕(손잡이 구멍 · 자루 사이 틈): 순백에 가까운 칸은 투명(시안의 하이라이트는 순백이 아니다)
    white = mask & (cells.min(2) > 240)
    seen = np.zeros_like(white)
    for j0, i0 in zip(*np.nonzero(white)):
        if seen[j0, i0]:
            continue
        blob, stack = [], [(j0, i0)]
        seen[j0, i0] = True
        while stack:
            j, i = stack.pop()
            blob.append((j, i))
            for nj, ni in ((j - 1, i), (j + 1, i), (j, i - 1), (j, i + 1)):
                if 0 <= nj < ch and 0 <= ni < cw and white[nj, ni] and not seen[nj, ni]:
                    seen[nj, ni] = True
                    stack.append((nj, ni))
        if len(blob) >= min_hole:
            for j, i in blob:
                mask[j, i] = False
    if raw:
        # 바탕 번짐: 바깥 가장자리의 밝은 칸(최솟값 200 넘음) 중 불투명 이웃이 모두 어두운 외곽선(합 330 아래)인 칸은
        # 외곽선 밖으로 번진 흰 바탕이라 투명(크림 천막 끝처럼 이웃이 밝은 칸은 그림이라 남긴다)
        pad = np.pad(mask, 1)
        edge = mask & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
        dark = cells.sum(2) < 330
        for j, i in zip(*np.nonzero(edge & (cells.min(2) > 200))):
            nb = [(j + dj, i + di) for dj, di in ((-1, 0), (1, 0), (0, -1), (0, 1)) if 0 <= j + dj < ch and 0 <= i + di < cw and mask[j + dj, i + di]]
            if nb and all(dark[q] for q in nb):
                mask[j, i] = False
        out = np.zeros((ch, cw, 4), np.uint8)
        out[..., :3] = cells.round().astype(np.uint8)
        out[..., 3] = np.where(mask, 255, 0)
        ys_, xs_ = np.nonzero(mask)
        return out[ys_.min():ys_.max() + 1, xs_.min():xs_.max() + 1], (px_, phx, py_, phy)
    for _ in range(2):
        pad = np.pad(mask, 1)
        edge = ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
        mask &= ~((cells.min(2) > 200) & edge)
    rgb = cells.round().astype(int)
    palette = []
    for c in rgb[mask]:
        if not any(np.abs(p - c).sum() < 24 for p in palette):
            palette.append(c)
    palette = np.array(palette)
    rgb = palette[((rgb[..., None, :] - palette[None, None]) ** 2).sum(-1).argmin(-1)]
    out = np.zeros((ch, cw, 4), np.int64)
    out[..., :3] = rgb
    out[..., 3] = np.where(mask, 255, 0)
    out = merge_colors(out, mask, max_colors, mean_merge)
    cols = np.unique(out[mask][:, :3], axis=0)
    out[mask & (out[..., :3] == cols[cols.sum(1).argmin()]).all(2), :3] = LINE
    pad = np.pad(mask, 1)
    border = mask & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    out[border, :3] = LINE
    ys_, xs_ = np.nonzero(mask)
    return out[ys_.min():ys_.max() + 1, xs_.min():xs_.max() + 1].astype(np.uint8), (px_, phx, py_, phy)


if __name__ == '__main__':
    args = [x for x in sys.argv[1:] if not x.startswith('--')]
    opts = dict((x[2:].split('=') + ['1'])[:2] for x in sys.argv[1:] if x.startswith('--'))
    cells, g = snap(args[0], int(opts.get('colors', 12)), tuple(float(v) for v in opts['cell'].split(',')) if 'cell' in opts else None, raw='raw' in opts, square='square' in opts)
    px = int(opts.get('px', 2))
    Image.fromarray(np.repeat(np.repeat(cells, px, 0), px, 1)).save(args[1])
    n = len(np.unique(cells[cells[..., 3] > 0][:, :3], axis=0))
    print('grid', round(g[0], 2), round(g[2], 2), 'cells', cells.shape[1], 'x', cells.shape[0], 'colors', n, '->', args[1])
