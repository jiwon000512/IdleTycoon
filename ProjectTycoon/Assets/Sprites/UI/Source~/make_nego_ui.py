# -*- coding: utf-8 -*-
# 월급 협상 화면 부품(2026-09-27, 시안 C 「빵집 흥정」): raw/nego_c.png(Codex 시안)에서 웜뱃·말풍선을 지운 raw/nego_c_clean.png의
# 격자를 재서 한 칸 = 1px로 줄이고, 부품마다 칸 좌표로 잘라 ../nego_*.png에 쓴다. 한 칸 = 캔버스 5px(PPU 20).
# 격자 재기·색 정리는 make_clerk_ui.py와 같다(그 스크립트는 불러오면 부품을 다시 쓰므로 필요한 함수만 옮겼다).
# 사용: make_nego_ui.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
P = 4.454   # 시안 D1과 같은 격자(Codex가 D1 참조 크기 941×1672 그대로 그렸다). FFT(period)는 5.0을 잡아 결이 뭉개진다


def edges(src, axis):
    d = np.abs(np.diff(src, axis=axis)).sum(2)
    return (d > 40).sum(axis=1 - axis).astype(float)


def period(src, axis):   # 참고용: 새 시안의 격자를 잴 때
    e = edges(src, axis)
    e -= e.mean()
    f = np.abs(np.fft.rfft(e))
    fr = np.fft.rfftfreq(len(e))
    m = (fr > 1 / 8) & (fr < 1 / 3)
    return 1 / fr[m][np.argmax(f[m])]


def phase(src, axis, p):
    e = edges(src, axis)
    best = None
    for off in np.arange(0, p, 0.1):
        score = sum(e[i] for i in (int(round(x)) for x in np.arange(off, len(e), p)) if i < len(e))
        if best is None or score > best[0]:
            best = (score, off)
    return best[1]


class Grid:
    # 시안 한 장을 칸(중앙값 색)으로 줄인다. box(px) → 칸 RGBA
    def __init__(self, path, p=P):
        src = np.asarray(Image.open(path).convert('RGB')).astype(int)
        H, W = src.shape[:2]
        self.p = p
        self.ox, self.oy = phase(src, 1, p), phase(src, 0, p)
        xs = np.arange(self.ox + 1, W, p)
        ys = np.arange(self.oy + 1, H, p)
        self.m = np.zeros((len(ys), len(xs), 3), int)
        for j, y in enumerate(ys):
            y0, y1 = int(round(y)), int(round(y + p))
            for i, x in enumerate(xs):
                x0, x1 = int(round(x)), int(round(x + p))
                cell = src[y0 + 1:max(y0 + 2, y1 - 1), x0 + 1:max(x0 + 2, x1 - 1)].reshape(-1, 3)
                self.m[j, i] = np.median(cell, axis=0)
        print('%s grid %.3f cells %dx%d' % (path, p, self.m.shape[1], self.m.shape[0]))

    def crop(self, px0, py0, px1, py1):
        x0, y0 = int((px0 - self.ox - 1) // self.p), int((py0 - self.oy - 1) // self.p)
        x1, y1 = int((px1 - self.ox - 1) // self.p) + 1, int((py1 - self.oy - 1) // self.p) + 1
        a = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
        a[..., :3] = self.m[y0:y1, x0:x1]
        a[..., 3] = 255
        return a


clean = Grid('raw/nego_c_clean.png')   # 웜뱃·말풍선을 지운 판(무대)
mock = Grid('raw/nego_c.png')          # 시안 원본(리본)


def quantize(a, th=28):
    rgb = a[..., :3]; alpha = a[..., 3]
    centers = []
    for c in rgb[alpha > 0]:
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
        out[idx][:3] = np.round(min(centers, key=lambda k: ((k[0] - c) ** 2).sum())[0])
    return out


def clear_outside(a):
    # 가장자리에서 이어진 밝은 칸(시안의 크림 바탕)을 투명으로: 둥근 모서리 밖 찌꺼기 제거
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


def save(name, a, clear=True):
    a = quantize(clear_outside(a) if clear else a)
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': None, 'ppu': 20}
    print(f'{name:24s} {a.shape[1]:3d} x {a.shape[0]:3d}')


def framed(inner):
    # 안쪽 그림에 UI 공통 외곽선 1칸(#342020)을 두르고 네 귀를 1칸 둥글게 깎는다
    h, w = inner.shape[:2]
    out = np.zeros((h + 2, w + 2, 4), np.uint8)
    out[..., :3] = OUTLINE
    out[..., 3] = 255
    out[1:-1, 1:-1] = inner
    for y, x in ((0, 0), (0, -1), (-1, 0), (-1, -1)):
        out[y, x] = 0
    for y, x in ((1, 1), (1, -2), (-2, 1), (-2, -2)):
        out[y, x, :3] = OUTLINE
    return out


OUTLINE = (0x34, 0x20, 0x20)


def trim_band(a):
    # 가장자리에 남은 시안 틀의 선·크림 띠 줄·열을 깎는다(장면 색이 절반 넘게 나올 때까지)
    def band(line):
        rgb = line[:, :3].astype(int)
        return ((rgb.sum(1) > 660) | (rgb.sum(1) < 200)).mean() > 0.5
    while band(a[0]): a = a[1:]
    while band(a[-1]): a = a[:-1]
    while band(a[:, 0]): a = a[:, 1:]
    while band(a[:, -1]): a = a[:, :-1]
    return a
# 빵집 무대: 시안 틀(진갈색 선 + 크림 띠, 픽셀 x 73..867 · y 355..920)은 버리고 안쪽 장면(x 82..858 · y 364..916)만 잘라 공통 외곽선을 두른다
inner = clean.crop(82, 364, 858, 916)
inner = trim_band(inner)
save('nego_scene', framed(inner), clear=False)

# 말풍선(시안 C 꼬리 달린 말풍선을 칸 무늬로 다시 그림: 시안 칸은 잡티가 많다). # = 외곽선, o = 크림(시안 243,235,225), . = 투명
CREAM = (243, 235, 225)


def pattern(rows, palette=None):
    palette = palette or {'#': OUTLINE, 'o': CREAM}
    a = np.zeros((len(rows), len(rows[0]), 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                a[y, x, :3] = palette[ch]
                a[y, x, 3] = 255
    return a


def save_raw(name, a, border=None):
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': list(border) if border else None, 'ppu': 20}
    print(f'{name:24s} {a.shape[1]:3d} x {a.shape[0]:3d}  border={border}')


# 몸통 9-slice: 모서리 둥글기 2칸, 경계 3칸
save_raw('bubble', pattern([
    '..###..',
    '.#ooo#.',
    '#ooooo#',
    '#ooooo#',
    '#ooooo#',
    '.#ooo#.',
    '..###..',
]), border=(3, 3, 3, 3))
# 꼬리: 첫 줄이 몸통 아래 선 위에 겹쳐 선을 끊는다. 왼쪽 변은 곧고 오른쪽이 비스듬(시안 C 왼쪽 말풍선). 오른쪽 말풍선은 좌우 뒤집어 쓴다
save_raw('bubble_tail', pattern([
    '#ooooo#',
    '#oooo#.',
    '#ooo#..',
    '#oo#...',
    '#o#....',
    '##.....',
]))

def clear_bg(a):
    # 가장자리에서 이어진 옅은 무채색 칸(시안 크림 바탕과 그 번짐)을 투명으로 → 빈 줄·열을 깎는다
    rgb = a[..., :3].astype(int)
    bg = (rgb.max(2) - rgb.min(2) < 45) & (rgb.sum(2) > 450)
    h, w = bg.shape
    seen = np.zeros_like(bg)
    q = [(y, x) for y in range(h) for x in (0, w - 1)] + [(y, x) for x in range(w) for y in (0, h - 1)]
    while q:
        y, x = q.pop()
        if 0 <= y < h and 0 <= x < w and bg[y, x] and not seen[y, x]:
            seen[y, x] = True
            q += [(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)]
    a = a.copy()
    a[seen] = 0
    ys, xs = np.nonzero(a[..., 3])
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


ribbon = clear_bg(mock.crop(256, 958, 700, 1042))
# 리본 안내문(시안 C 그림 그대로, 글자만 지움): 띠 안쪽(2~13줄 · 5~91열)을 글자 없는 띠 칸의 중앙값 한 색으로. 양 끝 접힌 부분 5칸·위 2줄·아래 3줄은 늘어나지 않는다
band = ribbon[2:14, 5:92, :3].reshape(-1, 3).astype(int)
ribbon[2:14, 5:92, :3] = np.median(band[band.sum(1) > 400], axis=0)
if __name__ == '__main__' and os.environ.get('DUMP'):
    for y in range(ribbon.shape[0]):
        print('%2d ' % y + ''.join('.' if ribbon[y, x, 3] == 0 else ('#' if ribbon[y, x, :3].astype(int).sum() < 250 else ('t' if ribbon[y, x, :3].astype(int).sum() < 400 else 'o')) for x in range(ribbon.shape[1])))
    raise SystemExit

save_raw('nego_ribbon', quantize(ribbon), border=(5, 3, 5, 2))

# 타이밍 바(시안 C 나무 자): 색은 시안에서 뽑았다. h 밝은 줄 · p 판 · s 그늘 · n 못 · d 화살표 · l 화살표 밝은 면
WOOD = {'#': OUTLINE, 'h': (193, 133, 90), 'p': (172, 120, 82), 's': (101, 64, 47), 'n': (49, 32, 30),
        'd': (79, 50, 43), 'l': (140, 93, 69)}
# 판 9-slice: 가운데 칸(홈 안)만 늘어난다. 위 7칸(선·밝은 줄·판 4·홈 선), 아래 6칸(홈 선·판 3·그늘·선), 좌우 5칸. 게임에서 높이 24칸 = 120px, 홈 안 11칸
save_raw('nego_bar', pattern([
    '.#########.',
    '#hhhhhhhhh#',
    '#hnpppppns#',
    '#hppppppps#',
    '#hppppppps#',
    '#hppppppps#',
    '#hpp###pps#',
    '#hpp#p#pps#',
    '#hpp###pps#',
    '#hppppppps#',
    '#hppppppps#',
    '#hnpppppns#',
    '#sssssssss#',
    '.#########.',
], WOOD), border=(5, 6, 5, 7))
# 눈금: 20칸마다 세 칸 높이 한 줄(Tiled로 깐다)
save_raw('nego_bar_tick', pattern([
    '..........n.........',
    '..........n.........',
    '..........n.........',
], WOOD))
# 매달린 화살표: 끝(마지막 줄)이 홈 윗선에 닿는다
save_raw('nego_arrow', pattern([
    '...#####...',
    '...#ldd#...',
    '...#ldd#...',
    '...#ldd#...',
    '.###ldd###.',
    '.#ldddddd#.',
    '..#ldddd#..',
    '...#ldd#...',
    '....#d#....',
    '.....#.....',
], WOOD))

json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ui_slices.json', len(slices))
