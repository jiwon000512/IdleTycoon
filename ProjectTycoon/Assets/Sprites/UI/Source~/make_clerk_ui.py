# -*- coding: utf-8 -*-
# 점원 팝업 부품(2026-09-26, 시안 D1 그대로): raw/clerk_d1.png(Codex 시안)의 픽셀 격자를 재서 한 칸 = 1px로 줄인 뒤,
# 부품마다 칸 좌표로 잘라 ../clerk_*.png에 쓴다. 글자(폰트)와 웜뱃 그림은 빼고 잘라(9-slice 가운데는 글자 없는 줄·칸에서 뽑는다) 게임 폰트·웜뱃 정지 그림을 얹는다.
# 한 칸 = 캔버스 5px(PPU 20, ui_slices.json의 ppu). 사용: make_clerk_ui.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))

src = np.asarray(Image.open('raw/clerk_d1.png').convert('RGB')).astype(int)
H, W = src.shape[:2]


# ---------- 격자 재기: 색 경계 간격(FFT) + 위상(경계가 가장 많이 겹치는 자리) → 칸마다 중앙값 ----------
def period(axis):
    d = np.abs(np.diff(src, axis=axis)).sum(2)
    e = (d > 40).sum(axis=1 - axis).astype(float)
    e -= e.mean()
    f = np.abs(np.fft.rfft(e))
    fr = np.fft.rfftfreq(len(e))
    m = (fr > 1 / 8) & (fr < 1 / 3)
    return 1 / fr[m][np.argmax(f[m])]


def phase(axis, p):
    d = np.abs(np.diff(src, axis=axis)).sum(2)
    e = (d > 40).sum(axis=1 - axis).astype(float)
    best = None
    for off in np.arange(0, p, 0.1):
        idx = [int(round(x)) for x in np.arange(off, len(e), p)]
        score = sum(e[i] for i in idx if i < len(e))
        if best is None or score > best[0]:
            best = (score, off)
    return best[1]


p = (period(1) + period(0)) / 2
ox, oy = phase(1, p), phase(0, p)
xs = np.arange(ox + 1, W, p)
ys = np.arange(oy + 1, H, p)
m = np.zeros((len(ys), len(xs), 3), int)
for j, y in enumerate(ys):
    y0, y1 = int(round(y)), int(round(y + p))
    for i, x in enumerate(xs):
        x0, x1 = int(round(x)), int(round(x + p))
        cell = src[y0 + 1:max(y0 + 2, y1 - 1), x0 + 1:max(x0 + 2, x1 - 1)].reshape(-1, 3)
        m[j, i] = np.median(cell, axis=0)
print('grid %.3f cells %dx%d' % (p, m.shape[1], m.shape[0]))


# ---------- 도구 ----------
def quantize(a, th=28):
    # 비슷한 색(거리 th 안)을 한 색(평균)으로: 시안의 칸 노이즈를 지운다. 알파 0 칸은 그대로
    rgb = a[..., :3]; alpha = a[..., 3]
    flat = rgb[alpha > 0]
    centers = []
    for c in flat:
        for k in centers:
            if np.sqrt(((k[0] - c) ** 2).sum()) < th:
                k[1].append(c); break
        else:
            centers.append([c.astype(float), [c]])
    for k in centers:
        k[0] = np.mean(k[1], axis=0)
    out = a.copy()
    for idx in zip(*np.nonzero(alpha > 0)):
        c = rgb[idx]
        best = min(centers, key=lambda k: ((k[0] - c) ** 2).sum())
        out[idx][:3] = np.round(best[0])
    return out


def rgba(cells):
    a = np.zeros(cells.shape[:2] + (4,), np.uint8)
    a[..., :3] = cells; a[..., 3] = 255
    return a


def clear_outside(a):
    # 가장자리에서 이어진 밝은 칸(시안의 크림 바탕)을 투명으로: 둥근 모서리 밖 찌꺼기 제거. 외곽선이 막아 안쪽 크림은 남는다
    rgb = a[..., :3].astype(int)
    light = (rgb.sum(2) > 560) & (rgb.max(2) - rgb.min(2) < 60) & (a[..., 3] > 0)
    h, w = light.shape
    seen = np.zeros_like(light)
    q = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
    while q:
        y, x = q.pop()
        if 0 <= y < h and 0 <= x < w and light[y, x] and not seen[y, x]:
            seen[y, x] = True
            q += [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
    a = a.copy()
    a[seen] = 0
    return a


def clear_rows(a):
    # 아래가 열린 틀(배지가 덮는 초상 틀)용: 줄마다 첫 진한 칸 왼쪽·마지막 진한 칸 오른쪽만 투명으로
    a = a.copy()
    dark = (a[..., :3].astype(int).sum(2) < 200) & (a[..., 3] > 0)
    for y in range(a.shape[0]):
        xs = np.nonzero(dark[y])[0]
        if len(xs):
            a[y, :xs[0]] = 0
            a[y, xs[-1] + 1:] = 0
    return a


def save(name, a, border=None, clear=True):
    if clear == 'rows':
        a = clear_rows(a)
    elif clear:
        a = clear_outside(a)
    a = quantize(a)
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': list(border) if border else None, 'ppu': 20}
    print(f'{name:24s} {a.shape[1]:3d} x {a.shape[0]:3d}  border={border}')


def nine(cells, box, border, mid, mirror_left=False):
    # box=(x0,y0,x1,y1) 끝 제외, border=(l,b,r,t) Unity 순서. 모서리는 상자 네 귀, 변은 mid 열/줄에서 뽑고 가운데는 mid 칸 하나
    x0, y0, x1, y1 = box; l, b, r, t = border; cx, cy = mid
    X = list(range(x0, x0 + l)) + [cx] + list(range(x1 - r, x1))
    Y = list(range(y0, y0 + t)) + [cy] + list(range(y1 - b, y1))
    out = np.zeros((len(Y), len(X), 4), np.uint8)
    for j, sy in enumerate(Y):
        for i, sx in enumerate(X):
            out[j, i, :3] = cells[sy, sx][:3]
            out[j, i, 3] = cells[sy, sx][3] if cells.shape[2] == 4 else 255
    if mirror_left:
        out[:, :l] = out[:, len(X) - r:][:, ::-1][:, :l]
    return out


def crop(cells, box):
    x0, y0, x1, y1 = box
    return rgba(cells[y0:y1, x0:x1])


def erase(a, box, sample):
    # a 안의 상자(끝 제외)를 sample 상자 안 밝은 칸들의 중앙값으로 지운다(글자·그림 제거)
    x0, y0, x1, y1 = box
    sx0, sy0, sx1, sy1 = sample
    cells = a[sy0:sy1, sx0:sx1, :3].reshape(-1, 3)
    light = cells[cells.sum(1) > 560]
    a[y0:y1, x0:x1, :3] = np.median(light, axis=0)
    return a


# ---------- 부품(칸 좌표는 시안 D1 211×375 기준) ----------
# 패널 틀 192×282(x 9..201, y 43..325): 주황 띠 + 진갈색 선, 안은 크림
save('clerk_panel', nine(m, (9, 43, 201, 325), (6, 6, 6, 6), (105, 230)), border=(6, 6, 6, 6), clear=False)
# 닫기 17×17(X 그림 포함)
save('clerk_close', crop(m, (176, 51, 193, 68)))
# 가게 탭 51×16: 글자 없는 열(x 25)·줄(y 78)에서 변을 뽑는다
save('clerk_tab', nine(m, (16, 71, 67, 87), (7, 4, 7, 4), (25, 78)), border=(7, 4, 7, 4))
# 명찰 카드 178×49
save('clerk_card', nine(m, (16, 90, 194, 139), (5, 4, 5, 4), (100, 95)), border=(5, 4, 5, 4))
# 초상 틀 32×29(안은 웜뱃 정지 그림을 얹는다): 웜뱃 왼쪽 열(x 24)·위 줄(y 98)에서 변을 뽑는다
frame = nine(m, (21, 95, 53, 124), (5, 5, 5, 5), (24, 98))
frame[-5:] = frame[:5][::-1]   # 시안은 아래 변이 배지에 가려 열려 있다: 배지 없는 후보 줄에서도 닫힌 틀이 되게 위 변을 뒤집어 붙인다
save('clerk_frame', frame, border=(5, 5, 5, 5), clear='rows')
# 빈 자리 틀 32×29: 점선 그대로, 「+」만 지운다(글자는 폰트로)
empty = crop(m, (21, 148, 53, 177))
save('clerk_frame_empty', erase(empty, (8, 6, 25, 21), (8, 6, 25, 21)), clear='rows')
# 상태 배지 36×12: 바깥선(진갈색)과 채움(흰색, 색은 Image.color로 상태색)을 나눈다. 글자 칸(안쪽)은 채움으로 본다
bx0, by0, bx1, by1 = 20, 123, 56, 135
line = np.zeros((by1 - by0, bx1 - bx0, 4), np.uint8)
fill = np.zeros_like(line)
ink = m[127, 20]
for j in range(by1 - by0):
    for i in range(bx1 - bx0):
        c = m[by0 + j, bx0 + i]
        inside = 24 <= bx0 + i < 52 and 125 <= by0 + j < 133
        if c.sum() < 200:
            line[j, i] = (*ink, 255)
        elif inside or c.sum() < 560:
            fill[j, i] = (255, 255, 255, 255)
fill[line[..., 3] > 0] = (255, 255, 255, 255)   # 선 자리도 채워 두어 틈이 없게(선이 위에 겹친다)
save('clerk_badge_line', nine(line, (0, 0, 36, 12), (5, 5, 5, 5), (18, 6)), border=(5, 5, 5, 5), clear=False)
save('clerk_badge_fill', nine(fill, (0, 0, 36, 12), (5, 5, 5, 5), (18, 6)), border=(5, 5, 5, 5), clear=False)
print('badge green', tuple(m[128, 23]), 'gray', tuple(m[182, 23]))
# 자리 알약 33×12(x 87..119, y 98..109)
save('clerk_slot', nine(m, (87, 98, 120, 110), (4, 4, 4, 4), (90, 104)), border=(4, 4, 4, 4))
# 두 칸 표 91×22(x 59..150, y 113..135): 머리글 두 칸·값 두 칸 안을 각 칸 밝은 색 중앙값으로 지운다
table = crop(m, (59, 113, 150, 135))
for box in ((2, 1, 48, 10), (50, 1, 89, 10), (2, 11, 48, 21), (50, 11, 89, 21)):
    erase(table, box, box)
save('clerk_table', table, clear=False)
# 게이지 틀 30×7: 왼쪽 끝은 채움에 가려 있어 오른쪽 끝을 뒤집어 쓴다. 채움 19×5는 위 밝은 줄·아래 어두운 줄 포함
save('clerk_gauge', nine(m, (63, 126, 93, 133), (3, 3, 3, 3), (88, 129), mirror_left=True), border=(3, 3, 3, 3))
save('clerk_gauge_fill', nine(m, (64, 127, 83, 132), (2, 1, 0, 1), (75, 129)), border=(2, 1, 0, 1))
# 코인 7×7
save('clerk_coin', crop(m, (115, 126, 122, 133)))
# 버튼 36×17 + 눌림(위 밝은 줄 없음) + 비활성(회색)
btn = nine(m, (153, 100, 189, 117), (5, 5, 5, 5), (171, 108))
save('clerk_btn', btn, border=(5, 5, 5, 5))
pressed = btn.copy(); pressed[1, 1:-1] = pressed[2, 1:-1]
save('clerk_btn_pressed', pressed, border=(5, 5, 5, 5))
disabled = btn.copy()
g = (disabled[..., :3] * [0.3, 0.59, 0.11]).sum(2, keepdims=True)
disabled[..., :3] = np.clip(g * 0.85 + 30, 0, 255)
save('clerk_btn_disabled', disabled, border=(5, 5, 5, 5))

json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ui_slices.json', len(slices))
