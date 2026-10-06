# 낚시터 연출(2026-10-05 설계 46). 설치물은 말뚝에서 빠지고 미끼 노점 업그레이드가 되어(사용자) 쓰는 것은 물 소용돌이 하나:
#   ../whirl_0~3.png  소용돌이 업그레이드가 맨 앞 물고기를 되돌릴 때 그 물고기 자리에 생기는 물 소용돌이 네 칸
#                     (24×12칸 납작한 타원, 흰 물보라 팔 둘이 45°씩 돎 → 네 칸이면 반 바퀴로 이어짐, 팔 사이는 짙은 물). 기하 도형이라 칸 무늬로 그린다(한 칸 2px)
#   (등대 켜짐 · 대 끝 불빛 점 · 통발 · 도르래 회전 칸은 같은 날 만들었다가 설치물이 빠져 지웠다)
# 사용: python make_fx.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
LIGHT = (110, 182, 170)
SHINE = (204, 234, 222)


def save(a, name):
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1), 'RGBA').save(os.path.join(OUT, name + '.png'))
    print(name, a.shape[1], 'x', a.shape[0])


def whirl():
    # 물 위에서 보이게(2026-10-05 프로그래밍방 플레이 「옅은 청록이라 물 위에서 거의 안 보임」): 24×12칸, 팔은 흰 물보라 · 테는 밝은 물결,
    # 팔 사이는 짙은 물(반투명)로 대비. 바깥 테 한 겹은 밝은 물결 반투명
    W, H = 24, 12
    yy, xx = np.mgrid[0:H, 0:W]
    dx, dy = (xx - (W - 1) / 2) / (W / 2), (yy - (H - 1) / 2) / (H / 2)
    rr, th = np.hypot(dx, dy), np.arctan2(dy, dx)
    for k in range(4):
        a = np.zeros((H, W, 4), np.uint8)
        arm = np.mod(th + rr * 4.5 - k * np.pi / 4, np.pi) < 1.15          # 두 갈래 팔(굵게), 칸마다 45°
        inside = rr <= 0.92
        a[inside & ~arm] = (36, 96, 92, 190)                               # 팔 사이: 짙은 물
        a[inside & arm] = SHINE + (255,)                                   # 팔: 흰 물보라
        a[inside & arm & (rr > 0.7)] = LIGHT + (255,)                      # 팔 끝은 밝은 물결
        a[(rr > 0.92) & (rr <= 1.05)] = LIGHT + (150,)                     # 바깥 테
        a[(rr < 0.16)] = (36, 96, 92, 230)                                 # 가운데 구멍
        save(a, 'whirl_%d' % k)


if __name__ == '__main__':
    whirl()
