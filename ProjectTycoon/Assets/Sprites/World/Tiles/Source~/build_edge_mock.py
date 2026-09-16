# -*- coding: utf-8 -*-
# 1) 잔디 시트 → 화면 주기 텍스처(192×96) 복원  2) 캡처의 배경색 픽셀을 격자에 맞춘 잔디로 치환(안 A 합성)  3) 아티팩트 조립
import sys, base64
from PIL import Image
import numpy as np
S = sys.argv[1]
sheet = np.asarray(Image.open('C:/project/Tycoon/ProjectTycoon/Assets/Sprites/World/Tiles/grass.png').convert('RGBA'))
TW, TH = 192, 96
period = np.zeros((TH, TW, 4), np.uint8)
# 타일 (d, s_)의 픽셀 (x, y) ↔ 텍스처 u = (32d + x − 32) mod 192, v = (−(16s_ + 16 − y)) mod 96  (make_tiles.py와 같은 식)
for d in range(6):
    for k in range(3):
        s_ = (d % 2) + 2 * k; i = d * 3 + k
        tile = sheet[:, i * 64:(i + 1) * 64]
        for y in range(32):
            hw = (y + 1) * 2 if y < 16 else (32 - y) * 2
            for x in range(32 - hw, 32 + hw):
                u = (32 * d + x - 32) % TW; vv = (-(16 * s_ + 16 - y)) % TH
                period[vv, u] = tile[y, x]
assert (period[..., 3] > 0).all(), 'period incomplete'
Image.fromarray(period).save(S + '/edge/grass_period.png')
# 안 A 합성: 카메라 (−0.5, −2), size 6, 1080×1920 → 160 px/유닛 = 2.5 화면px/월드px
before = np.asarray(Image.open(S + '/edge/before.png').convert('RGB')).copy()
H, W = before.shape[:2]; camx, camy = -0.5, -2.0; ppu = 160.0
bg = np.array([102, 158, 77])
mask = (np.abs(before.astype(int) - bg).sum(2) <= 6)
py, px = np.nonzero(mask)
wx = (px + 0.5 - W / 2) / ppu * 64 + camx * 64          # 월드 px
wy = (H / 2 - (py + 0.5)) / ppu * 64 + camy * 64
u = np.floor(wx).astype(int) % TW
v = (15 - np.floor(wy).astype(int)) % TH                 # 텍스처 행 v ↔ y px [15−v, 16−v)
after = before.copy(); after[py, px] = period[v, u, :3]
Image.fromarray(after).save(S + '/edge/after.png')
print('replaced', int(mask.sum()), 'px')
def small(n):
    im = Image.open(S + '/edge/' + n + '.png'); im = im.resize((540, 960), Image.LANCZOS); im.save(S + '/edge/' + n + '_half.png')
    return 'data:image/png;base64,' + base64.b64encode(open(S + '/edge/' + n + '_half.png', 'rb').read()).decode()
h = open(S + '/edge/edge-plan.template.html', encoding='utf-8').read()
h = h.replace('__before__', small('before')).replace('__after__', small('after'))
open(S + '/edge/edge-plan.html', 'w', encoding='utf-8').write(h); print('html', len(h) // 1024, 'KB')
