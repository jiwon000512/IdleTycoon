# -*- coding: utf-8 -*-
# 웜뱃 굴 파기 동작 「웅크려 퍼 던지기」(2026-10-05 사용자 선택 2차 A). 파기 버튼을 누를 때 한 번: 웅크려 앞발로 두 번 긁고 → 펄쩍 일어나 머리 위로 흙을 던진다.
#   부위 조립 시안은 사용자 「기존거 응용한것 같은데 새로 만들어봐」로 버리고, 새 자세는 Codex로 그렸다(프롬프트 shop_raw/dig_prompt.txt).
#   1차는 앞모습이 정수리를 보이게 숙여서 지금 웜뱃 얼굴을 덧붙였는데 광대처럼 보여(사용자 「정면 두번째 세번째가 광대가 있는듯한」) 처음부터 다시 그렸다.
#   2차는 시안마다 앞 · 옆 · 뒤를 한 장에 그리게 했고, 앞모습은 머리를 숙이지 않고 몸을 웅크린다. 덧붙이기 · 손질 없음
#   원본 shop_raw/dig_sheet_raw.png: 3줄(앞 · 옆 · 뒤) × 4칸(웅크려 짚기 · 배 쪽으로 긁기 · 던지기 · 서기). 넷째 칸은 크기 기준이라 쓰지 않는다.
#   1. 줄마다 띠 하나를 원본 격자 그대로 칸으로(snap_codex) → 같은 줄의 발 기준선이 지켜진다. 띠 안에서 네 장으로 나눈다.
#      시트 한 장이라 줄마다 찾은 주기의 중앙값 하나로 옮긴다(앞 줄 주기가 두 배로 잡혀 반 크기가 되던 것 막기)
#   2. 색은 방향마다 지금 웜뱃(wombat_<방향>_base)의 6~7색으로. 어두운 갈색(안쪽 선 · 흙 부스러기)은 외곽선
#   3. 던지기 장면 앞 · 옆 눈웃음은 서 있을 때 눈으로 바꾼다(OPEN_EYES, 칸 단위)
#   4. 발끝(몸 덩어리 맨 아랫줄)을 지금 웜뱃 숨쉬기 프레임의 발끝에 맞춘다(앞 · 뒤는 아래 세 줄 가운데, 옆은 뒤꿈치).
#      발끝 아래(앞으로 쏟아진 흙)는 자른다. 숙인 옆모습이 앞으로 길게 나와 칸 폭이 104px보다 넓다(세 방향에 맞춰 main이 정한다, 지금 60칸 = 120px)
# 결과: ../wombat_<방향>_dig.png(가로 1행, 칸 폭 120px, 발끝 = 아래 끝, 칸 가운데 = 숨쉬기 그림의 피벗 열). 프레임은 SEQ 순서, 한 칸 60ms
# 게임 연결(프로그래밍방): BakeryBaker가 VisitorSheetImporter.Import(경로, true, 120)로 자르고, WombatView가 Events.Dug(빵집 · 농장 칸 · 층 파기)
#   때 지금 방향 시트를 한 번 돌린다(그동안 웜뱃은 제자리). 한 칸 시간은 ConfigTable wombatDigSeconds(0.72초) ÷ 12, 던지기는 9번째 프레임(0.48초)부터라 흙덩이(DigView.Clods)를 거기 맞춘다
# 사용: python make_dig.py [미리보기 폴더]   (Windows Python · Pillow · numpy)
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'shop_raw')
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
from anim_parts import Sprite, save  # noqa: E402
from anim_specs import WOMBAT  # noqa: E402
from make_tanuki import snap_fixed  # noqa: E402
from make_anim import idle_frames, IDLE_DUR  # noqa: E402

VIEWS = ('front', 'side', 'back')
SEQ = [0, 0, 1, 1, 0, 0, 1, 1, 2, 2, 2, 2]   # 긁기 두 번(장면 0 · 1 번갈아) → 던지기, 한 칸 60ms = 0.72초
TICK = 60   # 미리보기 GIF용. 게임은 ConfigTable wombatDigSeconds ÷ len(SEQ)
INK, FUR = (36, 24, 24), (192, 168, 144)
EAR_COLOR = (168, 108, 96)


def split(mask_1d, n):
    """참인 구간을 가장 넓은 빈틈 n-1개에서 나눠 n 구간(시작, 끝)"""
    xs = np.nonzero(mask_1d)[0]
    gaps = sorted(((xs[i + 1] - xs[i], i) for i in range(len(xs) - 1)), reverse=True)[:n - 1]
    cut = sorted(i for _, i in gaps)
    b = [xs[0]] + [v for i in cut for v in (xs[i] + 1, xs[i + 1])] + [xs[-1] + 1]
    return [(b[2 * k], b[2 * k + 1]) for k in range(n)]


def blobs(m, diag=False):
    seen = np.zeros_like(m)
    out = []
    steps = [(dy, dx) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dy or dx) and (diag or not (dy and dx))]
    for y, x in zip(*np.nonzero(m)):
        if seen[y, x]:
            continue
        st, cells = [(y, x)], []
        seen[y, x] = True
        while st:
            cy, cx = st.pop()
            cells.append((cy, cx))
            for dy, dx in steps:
                ny, nx = cy + dy, cx + dx
                if 0 <= ny < m.shape[0] and 0 <= nx < m.shape[1] and m[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    st.append((ny, nx))
        out.append(cells)
    return out


def clear_white(a, keep=5):
    """안쪽에 갇힌 흰 칸 덩어리 → 투명: keep칸보다 크거나, 둘레의 절반 이상이 외곽선(앞발과 머리 사이에 갇힌 바탕).
    털에 둘러싸인 하이라이트 한 획만 남는다"""
    a = a.copy()
    light = (a[..., 3] > 0) & (a[..., :3].min(2) >= 235)
    dark = (a[..., 3] > 0) & (a[..., :3].max(2) < 110)
    for cells in blobs(light):
        own = set(cells)
        rim = {(y + dy, x + dx) for y, x in cells for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1))} - own
        rim = [(y, x) for y, x in rim if 0 <= y < a.shape[0] and 0 <= x < a.shape[1]]
        inked = sum(dark[y, x] or a[y, x, 3] == 0 for y, x in rim)
        if len(cells) > keep or inked * 2 >= len(rim):
            for y, x in cells:
                a[y, x] = 0
    return a


def snap_rows():
    """원본 시트 → {방향: [장면 0, 1, 2, 서기](칸 배열, 같은 줄은 같은 높이 · 같은 바닥선)}"""
    sheet = Image.open(os.path.join(RAW, 'dig_sheet_raw.png')).convert('RGB')
    rows = split((np.asarray(sheet.convert('L')) < 235).any(1), 3)
    tmps = [os.path.join(HERE, f'_dig_tmp{i}.png') for i in range(3)]
    out = {}
    try:
        for t, (y0, y1) in zip(tmps, rows):
            sheet.crop((0, max(y0 - 15, 0), sheet.width, min(y1 + 15, sheet.height))).save(t)
        period = float(np.median([snap_codex.snap(t, 12, square=True, min_hole=40)[1][0] for t in tmps]))
        for view, t in zip(VIEWS, tmps):
            cells = clear_white(snap_fixed(t, period, 0.01), keep=30)   # 머리 위 흰 하이라이트 한 획(30칸 안)은 남긴다
            out[view] = [cells[:, a:b] for a, b in split(cells[..., 3].any(0), 4)]
    finally:
        for t in tmps:
            if os.path.exists(t):
                os.remove(t)
    return out


def base(view):
    return np.asarray(Image.open(os.path.join(HERE, f'wombat_{view}_base.png')).convert('RGBA'))[::2, ::2]


def to_palette(a, pal):
    """칸 색을 모두 지금 웜뱃 팔레트로. 어두운 칸(max < 110)은 외곽선(회색이면 회색 코 · 외곽선 중 가까운 것),
    나머지는 회색 코를 뺀 가장 가까운 색. 귀 안쪽 분홍은 작은 덩어리(20칸 이하, 귀)만, 큰 덩어리(팔 · 그늘)는 다음 색"""
    a = a.copy()
    pal = sorted(pal)
    greys = [c for c in pal if max(c) - min(c) < 12 and c != INK and sum(c) < 400]
    warm = [c for c in pal if c not in greys]
    op = a[..., 3] > 0
    for c in {tuple(int(v) for v in x) for x in a[op][:, :3]}:
        cand = ([INK] + greys if max(c) - min(c) < 20 else [INK]) if max(c) < 110 else warm
        P = np.array(cand, float)
        order = [cand[i] for i in np.argsort(np.sqrt(((P - c) ** 2).sum(1)))]
        m = op & (a[..., :3] == c).all(-1)
        if order[0] == EAR_COLOR and len(order) > 1:
            for cells in blobs(m):
                for y, x in cells:
                    a[y, x, :3] = order[0] if len(cells) <= 20 else order[1]
            continue
        a[m, :3] = order[0]
    return a


# 던지기 장면 눈웃음 → 서 있을 때 눈(2026-10-05 사용자 「눈 웃음 없애봐」). 칸 좌표는 snap 결과(던지기 장면) 기준.
# 눈 크기 · 코와의 거리는 지금 웜뱃 숨쉬기 그림과 같다(앞 2×3칸 · 코 가운데에서 7.5칸, 옆 2×2칸 · 코 왼쪽 선에서 3~4칸, 눈 윗줄 = 코 윗선 한 줄 위)
OPEN_EYES = {   # 방향: (장면 크기, 털로 덮을 칸 [(줄, 시작 열, 끝 열)], 눈 칸 [(줄, 시작 열, 끝 열)])
    'front': ((49, 39), [(20, 10, 14), (21, 10, 14), (22, 10, 14), (21, 24, 27), (22, 24, 27)],
              [(20, 11, 12), (21, 11, 12), (22, 11, 12), (20, 26, 27), (21, 26, 27), (22, 26, 27)]),
    'side': ((48, 41), [(18, 27, 31), (19, 27, 31)], [(18, 31, 32), (19, 31, 32)]),
}


def open_eyes(view, a):
    size, clear, eyes = OPEN_EYES[view]
    assert a.shape[:2] == size, f'{view} 던지기 장면 칸 크기가 바뀌었다 {a.shape[:2]} — OPEN_EYES 좌표를 다시 잡는다'
    a = a.copy()
    for color, spans in ((FUR, clear), (INK, eyes)):
        for r, c0, c1 in spans:
            a[r, c0:c1 + 1, :3] = color
    return a


def close_edge(a):
    op = a[..., 3] > 0
    edge = op & ~(np.roll(op, 1, 0) & np.roll(op, -1, 0) & np.roll(op, 1, 1) & np.roll(op, -1, 1))
    a[edge, :3] = INK
    return a


def body_mask(a):
    best = max(blobs(a[..., 3] > 0, diag=True), key=len)
    m = np.zeros(a.shape[:2], bool)
    for y, x in best:
        m[y, x] = True
    return m


def foot_anchor(a, view, bottom=None):
    """(발끝 줄, 기준 열): 앞 · 뒤는 몸 아래 세 줄 가운데, 옆은 뒤꿈치(아래 세 줄 왼쪽 끝, 오른쪽을 본다).
    bottom을 주면 그 줄을 발끝으로(발 앞에 쏟아진 흙이 몸과 붙어 있어도 바닥선은 그대로)"""
    m = body_mask(a)
    if bottom is None:
        bottom = np.nonzero(m.any(1))[0].max()
    xs = np.nonzero(m[bottom - 2:bottom + 1].any(0))[0]
    return bottom, (xs.min() if view == 'side' else (xs.min() + xs.max()) / 2)


def floor_row(a, view):
    """바닥선: 발이 있는 열(앞 · 뒤는 몸 양쪽 바깥 35%, 옆은 뒤쪽 절반)에서 몸의 맨 아랫줄.
    가운데로 쏟아진 흙덩이(몸과 붙어 있어도)는 바닥선을 끌어내리지 않는다"""
    m = body_mask(a)
    xs = np.nonzero(m.any(0))[0]
    x0, x1 = xs.min(), xs.max() + 1
    w = x1 - x0
    cols = np.zeros(a.shape[1], bool)
    if view == 'side':
        cols[x0:x0 + w // 2] = True
    else:
        cols[x0:x0 + int(w * 0.35)] = True
        cols[x1 - int(w * 0.35):x1] = True
    return np.nonzero((m & cols).any(1))[0].max()


def build():
    """{방향: (숨쉬기 프레임, [장면 0 · 1 · 2], [장면별 (위, 왼) 자리])} — 자리는 숨쉬기 캔버스 기준(발끝 줄 · 피벗 열이 같다)"""
    poses = snap_rows()
    out = {}
    for view in VIEWS:
        pal = {tuple(int(v) for v in c) for c in base(view)[base(view)[..., 3] > 0][:, :3]}
        ps = [to_palette(p, pal) for p in poses[view][:3]]
        if view in OPEN_EYES:
            ps[2] = open_eyes(view, ps[2])
        idle = Sprite(os.path.join(HERE, f'wombat_{view}_base.png'), WOMBAT[view]).frame(**idle_frames()[0])
        fb, fx = foot_anchor(idle, view)
        spots = []
        for p in ps:
            b, x = foot_anchor(p, view, floor_row(p, view))
            spots.append((fb - b, int(round(fx - x))))
        out[view] = (idle, ps, spots)
    return out


def frames_in_cell(view, idle, ps, spots, cell):
    """칸 폭 cell, 칸 가운데 열 = 숨쉬기 캔버스의 피벗 열(W // 2, BakeryBaker.CellBottom · make_anim.sheet와 같다), 발끝 = 아래 끝"""
    H, W = idle.shape[:2]
    shift = cell // 2 - W // 2
    top = min(dy for dy, _ in spots)
    h = H - min(top, 0)
    cut = 0
    frames = []
    for p, (dy, dx) in zip(ps, spots):
        f = np.zeros((h, cell, 4), np.uint8)
        for y, x in zip(*np.nonzero(p[..., 3])):
            fy, fx = y + dy - min(top, 0), x + dx + shift
            if fy >= h:
                cut += 1          # 발끝 아래(앞으로 쏟아진 흙)
                continue
            assert 0 <= fx < cell, f'{view}: 칸 폭 {cell}칸을 넘는다(열 {fx})'
            f[fy, fx] = p[y, x]
        frames.append(close_edge(f))   # 잘린 흙 가장자리를 외곽선으로
    return frames, cut


def need_cell(view, idle, ps, spots):
    W = idle.shape[1]
    half = 0
    for p, (dy, dx) in zip(ps, spots):
        xs = np.nonzero(p[..., 3].any(0))[0] + dx
        half = max(half, W // 2 - xs.min(), xs.max() + 1 - W // 2)
    return 2 * half


def main():
    made = build()
    cell = max(need_cell(v, *made[v]) for v in VIEWS)
    cell += cell % 2
    for view in VIEWS:
        frames, cut = frames_in_cell(view, *made[view], cell)
        seq = [frames[i] for i in SEQ]
        out = np.concatenate(seq, axis=1)
        save(out, os.path.join(HERE, '..', f'wombat_{view}_dig.png'))
        print(f'wombat_{view}_dig.png', len(seq), 'frames', 'cell', cell * 2, 'px', 'cut below feet', cut)
    if len(sys.argv) > 1:
        preview(made, cell, sys.argv[1])


def preview(made, cell, folder):
    """서 있기 → 파기 → 서 있기, 앞 · 옆 · 뒤 나란히(한 칸 = 6px)"""
    os.makedirs(folder, exist_ok=True)
    cols = {}
    for view in VIEWS:
        idle, ps, spots = made[view]
        frames, _ = frames_in_cell(view, idle, ps, spots, cell)
        stand, _ = frames_in_cell(view, idle, [idle], [(0, 0)], cell)
        h = frames[0].shape[0]
        s = np.zeros_like(frames[0])
        s[h - stand[0].shape[0]:] = stand[0]
        cols[view] = [s] + frames
    h = max(c[0].shape[0] for c in cols.values())
    order = [-1] + [i for i in SEQ] + [-1]
    dur = [IDLE_DUR[0]] + [TICK] * len(SEQ) + [700]
    ims = []
    for i in order:
        canvas = Image.new('RGBA', (cell * 3, h), (138, 102, 78, 255))
        for k, view in enumerate(VIEWS):
            f = cols[view][0 if i < 0 else i + 1]
            canvas.alpha_composite(Image.fromarray(f, 'RGBA'), (k * cell, h - f.shape[0]))
        ims.append(canvas.resize((canvas.width * 6, canvas.height * 6), Image.NEAREST).convert('RGB'))
    ims[0].save(os.path.join(folder, 'wombat_dig.gif'), save_all=True, append_images=ims[1:], duration=dur, loop=0)


if __name__ == '__main__':
    main()
