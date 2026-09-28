# -*- coding: utf-8 -*-
# 부위 조립 애니메이션(칸 편집, AI 없음, 2026-09-29 걷기 B3 확정). 정지 그림 한 장을 부위로 나눠 프레임마다 다시 조립한다.
#   몸: 줄 겹치기(늘어남) · 몸 전체 오르내림(발 위에서, 뜨면 발 윗줄을 늘려 다리로 잇는다) · 발밑 축 기울기(8×8 다수결, 눈은 떼었다 찍는다)
#   귀·앞발·꼬리: 한 박자 늦게(귀는 귀 열에서 줄을 겹치거나 빼고, 앞발·꼬리는 블록을 1칸 올리고 내린다)
#   발: 떼어 들고(앞·뒤) 앞뒤로 옮긴다(옆, 먼 발은 어둡게 뒤에). 옮긴 뒤 드러난 가장자리는 외곽선으로 닫는다
# 부위 좌표는 anim_specs.py, 프레임 순서·시간은 make_anim.py
import numpy as np
from PIL import Image

PAD_T, PAD_X = 6, 5   # 캔버스 위 여백(늘어남·오르내림) · 좌우 여백(기울기·발 옮기기). 발끝 = 캔버스 맨 아랫줄


def load(path):
    """2px 칸 그림 → 칸 배열(RGBA, 알파 0/255)"""
    a = np.asarray(Image.open(path).convert('RGBA'))[::2, ::2].copy()
    a[a[..., 3] < 128] = 0
    a[a[..., 3] >= 128, 3] = 255
    return a


def save(a, path):
    Image.fromarray(a, 'RGBA').resize((a.shape[1] * 2, a.shape[0] * 2), Image.NEAREST).save(path)


def opaque(a):
    return a[..., 3] > 0


def boundary(a):
    op = opaque(a)
    pad = np.pad(op, 1)
    inner = pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    return op & ~inner


def remove_row(a, r):
    """r 줄을 빼고 위를 1칸 내린다. 높이는 그대로"""
    return np.concatenate([np.zeros_like(a[:1]), a[:r], a[r + 1:]])


def dup_row(a, r):
    """r 줄을 겹쳐 위를 1칸 올린다(늘어남). 맨 윗줄은 비어 있어야 한다"""
    return np.concatenate([a[1:r + 1], a[r:r + 1], a[r + 1:]])


def shift_block_v(a, r0, r1, c0, c1, d):
    """r0~r1 · c0~c1 블록을 세로로 d칸(+ 아래) 옮기고 빈 줄은 블록 가장자리 줄로 메운다. 몸 안쪽 블록에만 쓴다"""
    if d == 0:
        return a
    b = a.copy()
    blk = a[r0:r1 + 1, c0:c1 + 1].copy()
    b[r0 + d:r1 + 1 + d, c0:c1 + 1] = blk
    if d < 0:
        b[r1 + 1 + d:r1 + 1, c0:c1 + 1] = blk[-1:]
    else:
        b[r0:r0 + d, c0:c1 + 1] = blk[:1]
    return b


def ear_lag(a, bottom, ranges, d):
    """귀만 몸보다 한 박자 늦게: -1 = 귀가 1칸 위(길어 보임), +1 = 1칸 아래(짧아 보임)"""
    if d == 0:
        return a
    b = a.copy()
    for c0, c1 in ranges:
        col = a[:, c0:c1 + 1]
        b[:, c0:c1 + 1] = dup_row(col, bottom) if d < 0 else remove_row(col, bottom)
    return b


def tilt(a, lean, eyes, outline, k=8):
    """몸 전체를 발밑 가운데를 축으로 돌린다: 맨 위가 lean칸 옆으로(+ 오른쪽).
    칸마다 k×k 점을 거꾸로 돌려 원본 칸을 찾고 다수결로 색을 정한다(새 색이 안 생기고, 윤곽 계단이 가로줄 하나에 몰리지 않는다).
    눈은 돌리면 모양이 깨져서 떼어 두었다가 돌린 자리에 그대로 찍는다"""
    if lean == 0:
        return a
    a = a.copy()
    op = opaque(a)
    ys, _ = np.nonzero(op)
    top, bottom = ys.min(), ys.max()
    row = np.nonzero(op[bottom])[0]
    px, py = (row.min() + row.max() + 1) / 2.0, bottom + 1.0
    t = np.arctan2(lean, py - top)
    stamps = []
    for r0, r1, c0, c1 in eyes:
        box = a[r0:r1 + 1, c0:c1 + 1]
        m = (box[..., :3] == outline).all(-1) & (box[..., 3] > 0)
        if not m.any():
            continue
        ring = [tuple(v) for v in a[r0 - 1:r1 + 2, c0 - 1:c1 + 2].reshape(-1, 4) if v[3] and tuple(v[:3]) != tuple(outline)]
        fill = max(set(ring), key=ring.count)
        cells = [(r0 + y, c0 + x, a[r0 + y, c0 + x].copy()) for y, x in zip(*np.nonzero(m))]
        for y, x, _ in cells:
            a[y, x] = fill
        cy = np.mean([c[0] for c in cells]) + 0.5
        cx = np.mean([c[1] for c in cells]) + 0.5
        dxv, dyu = cx - px, -(cy - py)
        nx = dxv * np.cos(t) + dyu * np.sin(t) + px
        ny = -(-dxv * np.sin(t) + dyu * np.cos(t)) + py
        stamps.append((cells, int(round(nx - cx)), int(round(ny - cy))))
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H * k, 0:W * k]
    X = (xx + 0.5) / k - px
    Yu = -((yy + 0.5) / k - py)
    ix = np.floor(X * np.cos(t) - Yu * np.sin(t) + px).astype(int)
    iy = np.floor(-(X * np.sin(t) + Yu * np.cos(t)) + py).astype(int)
    ok = (ix >= 0) & (ix < W) & (iy >= 0) & (iy < H)
    packed = (a[..., 0].astype(np.int64) << 24) | (a[..., 1].astype(np.int64) << 16) | (a[..., 2].astype(np.int64) << 8) | a[..., 3]
    key = np.zeros((H * k, W * k), np.int64)
    key[ok] = packed[iy[ok], ix[ok]]
    blocks = key.reshape(H, k, W, k).transpose(0, 2, 1, 3).reshape(H, W, k * k)
    out = np.zeros_like(a)
    for y in range(H):
        for x in range(W):
            vals, cnt = np.unique(blocks[y, x], return_counts=True)
            v = int(vals[np.argmax(cnt)])
            out[y, x] = [(v >> 24) & 255, (v >> 16) & 255, (v >> 8) & 255, v & 255]
    for cells, ox, oy in stamps:
        for y, x, c in cells:
            out[y + oy, x + ox] = c
    return out


def paste(canvas, img, y, x):
    h, w = img.shape[:2]
    for yy in range(h):
        for xx in range(w):
            cy, cx = y + yy, x + xx
            if img[yy, xx, 3] and 0 <= cy < canvas.shape[0] and 0 <= cx < canvas.shape[1]:
                canvas[cy, cx] = img[yy, xx]


class Sprite:
    """한 방향의 정지 그림과 부위(anim_specs 좌표 = 원본 칸)"""

    def __init__(self, path, spec):
        self.s = spec
        self.base = load(path)
        self.palette = {tuple(int(v) for v in c) for c in self.base[opaque(self.base)][:, :3]}
        self.outline = min(self.palette, key=sum)
        h, w = self.base.shape[:2]
        self.H, self.W = h + PAD_T, w + 2 * PAD_X
        c = self.canvas(self.base)
        self._edge = {(y, x, tuple(int(v) for v in c[y, x, :3])) for y, x in zip(*np.nonzero(boundary(c)))}

    def canvas(self, body):
        c = np.zeros((self.H, self.W, 4), np.uint8)
        c[PAD_T:, PAD_X:PAD_X + self.base.shape[1]] = body
        return c

    def feet_parts(self):
        """발(떼어 낸 조각 · 원본 윗줄 · 왼쪽 열 · 정의)과 발을 뺀 몸"""
        body = self.base.copy()
        feet = []
        for f in self.s['feet']:
            if f.get('far'):
                # 먼 발: 가까운 발을 어둡게 칠해 뒤에 둔다
                near, nr0, nc0, _ = feet[f['from']]
                part = near.copy()
                for src, dst in f['recolor']:
                    part[(part[..., :3] == src).all(-1) & opaque(part), :3] = dst
                cy, cx, col = f['claw']
                part[cy, cx, :3] = col
                feet.append((part, nr0 + f['dy'], nc0 + f['dx'], f))
                continue
            (r0, r1), (c0, c1) = f['rows'], f['cols']
            part = self.base[r0:r1 + 1, c0:c1 + 1].copy()
            body[r0:r1 + 1, c0:c1 + 1][opaque(part)] = 0
            feet.append((part, r0, c0, f))
        for r in self.s.get('clear_rows', []):
            body[r] = 0
        return body, feet

    def leg_row(self, part, r0, c0, f):
        """몸이 발보다 떴을 때 발 위에 잇는 다리 한 줄: 기본은 발 윗줄, 'above'면 발 바로 위 몸 줄(양 끝은 외곽선)"""
        if f.get('leg') != 'above':
            return part[:1]
        row = self.base[r0 - 1:r0, c0:c0 + part.shape[1]].copy()
        xs = np.nonzero(opaque(part[:1])[0])[0]
        row[0, :xs.min()] = 0
        row[0, xs.max() + 1:] = 0
        row[0, xs.min(), :3] = self.outline
        row[0, xs.max(), :3] = self.outline
        return row

    def frame(self, body_mode=0, body_y=0, lifts=(0, 0), xs=(0, 0), ear=0, paw=0, tail=0, tilt_cells=0, blink=False):
        s = self.s
        body, feet = self.feet_parts()
        if blink:
            fill = self.base[s['blink_fill'][0], s['blink_fill'][1]]
            line = np.array(list(self.outline) + [255], np.uint8)
            for r, c, ch in s['blink']:
                body[r, c] = fill if ch == '.' else line
        if paw and 'paw' in s:
            body = shift_block_v(body, *s['paw'], paw)
        if tail and 'tail' in s:
            body = shift_block_v(body, *s['tail'], tail)
        cb = self.canvas(body)
        cb = ear_lag(cb, s['ear'] + PAD_T, [(c0 + PAD_X, c1 + PAD_X) for c0, c1 in s['ears']], ear)
        for _ in range(body_mode):
            cb = dup_row(cb, s['stretch'] + PAD_T)
        if tilt_cells:
            # 눈 찾을 상자(줄 연산으로 1칸 움직였을 수 있어 위아래 1칸 넓게)
            eyes = [(r0 + PAD_T - 1, r1 + PAD_T + 1, c0 + PAD_X, c1 + PAD_X) for r0, r1, c0, c1 in s.get('eyes', [])]
            cb = tilt(cb, tilt_cells, eyes, self.outline)
        if body_y > 0:
            cb = np.concatenate([cb[body_y:], np.zeros_like(cb[:body_y])])
        elif body_y < 0:
            cb = np.concatenate([np.zeros_like(cb[:-body_y]), cb[:body_y]])

        def foot(i):
            part, r0, c0, f = feet[i]
            ext = body_y - lifts[i]
            if ext > 0:
                part = np.concatenate([self.leg_row(part, r0, c0, f)] * ext + [part])
                r0 -= ext
            return part, PAD_T + r0 - lifts[i], PAD_X + c0 + xs[i]

        out = np.zeros_like(cb)
        # 뒤 발 → 몸 → 앞 발
        for i, f in enumerate(feet):
            if f[3].get('z', 1) < 1:
                paste(out, *foot(i))
        paste(out, cb, 0, 0)
        for i, f in enumerate(feet):
            if f[3].get('z', 1) >= 1:
                paste(out, *foot(i))
        return self.close_outline(out)

    def close_outline(self, a):
        """드러난 가장자리(투명과 맞닿은 칸)가 외곽선이 아니면 외곽선으로. 정지 그림에서 원래 그런 칸은 둔다"""
        b = a.copy()
        for y, x in zip(*np.nonzero(boundary(a))):
            c = tuple(int(v) for v in a[y, x, :3])
            if c != self.outline and (y, x, c) not in self._edge:
                b[y, x, :3] = self.outline
                b[y, x, 3] = 255
        return b

    def check(self, frames, name, lifted=()):
        """자동 검사: 정지 그림 팔레트 밖 색 없음 · 두 발을 딛은 프레임의 발끝 줄이 같음 · 가장자리는 외곽선"""
        issues = []
        bottom = None
        for i, f in enumerate(frames):
            extra = {tuple(int(v) for v in c) for c in f[opaque(f)][:, :3]} - self.palette
            if extra:
                issues.append(f'{name}[{i}] palette {extra}')
            low = np.nonzero(opaque(f).any(1))[0].max()
            if i not in lifted:
                bottom = low if bottom is None else bottom
                if low != bottom:
                    issues.append(f'{name}[{i}] baseline {low} != {bottom}')
            bad = [1 for y, x in zip(*np.nonzero(boundary(f)))
                   if tuple(int(v) for v in f[y, x, :3]) != self.outline and (y, x, tuple(int(v) for v in f[y, x, :3])) not in self._edge]
            if bad:
                issues.append(f'{name}[{i}] edge {len(bad)}')
        return issues
