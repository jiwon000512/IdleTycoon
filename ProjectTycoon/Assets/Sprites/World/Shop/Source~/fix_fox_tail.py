# -*- coding: utf-8 -*-
# 여우 뒷모습 꼬리(2026-09-23 2차): 생성물의 꼬리(옆에 붙은 덩어리)를 지우고, 허리 아래 가운데서 나와 오른쪽으로 휘어 올라가는 꼬리를
# 베지어 곡선 위 원반의 합으로 그려 몸 위에 얹는다. 색은 앞모습 꼬리와 같은 3색(주황·진한 주황 안쪽·크림 끝) + 외곽선 1칸.
import numpy as np, sys
from PIL import Image
OUT, MAIN, DARK, TIP = (48, 24, 24), (216, 120, 72), (192, 96, 48), (240, 228, 204)
def cells(p): return np.asarray(Image.open(p).convert('RGBA'))[::2, ::2].copy()
def save(g, p): Image.fromarray(np.repeat(np.repeat(g, 2, 0), 2, 1)).save(p)
g = cells('fox_back_unpadded.png')
g[20:41, 31:] = 0  # 생성물 꼬리 제거
PAD = 9; g = np.concatenate([np.zeros((g.shape[0], PAD, 4), np.uint8), g], axis=1)
H, W = g.shape[:2]
# 몸 오른쪽 외곽선이 꼬리를 지운 자리에서 끊겼으므로 몸을 닫는다: 행 20~40의 오른쪽 끝을 외곽선으로
for y in range(20, 41):
    xs = np.nonzero(g[y, :, 3])[0]
    if len(xs): g[y, xs.max()] = (*OUT, 255)
P0, P1, P2 = (27.0, 38.0), (45.0, 41.0), (43.5, 21.0)
fill = np.zeros((H, W), bool); tip = np.zeros((H, W), bool); inner = np.zeros((H, W), bool)
yy, xx = np.mgrid[0:H, 0:W]
for i in range(81):
    t = i / 80
    x = (1-t)**2*P0[0] + 2*(1-t)*t*P1[0] + t*t*P2[0]; y = (1-t)**2*P0[1] + 2*(1-t)*t*P1[1] + t*t*P2[1]
    r = 4.2 + 2.2*np.sin(np.pi*t)**1.2 - 0.8*t
    d = (xx - x)**2 + (yy - y)**2 <= r*r
    fill |= d
    if t > 0.66: tip |= d
    if t < 0.75: inner |= (xx - x)**2 + (yy - (y + 1.5))**2 <= (r - 2.6)**2  # 아래쪽 그늘 띠
pf = np.pad(fill, 1); ring = (pf[:-2,1:-1] | pf[2:,1:-1] | pf[1:-1,:-2] | pf[1:-1,2:]) & ~fill
g[ring] = (*OUT, 255); g[fill] = (*MAIN, 255); g[fill & inner & ~tip] = (*DARK, 255); g[tip] = (*TIP, 255)
# 꼬리와 몸이 만나는 곳: 꼬리 외곽선이 몸 안에서 끝나므로 그대로 두면 앞에 얹힌 것으로 읽힌다
save(g, 'fox_back.png'); print('fox_back', W, 'x', H)
