# 설계 27 농장 그림(Codex 시안 → 칸 단위): 밭 칸 흙판 + 밀 5단계 × 두 줄(+ 바람 흔들림 판) + 농사 타이머 · 다 익음 표시.
# 원본 시안은 raw/(soil_a~c: 흙판 135×96칸, wheat_a~c: 밀 시트 3단계 가로 줄). 2026-09-30 에이전트가 고름: 흙판 = soil_b(가는 고랑), 밀 = wheat_a(둥근 잎 · 통통한 이삭).
# 흙판(사용자 피드백 2026-09-30 「가장자리가 네모라 땅과 구분이 안 됨」 · 「각 칸에 심겨야」, 시안 A 선택): 칸(135×96) 둘레 여백(INSET, FarmConfigTable fieldInset 0.25유닛 = 10칸)은 투명(굴 바닥이 보인다),
#   그 안쪽에 테두리 2칸(외곽선 + 밝은 턱, 아래는 그늘)과 속 흙. 속 흙은 soil_b 팔레트로 직접 그린다: 바탕 + 잔돌, 같은 높이 이랑 셋(BEDS)을 가는 밝은 선 한 칸으로 나눈다.
#   작물 줄 밑변(ROW_CELLS)은 이랑 아래에서 6칸 위 = BakeryBaker k_CropOffsets와 같은 값(칸 밑변에서 18 · 42 · 66칸 = 0.45 · 1.05 · 1.65유닛).
# 밀: 시트의 흰 줄 사이 띠 셋(위부터 새싹 · 줄기 · 익음)을 찾고, 띠의 원본 격자(FFT, 8px쯤)를 재서 한 띠를 반으로 나눠(120칸) 두 줄 그림으로 만든 뒤
#   밭 속(111칸)에 들어가게 잉크가 적은 끝을 잘라 WHEAT_MAX_W칸으로. 새싹 · 줄기 단계는 세계 팔레트 쪽으로 채도 · 밝기를 누른다(PRESS, 사용자 피드백 6-2).
#   밭 칸에 줄 셋을 겹쳐 얹고(BakeryBaker k_CropOffsets), 줄마다 반쪽 0 · 1을 번갈아 써 같은 그림이 반복되지 않게 한다.
# 표시: 진행 게이지 「새싹 원판」 20×20칸 16장(farm_timer_00~15, 테두리가 초록으로 돌고 가운데 새싹이 자람)과 금빛 테두리 · 이삭의 다 익음 표시(farm_ready_mark). 작물 위에 뜨므로 그림은 모든 것 위(BakeryBaker k_PlotMarkOrder).
#   2026-09-30 「무럭무럭」: 띠 셋에서 5단계(씨앗 둔덕 · 새싹 · 줄기 · 익어 가는 · 익음)를 만들고, 단계 · 반쪽마다 위 절반을 한 칸 기운 흔들림 판(_l · _r).
# 출력: ../plot.png · ../farm_timer_XX.png · ../farm_ready_mark.png · ../farm_sparkle_0~1.png · Resources/Sprites/Farm/wheat_<단계>_<반쪽>[_l|_r].png. 실행: Windows Python(Pillow · numpy) make_farm_art.py
import colorsys
import math
import os
from collections import Counter
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
FARM = os.path.join(HERE, '..')
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Farm')
PX = 2
CELL_W, CELL_H = 135, 96
INSET = 10
RIM = 2
BEDS = 3
ROW_CELLS = (18, 42, 66)
WHEAT_RAW = 'wheat_a.png'
# 밀 한 줄(반쪽)의 폭(칸): 원본 격자(FFT)는 6.4px로 재지만 눈으로 잰 픽셀은 8px쯤이라 폭을 직접 준다. 밭 속 111칸에 여유 있게 끝을 잘라 WHEAT_MAX_W칸
WHEAT_CELLS_W = 120
WHEAT_MAX_W = 104
WHEAT_TRIM = 16
# 단계마다 (채도 배율, 밝기 배율). 익음(2)은 그대로
PRESS = {0: (0.72, 0.9), 1: (0.85, 0.95), 2: (1.0, 1.0)}
# 팔레트: 이 비율보다 드문 색은 버리고, 이 거리 안은 한 색, 많아도 7색(art.md 5~7색)
PALETTE_TH = 0.008
PALETTE_GAP = 48
PALETTE_MAX = 7

# 흙판 색(soil_b를 칸 단위로 줄였을 때의 팔레트, 2026-09-30) + 외곽선(art.md)
LINE = (52, 32, 32)
SOIL = (96, 60, 36)
SOIL_LIGHT = (144, 96, 60)
SOIL_DARK = (72, 36, 24)
SOIL_PALE = (192, 132, 96)
MANURE = (48, 28, 24)
# 씨앗 둔덕의 싹 · 익어 가는 이삭에 섞는 초록(밀 줄기 단계 팔레트 쪽)과 섞는 비율
SEED_GREEN = (144, 156, 96)
TURN_GREEN = (150, 162, 92)
TURN_MIX = 0.5
MANURE_LIGHT = (104, 72, 44)

# 표시 색: 초록은 밀 줄기 단계 팔레트, 이삭은 익음 단계 팔레트
GREEN = (120, 132, 36)
GREEN_LIGHT = (180, 192, 60)
EAR = {'#': (60, 24, 24), 'g': (228, 168, 60), 'G': (204, 144, 48), 'h': (240, 216, 144), 's': (156, 96, 36), 'o': SOIL_LIGHT, 'O': SOIL}
EAR_ROWS = ['...h...',
            '..ghg..',
            '..GgG..',
            '..gGg..',
            's.GgG.s',
            '.s.s.s.',
            '..sss..',
            '...s...',
            '.oOOOo.']
# 진행 게이지 원판(칸): 크기 · 가운데 그림 자리 · 색(원판은 오븐 타이머와 같은 크림)
DIAL = 20
DIAL_SPROUT_X, DIAL_SPROUT_Y = 6, 5
DIAL_FACE, DIAL_HI, DIAL_RING = (240, 228, 216), (251, 244, 230), (217, 200, 180)
SPROUT = {'g': GREEN, 'y': GREEN_LIGHT, 'Y': GREEN, 's': SOIL_LIGHT, 'S': SOIL}
SPROUT_ROWS = [
    ['.......', '.......', '.......', '.......', '.......', '.......', '.......', '..sss..', '.sSSSs.'],
    ['.......', '.......', '.......', '.......', '.......', '.g...g.', '..g.g..', '...g...', '.sSSSs.'],
    ['.......', '.......', '.......', 'g.....g', '.g...g.', '..g.g..', '...g...', '...g...', '.sSSSs.'],
    ['...y...', '..yYy..', '...y...', 'g..g..g', '.g.g.g.', '..ggg..', '...g...', '...g...', '.sSSSs.'],
]

SPARKLE = {'#': LINE, 'o': (240, 200, 96), '*': (255, 255, 255)}
SPARKLE_ROWS = [['..#..', '.#o#.', '#o*o#', '.#o#.', '..#..'],
                ['.....', '..o..', '.o*o.', '..o..', '.....']]


def save(rgba, path):
    out = np.repeat(np.repeat(rgba, PX, axis=0), PX, axis=1)
    if out.shape[1] % 2:
        out = np.concatenate([out, np.zeros((out.shape[0], 1, 4), np.uint8)], axis=1)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(out).save(path)
    print(os.path.relpath(path, HERE), out.shape[1], 'x', out.shape[0])


def quantize(small, mask):
    # 팔레트: 빈도순 탐욕 군집(거리 30) 뒤 가장 가까운 색으로(make_pixel.py와 같은 규칙)
    cols, counts = np.unique(small[mask], axis=0, return_counts=True)
    order = np.argsort(-counts)
    pal = []
    for i in order:
        if counts[i] < mask.sum() * PALETTE_TH:
            break
        c = cols[i].astype(int)
        if all(np.abs(c - p).sum() > PALETTE_GAP for p in pal):
            pal.append(c)
    pal = np.array(pal[:PALETTE_MAX])
    dist = ((small[:, :, None, :] - pal[None, None, :, :]) ** 2).sum(3)
    print('  palette', len(pal))
    return pal[dist.argmin(2)].astype(np.uint8)


def press(rgb, mask, sat, val):
    # 팔레트 색마다 채도 · 밝기를 누른다(어두운 외곽선은 그대로: 밝기 0.35 아래)
    out = rgb.copy()
    for c in np.unique(rgb[mask], axis=0):
        h, s, v = colorsys.rgb_to_hsv(*(c / 255.0))
        if v < 0.35:
            continue
        r, g, b = colorsys.hsv_to_rgb(h, s * sat, v * val)
        out[(rgb == c).all(2)] = (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))
    return out


def cells_of(rgb, mask, cells_w, cells_h):
    # 칸마다 불투명 과반이면 최빈색(색은 12 단위로 묶어 센다)
    h, w = mask.shape
    q = rgb // 12 * 12
    small = np.zeros((cells_h, cells_w, 3), int)
    sa = np.zeros((cells_h, cells_w), bool)
    for cy in range(cells_h):
        ya, yb = int(cy * h / cells_h), max(int((cy + 1) * h / cells_h), int(cy * h / cells_h) + 1)
        for cx in range(cells_w):
            xa, xb = int(cx * w / cells_w), max(int((cx + 1) * w / cells_w), int(cx * w / cells_w) + 1)
            m = mask[ya:yb, xa:xb]
            if m.mean() < 0.5:
                continue
            sa[cy, cx] = True
            small[cy, cx] = Counter(map(tuple, q[ya:yb, xa:xb][m])).most_common(1)[0][0]
    return small, sa


def grid_period(g):
    res = []
    for axis in (0, 1):
        d = np.abs(np.diff(g, axis=axis))
        e = (d > 25).sum(axis=1 - axis).astype(float)
        e -= e.mean()
        f = np.abs(np.fft.rfft(e))
        fr = np.fft.rfftfreq(len(e))
        m = (fr > 1 / 80) & (fr < 1 / 4)
        res.append(1 / fr[m][np.argmax(f[m])])
    return float(np.median(res))


def trim(out, max_w):
    # 양끝에서 잉크(불투명 칸)가 가장 적은 곳을 잘라 max_w칸 안으로(포기 사이 빈 칸을 고른다)
    ink = (out[..., 3] > 0).sum(0)
    w = out.shape[1]
    best = None
    for left in range(0, WHEAT_TRIM + 1):
        for right in range(0, WHEAT_TRIM + 1):
            if w - left - right > max_w:
                continue
            cost = (ink[:left].sum() + ink[w - right:].sum(), abs(left - right), -(w - left - right))
            if best is None or cost < best[0]:
                best = (cost, left, right)
    _, left, right = best
    return out[:, left:w - right]


def soil():
    # 밭 칸 흙판: 둘레 여백은 투명, 테두리 2칸, 속은 바탕 + 잔돌, 같은 높이 이랑 셋을 가는 밝은 선으로 나눈다
    out = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    x0, x1 = INSET, CELL_W - INSET
    y0, y1 = INSET, CELL_H - INSET
    rng = np.random.RandomState(27)
    body = np.zeros((y1 - y0, x1 - x0, 3), np.uint8)
    body[:] = SOIL
    speck = rng.rand(*body.shape[:2])
    body[speck < 0.05] = SOIL_LIGHT
    body[(speck >= 0.05) & (speck < 0.06)] = SOIL_PALE
    body[(speck >= 0.06) & (speck < 0.09)] = SOIL_DARK
    inner_bottom = body.shape[0] - 1 - RIM
    bed = (body.shape[0] - 2 * RIM) // BEDS
    for b in range(1, BEDS):
        body[inner_bottom - bed * b, :] = SOIL_LIGHT
    # 테두리: 바깥 외곽선, 안쪽 턱(위 · 좌 · 우 밝게, 아래 그늘)
    body[0, :] = LINE
    body[-1, :] = LINE
    body[:, 0] = LINE
    body[:, -1] = LINE
    body[1, 1:-1] = SOIL_PALE
    body[1:-1, 1] = SOIL_LIGHT
    body[1:-1, -2] = SOIL_LIGHT
    body[-2, 1:-1] = SOIL_DARK
    out[y0:y1, x0:x1, :3] = body
    out[y0:y1, x0:x1, 3] = 255
    save(out, os.path.join(FARM, 'plot.png'))


def white_bg(a):
    # 네 변에서 이어진 흰색만 배경(안쪽 밝은 선은 남긴다). 밀은 안 쓴다(흰 하이라이트가 없어 전부 배경)
    from collections import deque
    near = np.abs(a[..., :3].astype(int) - 255).sum(2) < 60
    h, w = near.shape
    bg = np.zeros((h, w), bool)
    q = deque()
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x] and not bg[y, x]:
                bg[y, x] = True
                q.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x] and not bg[y, x]:
                bg[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and near[ny, nx] and not bg[ny, nx]:
                bg[ny, nx] = True
                q.append((ny, nx))
    return bg


def wheat():
    img = np.asarray(Image.open(os.path.join(RAW, WHEAT_RAW)).convert('RGBA'))
    # 식물에는 흰 하이라이트가 없으니 흰색은 전부 배경(줄기 사이에 갇힌 흰 칸이 남지 않게)
    ink = (np.abs(img[..., :3].astype(int) - 255).sum(2) >= 60) & (img[..., 3] >= 128)
    rows = ink.any(1)
    # 흰 줄로 나뉜 띠(위부터 단계 0 · 1 · 2)
    bands, start = [], None
    for y, on in enumerate(rows):
        if on and start is None:
            start = y
        if not on and start is not None:
            if y - start > 20:
                bands.append((start, y))
            start = None
    if start is not None:
        bands.append((start, len(rows)))
    assert len(bands) == 3, bands
    raw = {}   # 원본 띠 단계 → 반쪽 두 장(칸 단위)
    for stage, (y0, y1) in enumerate(bands):
        band = ink[y0:y1]
        xs = np.nonzero(band.any(0))[0]
        x0, x1 = xs.min(), xs.max() + 1
        measured = grid_period(img[y0:y1, x0:x1, :3].astype(int).mean(2))
        mid = x0 + (x1 - x0) // 2
        halves = []
        for half, (hx0, hx1) in enumerate(((x0, mid), (mid, x1))):
            sub = img[y0:y1, hx0:hx1, :3].astype(int)
            mask = ink[y0:y1, hx0:hx1]
            cells_w = WHEAT_CELLS_W
            period = (hx1 - hx0) / cells_w
            cells_h = max(1, int(round((y1 - y0) / period)))
            small, sa = cells_of(sub, mask, cells_w, cells_h)
            rgb = press(quantize(small, sa), sa, *PRESS[stage])
            out = np.zeros((cells_h, cells_w, 4), np.uint8)
            out[..., :3] = rgb
            out[..., 3] = np.where(sa, 255, 0)
            halves.append(trim(out, WHEAT_MAX_W))
        # 두 반쪽의 폭·높이를 맞춘다(가운데 정렬, 밑변 정렬)
        w = max(h.shape[1] for h in halves)
        h = max(h.shape[0] for h in halves)
        for half, out in enumerate(halves):
            pad = np.zeros((h, w, 4), np.uint8)
            dx = (w - out.shape[1]) // 2
            pad[h - out.shape[0]:, dx:dx + out.shape[1]] = out
            print('band', stage, 'half', half, 'fft', round(measured, 1), 'period', round(period, 1), 'cells', out.shape[1], 'x', out.shape[0])
            raw.setdefault(stage, []).append(pad)
    # 2026-09-30 사용자 선택 「무럭무럭」: 5단계 = 씨앗 둔덕 · 새싹(띠 0) · 줄기(띠 1) · 익어 가는(띠 2를 초록 반쯤) · 익음(띠 2).
    # 단계마다 바람 흔들림 판 두 장(위 절반을 한 칸 왼쪽 _l · 오른쪽 _r, 밑동은 그대로)
    for half in range(2):
        stages = [seed_stage(raw[0][half]), raw[0][half], raw[1][half], turning_stage(raw[2][half]), raw[2][half]]
        for stage, cells in enumerate(stages):
            save(cells, os.path.join(RES, 'wheat_%d_%d.png' % (stage, half)))
            save(tilt(cells, -1), os.path.join(RES, 'wheat_%d_%d_l.png' % (stage, half)))
            save(tilt(cells, 1), os.path.join(RES, 'wheat_%d_%d_r.png' % (stage, half)))


def seed_stage(sprout):
    # 새싹 그림의 맨 아랫줄 줄기 자리마다 흙 둔덕(7칸) + 초록 싹 두 칸. 높이 6칸, 폭은 새싹과 같다
    w = sprout.shape[1]
    bottom = sprout[-1, :, 3] > 0
    centers, x = [], 0
    while x < w:
        if bottom[x]:
            start = x
            while x < w and bottom[x]:
                x += 1
            centers.append((start + x - 1) // 2)
        x += 1
    out = np.zeros((6, w, 4), np.uint8)
    rows = ['...#...',
            '..#g#..',
            '...g...',
            '.#lll#.',
            '#sssss#',
            '#######']
    pal = {'#': LINE, 'g': SEED_GREEN, 'l': SOIL_PALE, 's': SOIL_LIGHT}
    for cx in centers:
        for y, row in enumerate(rows):
            for dx, ch in enumerate(row):
                x = cx - 3 + dx
                if ch in pal and 0 <= x < w and out[y, x, 3] == 0:
                    out[y, x] = pal[ch] + (255,)
    return out


def turning_stage(ripe):
    # 익어 가는: 익음 그림의 모양 그대로 외곽선 밖 색을 초록과 반쯤 섞는다(색 수는 익음과 같다)
    out = ripe.copy()
    rgb = out[..., :3].astype(int)
    body = (out[..., 3] > 0) & (rgb.sum(2) >= 200)
    mix = (rgb * (1 - TURN_MIX) + np.array(TURN_GREEN) * TURN_MIX).astype(np.uint8)
    out[..., :3] = np.where(body[..., None], mix, out[..., :3])
    return out


def tilt(cells, d):
    # 위 절반을 d칸 옆으로(바람). 밑동이 제자리라 한 칸 꺾여 보인다
    out = np.zeros_like(cells)
    cut = cells.shape[0] // 2
    out[cut:] = cells[cut:]
    if d > 0:
        out[:cut, d:] = cells[:cut, :-d]
    else:
        out[:cut, :d] = cells[:cut, -d:]
    return out


def marks():
    # 진행 게이지 「새싹 원판」(2026-09-30 사용자 선택 C, 오븐 타이머와 다른 농사 게이지): 20×20칸 원판. 테두리가 12시부터 시계 방향으로 초록이 되고,
    # 가운데 새싹이 씨앗 둔덕 → 떡잎 → 잎 → 이삭 맺힘으로 자란다(farm_timer_00~15). 다 익음 표시는 금빛 테두리 + 금빛 이삭(farm_ready_mark)
    size = DIAL
    c = (size - 1) / 2
    ys, xs = np.mgrid[0:size, 0:size]
    r = np.sqrt((xs - c) ** 2 + (ys - c) ** 2)
    angle = (np.degrees(np.arctan2(xs - c, c - ys)) + 360) % 360
    outline = (r <= 9.6) & (r > 8.6)
    ring = (r <= 8.6) & (r > 6.6)
    outer = r > 7.6            # 테두리 바깥 한 칸은 밝게
    face = r <= 6.6

    def dial(fill_deg, on, on_outer):
        a = np.zeros((size, size, 4), np.uint8)
        a[outline] = LINE + (255,)
        a[ring] = DIAL_RING + (255,)
        filled = ring & (angle <= fill_deg)
        a[filled & outer] = on_outer + (255,)
        a[filled & ~outer] = on + (255,)
        a[face] = DIAL_FACE + (255,)
        a[face & (ys < c - 3) & (xs < c)] = DIAL_HI + (255,)
        return a

    def stamp(a, rows, pal):
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in pal:
                    a[DIAL_SPROUT_Y + y, DIAL_SPROUT_X + x] = pal[ch] + (255,)

    frames = 16
    for i in range(frames):
        p = i / (frames - 1)
        frame = dial(p * 360 + 1e-6 if i > 0 else -1, GREEN, GREEN_LIGHT)
        stamp(frame, SPROUT_ROWS[min(len(SPROUT_ROWS) - 1, int(p * len(SPROUT_ROWS)))], SPROUT)
        save(frame, os.path.join(FARM, 'farm_timer_%02d.png' % i))
    ready = dial(360, EAR['G'], EAR['g'])
    stamp(ready, EAR_ROWS, EAR)
    save(ready, os.path.join(FARM, 'farm_ready_mark.png'))
    # 2026-09-30 「무럭무럭」: 다 익은 이삭 위에 가끔 뜨는 반짝임 두 장(큰 +, 작은 +). 가운데 피벗
    for i, rows in enumerate(SPARKLE_ROWS):
        spark = np.zeros((len(rows), len(rows[0]), 4), np.uint8)
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in SPARKLE:
                    spark[y, x] = SPARKLE[ch] + (255,)
        save(spark, os.path.join(FARM, 'farm_sparkle_%d.png' % i))


def manure():
    # 설계 28 거름 준 밭의 알갱이(더미, 시안 전): 흙판 속에 짙은 거름 덩이를 흩뿌린 겹 그림(흙판과 같은 크기 · 같은 밑변). 이랑 선과 테두리는 비운다
    out = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    rng = np.random.RandomState(28)
    top, bottom = INSET + RIM + 1, CELL_H - INSET - RIM - 2
    left, right = INSET + RIM + 1, CELL_W - INSET - RIM - 3
    bed = (CELL_H - 2 * (INSET + RIM)) // BEDS
    lines = [CELL_H - INSET - RIM - 1 - bed * b for b in range(1, BEDS)]
    # 멀리서도 거름 밭이 보이게 덩이는 3×2칸, 촘촘히(처음 2×2 · 70개는 캡처에서 안 보였다)
    for _ in range(150):
        y, x = rng.randint(top, bottom), rng.randint(left, right - 1)
        if any(abs(y - line) <= 1 or abs(y + 1 - line) <= 1 for line in lines):
            continue
        out[y, x:x + 3] = MANURE + (255,)
        out[y + 1, x:x + 3] = MANURE + (255,)
        out[y, x + 1] = MANURE_LIGHT + (255,)
    save(out, os.path.join(FARM, 'plot_manure.png'))


if __name__ == '__main__':
    soil()
    manure()
    wheat()
    marks()
