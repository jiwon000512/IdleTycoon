# 설계 30 석상 축복 아이콘(2026-09-30 사용자 선택 B2 「알갱이 돌 메달」): 24×24칸(상단 알약 48px = 2배, 석상 팝업 카드 칸 96px = 4배).
# 석상과 같은 돌 색 메달: 바깥 외곽선(테 둘레 한 칸) · 돌 테(위 · 왼쪽 밝음 → 아래 · 오른쪽 어두움 세 톤) · 파인 면(위 안벽 그늘 · 아래 안벽 빛) · 판,
# 테에 한 톤 어두운 점무늬(고정 씨앗), 가운데에 상징(12×12칸 도형 + 외곽선). 상징 여섯: 손님(토끼 얼굴) · 황금(발바닥 금화) · 화덕(불꽃) ·
# 주판 · 새싹 · 반짝(민트 결정). 16칸 메달은 상징이 메달을 꽉 채워 돌이 안 보여서(사용자 「돌의 느낌이 잘 안 남」) 24칸으로 키웠다.
# 결과: Assets/Resources/Sprites/Blessings/<효과>.png (BlessingTable icon). 실행: Windows Python(Pillow · numpy) make_blessing_icons.py
import os
import random
import numpy as np
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Resources', 'Sprites', 'Blessings')
INK = (52, 32, 32)
STONE = [(88, 80, 78), (122, 114, 108), (156, 148, 138), (190, 182, 170), (222, 216, 204)]   # 석상(Plaza/Source~/make_statue.py)과 같은 돌 색
ORDER = ['visitors', 'price', 'bake', 'checkout', 'grow', 'bonus']
N = 24
SPECKLE_SEED, SPECKLES = 7, 26


def rgba(c):
    return c + (255,)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = rgba(INK)
    return a


def symbol(name):
    # 12×12, 도형은 1~10칸 안(바깥 1칸은 외곽선 자리)
    im = Image.new('RGBA', (12, 12))
    d = ImageDraw.Draw(im)
    if name == 'visitors':        # 손님: 토끼 얼굴(긴 귀)
        W, P = (240, 232, 224), (232, 160, 168)
        d.rectangle([3, 1, 4, 5], fill=rgba(W)); d.rectangle([7, 1, 8, 5], fill=rgba(W))
        d.point((3, 2), fill=rgba(P)); d.point((3, 3), fill=rgba(P)); d.point((8, 2), fill=rgba(P)); d.point((8, 3), fill=rgba(P))
        d.ellipse([1, 4, 10, 10], fill=rgba(W))
        d.point((4, 7), fill=rgba(INK)); d.point((7, 7), fill=rgba(INK)); d.rectangle([5, 8, 6, 8], fill=rgba(P))
    elif name == 'price':         # 황금: 발바닥 금화
        G, L, D = (232, 176, 72), (248, 216, 120), (160, 100, 40)
        d.ellipse([1, 1, 10, 10], fill=rgba(G)); d.arc([1, 1, 10, 10], 160, 300, fill=rgba(L))
        d.rectangle([4, 6, 7, 8], fill=rgba(D)); d.point((4, 8), fill=rgba(G)); d.point((7, 8), fill=rgba(G))
        for x, y in ((3, 4), (5, 3), (6, 3), (8, 4)):
            d.point((x, y), fill=rgba(D))
    elif name == 'bake':          # 화덕: 불꽃
        O, Y, R = (240, 132, 56), (248, 200, 80), (184, 64, 40)
        d.polygon([(5, 1), (9, 5), (9, 8), (7, 10), (4, 10), (2, 8), (2, 5), (4, 4)], fill=rgba(O))
        d.polygon([(5, 5), (7, 7), (7, 9), (4, 9), (4, 7)], fill=rgba(Y))
        d.point((8, 9), fill=rgba(R)); d.point((2, 7), fill=rgba(R))
    elif name == 'checkout':      # 주판: 밝은 나무 틀 + 막대 셋 + 알
        WL, WD, IN, B1, B2 = (204, 144, 96), (160, 104, 68), (88, 56, 40), (240, 132, 56), (251, 244, 230)
        d.rectangle([1, 1, 10, 10], fill=rgba(WL)); d.rectangle([1, 9, 10, 10], fill=rgba(WD)); d.rectangle([2, 2, 9, 8], fill=rgba(IN))
        for y in (3, 5, 7):
            d.line([(2, y), (9, y)], fill=rgba(WD))
        for x, y, c in ((2, 3, B1), (3, 3, B1), (8, 3, B2), (2, 5, B2), (6, 5, B1), (7, 5, B1), (4, 7, B2), (5, 7, B2), (9, 7, B1)):
            d.point((x, y), fill=rgba(c))
    elif name == 'grow':          # 새싹: 두 잎 + 흙
        LF, LD, SO = (156, 196, 96), (108, 148, 64), (144, 96, 60)
        d.pieslice([2, 8, 9, 13], 180, 360, fill=rgba(SO))
        d.rectangle([5, 4, 6, 9], fill=rgba(LD))
        d.polygon([(5, 5), (1, 3), (1, 1), (4, 2)], fill=rgba(LF)); d.polygon([(6, 5), (10, 3), (10, 1), (7, 2)], fill=rgba(LF))
    elif name == 'bonus':         # 반짝: 민트 결정 + 반짝임
        M, ML, MD = (132, 216, 180), (204, 240, 216), (60, 156, 132)
        d.polygon([(5, 2), (9, 6), (5, 10), (1, 6)], fill=rgba(M)); d.polygon([(5, 2), (5, 10), (1, 6)], fill=rgba(ML)); d.polygon([(5, 6), (9, 6), (5, 10)], fill=rgba(MD))
        for x, y in ((9, 0), (9, 2), (8, 1), (10, 1)):
            d.point((x, y), fill=rgba((248, 216, 120)))
        d.point((9, 1), fill=rgba((255, 255, 255)))
    return np.asarray(im).copy()


yy, xx = np.mgrid[0:N, 0:N]
c = (N - 1) / 2
r = np.hypot(xx - c, yy - c)
light = (xx - c) + (yy - c)          # 음수 = 위 · 왼쪽(빛)
RIM, FACE = r <= 10.8, r <= 7.9
WALL = FACE & (r > 6.9)               # 파인 면의 안벽


def medal():
    out = np.zeros((N, N, 4), np.uint8)
    ring = RIM.copy()                 # 외곽선 = 테 바로 바깥 한 칸(4방향), 끊기지 않게
    ring[1:] |= RIM[:-1]; ring[:-1] |= RIM[1:]; ring[:, 1:] |= RIM[:, :-1]; ring[:, :-1] |= RIM[:, 1:]
    out[ring & ~RIM] = rgba(INK)
    rim = RIM & ~FACE
    out[rim & (light < -5)] = rgba(STONE[4])
    out[rim & (light >= -5) & (light <= 5)] = rgba(STONE[3])
    out[rim & (light > 5)] = rgba(STONE[1])
    out[FACE] = rgba(STONE[2])
    out[WALL & (light < 0)] = rgba(STONE[1])
    out[WALL & (light >= 0)] = rgba(STONE[3])
    # 점무늬: 돌 칸 가운데 몇 칸을 한 톤 어둡게(고정 씨앗이라 늘 같은 자리)
    rnd = random.Random(SPECKLE_SEED)
    tones = [tuple(s) for s in STONE]
    cells = [(y, x) for y in range(N) for x in range(N) if RIM[y, x] and out[y, x, 3] and tuple(out[y, x, :3]) in tones[1:5]]
    for y, x in rnd.sample(cells, SPECKLES):
        i = tones.index(tuple(out[y, x, :3]))
        out[y, x, :3] = STONE[max(0, i - 1)]
    return out


def icon(name):
    out = medal()
    s = outline(symbol(name))
    o = (N - 12) // 2
    sub = out[o:o + 12, o:o + 12]
    m = s[..., 3] > 0
    sub[m] = s[m]
    return out


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name in ORDER:
        Image.fromarray(icon(name)).save(os.path.join(OUT, name + '.png'))
    print('blessings', len(ORDER), N, 'x', N)
