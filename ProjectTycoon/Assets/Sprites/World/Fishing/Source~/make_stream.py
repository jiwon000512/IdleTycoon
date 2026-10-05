# 낚시터 물길 「흙 도랑」(2026-10-05 사용자 선택 A, 컨셉 raw/stream_concept_a.png · 프롬프트 raw/prompt_stream.txt).
# 물길 끝 바닥 구멍은 설계 45(막다른 끝)로 지웠다.
# 물길은 굴 그리기(BurrowPainter)처럼 칸마다 칠한다: 물길 꺾은선이 어떻게 바뀌어도 곧은 · 꺾임 조각을 따로 두지 않는다.
#   ../water_tile_0~3.png  32×32(한 텍셀 = 한 칸) 이음새 없는 물 4칸: 청록 바탕 · 짙은 얼룩 · 밝은 물결 · 반짝 점. 굴 원점 기준 칸 좌표로 샘플.
#                      물결 획이 한 칸씩 오르내리고 반짝 점이 번갈아 켜진다(방향 없음, 옛 한 장짜리 water_tile.png = 0번)
#   ../stream_face.png 64×F(14) 먼 둑 흙 면: 굴 윗벽 띠 wall_face의 지층(14~27줄)을 조금 그늘지게 + 맨 위 선 · 맨 아래 젖은 선.
#                      위가 물 밖인 물 칸의 위 F줄에 깔고(가로로 이어 붙음), 그 아래 물 첫 줄은 밝은 거품 줄 LIGHT
#   둑 바깥 고리: 물 밖 거리 1 = 선 LINE, 2~3 = 턱 LIP, 4 = 선(방 둘레 턱과 같은 색). 물 안 옆 벽(좌우가 물 밖) 2칸은 DEEP
# 사용: python make_stream.py [미리보기 폴더]   (미리보기: FishingConfigTable main 행의 방 · 물길 · 말뚝 그대로 칠한 모형)
import os, sys, json, random
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
A = r'C:\project\Tycoon\ProjectTycoon\Assets'
W = os.path.join(A, 'Sprites', 'World')
PPU = 40
WATER = (60, 138, 126)
DEEP = (48, 120, 114)
LIGHT = (110, 182, 170)
SHINE = (204, 234, 222)
WET = (40, 92, 86)
LINE = (52, 32, 32)
LIP = (168, 120, 96)
F = 14


def tex(p):
    return np.asarray(Image.open(p).convert('RGBA'))


def water_tile(seed=3, frame=0):
    # frame 0~3(2026-10-05 품질 루프 「물이 고여 있네?」): 밝은 물결 획이 한 칸씩 오르내리고(0 → 위 → 0 → 아래) 반짝 점은 번갈아 켜진다.
    #   흐르는 방향이 없어 S자 물길 어느 구간에도 맞는다. frame 0 = 옛 한 장짜리 물 그림과 같다
    rnd = random.Random(seed)
    bob = (0, -1, 0, 1)[frame]
    t = np.zeros((32, 32, 4), np.uint8)
    t[..., :3] = WATER
    t[..., 3] = 255
    # 짙은 얼룩: 굵은 가로 덩어리 몇(이음새 없이 감아 돈다)
    for _ in range(3):
        y, x, w, h = rnd.randrange(32), rnd.randrange(32), rnd.randrange(9, 14), rnd.randrange(3, 5)
        for dy in range(h):
            for dx in range(w - abs(dy - h // 2) * 2):
                t[(y + dy) % 32, (x + dx + abs(dy - h // 2)) % 32, :3] = DEEP
    # 밝은 물결: 짧은 가로 획(가운데가 한 칸 위로 솟은 「︵」). 획마다 오르내림을 엇갈려(홀짝) 한꺼번에 움직이지 않게
    for i in range(3):
        y, x, w = rnd.randrange(32), rnd.randrange(32), rnd.randrange(7, 11)
        b = bob if i % 2 == 0 else -bob
        for dx in range(w):
            lift = 1 if 0 < dx < w - 1 else 0
            t[(y - lift + b) % 32, (x + dx) % 32, :3] = LIGHT
    for i in range(2):
        y, x = rnd.randrange(32), rnd.randrange(32)
        if frame in (0, 2) and i == 0 or frame in (1, 3) and i == 1 or frame == 0:
            t[y, x, :3] = SHINE
    return t


def stream_face():
    face = tex(os.path.join(W, 'Shop', 'wall_face.png'))
    rows = face[14:14 + F].astype(float)     # 지층 · 뿌리가 있는 줄
    rows[..., :3] *= 0.86                     # 도랑 안은 조금 그늘
    out = rows.astype(np.uint8)
    out[0, :, :3] = LINE                      # 먼 둑 턱 아래 선
    out[-1, :, :3] = WET                      # 물에 닿는 젖은 선
    return out


def mock(cfg, water, face):
    # 방: 가로 cols × cellWidth, 세로 entranceHeight + (rows − 1) × cellHeight. 위 = 윗벽 띠 40칸 + 턱 6 + 흙벽
    cw, ch, eh = 3.375, 2.4, 2.0
    width = int(cfg['cols'] * cw * PPU)
    height = int((eh + (cfg['rows'] - 1) * ch) * PPU)
    top = 24 + 6 + 40
    H, Wd = top + height + 40, width + 80
    ox, oy = 40 + width // 2, top            # 굴 원점(유닛 0,0) = 방 위 가운데
    floor, wall, wface = tex(os.path.join(W, 'Shop', 'floor_tile.png')), tex(os.path.join(W, 'Shop', 'wall_tile.png')), tex(os.path.join(W, 'Shop', 'wall_face.png'))
    img = np.zeros((H, Wd, 4), np.uint8)
    room = np.zeros((H, Wd), bool)
    room[top - 40:top + height, 40:40 + width] = True
    for y in range(H):
        for x in range(Wd):
            if room[y, x]:
                img[y, x] = wface[y - (top - 40), (x - ox) % wface.shape[1]] if y < top else floor[(y - oy) % 32, (x - ox) % 32]
            else:
                img[y, x] = wall[(y - oy) % 32, (x - ox) % 32]
    # 방 둘레 턱(굴과 같은 1·6 선, 2~5 턱)
    dist = np.zeros((H, Wd), int)
    cur = room.copy()
    for k in range(1, 7):
        grow = cur.copy()
        grow[1:] |= cur[:-1]; grow[:-1] |= cur[1:]; grow[:, 1:] |= cur[:, :-1]; grow[:, :-1] |= cur[:, 1:]
        ring = grow & ~cur
        dist[ring] = k
        cur = grow
    for k in range(1, 7):
        img[dist == k, :3] = LINE if k in (1, 6) else LIP
    # 물길 마스크: 선분마다 폭만큼 사각형(끝은 폭 절반만큼 늘여 꺾임이 맞물리게)
    pts = [(ox + p['x'] * PPU, oy - p['y'] * PPU) for p in cfg['stream']]
    hw = cfg['streamWidth'] * PPU / 2
    wm = np.zeros((H, Wd), bool)
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        xa, xb = sorted((x0, x1)); ya, yb = sorted((y0, y1))
        wm[int(round(ya - hw)):int(round(yb + hw)), int(round(xa - hw)):int(round(xb + hw))] = True
    wm &= room
    wm[:top] = False
    # 물: 타일, 먼 둑(위가 물 밖) 아래 F줄은 흙 면, 옆 벽 그늘 2칸
    up = np.zeros((H, Wd), int)
    for y in range(H):
        up[y] = np.where(wm[y], (up[y - 1] + 1) if y else 1, 0)
    left = np.zeros((H, Wd), int); right = np.zeros((H, Wd), int)
    for x in range(Wd):
        left[:, x] = np.where(wm[:, x], (left[:, x - 1] + 1) if x else 1, 0)
    for x in range(Wd - 1, -1, -1):
        right[:, x] = np.where(wm[:, x], (right[:, x + 1] + 1) if x < Wd - 1 else 1, 0)
    for y, x in zip(*np.nonzero(wm)):
        if up[y, x] <= F:
            img[y, x] = face[up[y, x] - 1, (x - ox) % face.shape[1]]
        elif up[y, x] == F + 1:
            img[y, x, :3] = LIGHT                 # 흙 면 아래 물가 거품 줄
            img[y, x, 3] = 255
        else:
            img[y, x] = water[(y - oy) % 32, (x - ox) % 32]
            if min(left[y, x], right[y, x]) <= 2:
                img[y, x, :3] = DEEP
    # 둑 바깥 고리: 1 = 선, 2~3 = 턱, 4 = 선
    d2 = np.zeros((H, Wd), int)
    cur = wm.copy()
    for k in range(1, 5):
        grow = cur.copy()
        grow[1:] |= cur[:-1]; grow[:-1] |= cur[1:]; grow[:, 1:] |= cur[:, :-1]; grow[:, :-1] |= cur[:, 1:]
        ring = grow & ~cur & room
        d2[ring] = k
        cur = grow
    for k in range(1, 5):
        img[(d2 == k) & (np.arange(H)[:, None] >= top), :3] = LINE if k in (1, 4) else LIP
    return Image.fromarray(img, 'RGBA'), (ox, oy), pts


def paste(img, sp_path, cell_xy, scale=0.5, bottom=True):
    """월드 그림(칸 2px)을 칸 단위로 줄여 붙인다. cell_xy = 발끝(bottom) 또는 가운데"""
    sp = Image.open(sp_path).convert('RGBA')
    sp = sp.resize((sp.width // 2, sp.height // 2), Image.NEAREST)
    x, y = cell_xy
    img.alpha_composite(sp, (int(x - sp.width / 2), int(y - (sp.height if bottom else sp.height / 2))))


if __name__ == '__main__':
    water, face = water_tile(), stream_face()
    for k in range(4):   # 물결 움직임 4칸(게임이 물길을 네 장 칠해 0.35초마다 돌린다)
        Image.fromarray(water_tile(frame=k), 'RGBA').save(os.path.join(OUT, 'water_tile_%d.png' % k))
    Image.fromarray(face, 'RGBA').save(os.path.join(OUT, 'stream_face.png'))
    print('water_tile_0~3', water.shape[1], 'x', water.shape[0], '· stream_face', face.shape[1], 'x', face.shape[0])
    if len(sys.argv) > 1:
        cfg = json.load(open(os.path.join(A, 'Resources', 'Data', 'FishingConfigTable.json'), encoding='utf-8'))['rows'][0]
        img, (ox, oy), pts = mock(cfg, water, face)
        paste(img, os.path.join(W, 'Shop', 'wombat_front.png'), (ox + 1.5 * PPU, oy + 2.4 * PPU))
        os.makedirs(sys.argv[1], exist_ok=True)
        img.resize((img.width * 2, img.height * 2), Image.NEAREST).save(os.path.join(sys.argv[1], 'fishing_stream_mock.png'))
