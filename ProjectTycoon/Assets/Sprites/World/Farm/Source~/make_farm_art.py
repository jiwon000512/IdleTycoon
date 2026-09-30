# 설계 27 농장 그림(Codex 시안 → 칸 단위): 갈아 놓은 흙판 + 밀 3단계 × 두 줄.
# 원본 시안은 raw/(soil_a~c: 흙판 135×96칸, wheat_a~c: 밀 시트 3단계 가로 줄). 2026-09-30 에이전트가 고름: 흙판 = soil_b(가는 고랑 여섯 줄), 밀 = wheat_a(둥근 잎 · 통통한 이삭).
# 흙판: 시안이 이미 135×96 격자(soil_b는 8배)라 칸마다 최빈색 → 팔레트 7색 → 한 칸 2px(270×192, PPU 80, 피벗 아래 가운데). 바닥 재질이라 외곽선 없음.
# 밀: 시트의 흰 줄 사이 띠 셋(위부터 새싹 · 줄기 · 익음)을 찾고, 띠의 원본 격자(FFT, 8px쯤)를 재서 한 띠를 반으로 나눠(120칸쯤) 두 줄 그림으로 만든다.
#   밭 칸(135칸)에 줄 셋을 겹쳐 얹고(BakeryBaker k_CropOffsets), 줄마다 반쪽 0 · 1을 번갈아 써 같은 그림이 반복되지 않게 한다.
# 출력: ../plot.png · Resources/Sprites/Farm/wheat_<단계>_<반쪽>.png. 실행: Windows Python(Pillow · numpy) make_farm_art.py
import os
from collections import Counter
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
FARM = os.path.join(HERE, '..')
RES = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Farm')
PX = 2
SOIL_RAW, SOIL_CELLS = 'soil_b.png', (135, 96)
WHEAT_RAW = 'wheat_a.png'
# 밀 한 줄(반쪽)의 폭(칸): 밭 칸 135칸 안에 여유 있게. 원본 격자(FFT)는 6.4px로 재지만 눈으로 잰 픽셀은 8px쯤이라 폭을 직접 준다
WHEAT_CELLS_W = 120
# 팔레트: 이 비율보다 드문 색은 버리고, 이 거리 안은 한 색, 많아도 7색(art.md 5~7색)
PALETTE_TH = 0.008
PALETTE_GAP = 48
PALETTE_MAX = 7


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


def white_bg(a):
    # 네 변에서 이어진 흰색만 배경(안쪽 흰 하이라이트는 남긴다)
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


def soil():
    a = np.asarray(Image.open(os.path.join(RAW, SOIL_RAW)).convert('RGB')).astype(int)
    cw, ch = SOIL_CELLS
    small, sa = cells_of(a, np.ones(a.shape[:2], bool), cw, ch)
    rgb = quantize(small, sa)
    out = np.zeros((ch, cw, 4), np.uint8)
    out[..., :3] = rgb
    out[..., 3] = 255
    save(out, os.path.join(FARM, 'plot.png'))


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
            rgb = quantize(small, sa)
            out = np.zeros((cells_h, cells_w, 4), np.uint8)
            out[..., :3] = rgb
            out[..., 3] = np.where(sa, 255, 0)
            halves.append(out)
        # 두 반쪽의 폭·높이를 맞춘다(가운데 정렬, 밑변 정렬)
        w = max(h.shape[1] for h in halves)
        h = max(h.shape[0] for h in halves)
        for half, out in enumerate(halves):
            pad = np.zeros((h, w, 4), np.uint8)
            dx = (w - out.shape[1]) // 2
            pad[h - out.shape[0]:, dx:dx + out.shape[1]] = out
            print('stage', stage, 'half', half, 'fft', round(measured, 1), 'period', round(period, 1), 'cells', out.shape[1], 'x', out.shape[0])
            save(pad, os.path.join(RES, 'wheat_%d_%d.png' % (stage, half)))


if __name__ == '__main__':
    soil()
    wheat()
