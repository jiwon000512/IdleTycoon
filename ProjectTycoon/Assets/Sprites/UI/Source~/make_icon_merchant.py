# 설계 32 행상 너구리 얼굴 아이콘(2026-10-01 사용자 선택 A 「얼굴만」): 상단 「다음 행상」 알약(48px = 2배) · 유물 팝업 제목(72px = 3배).
#   24×24px(한 칸 1px, 축복 아이콘과 같은 칸). 색은 너구리 정지 그림(World/Shop/tanuki_front.png)의 것. 도형 + 바깥 1칸 외곽선(창고 아이콘과 같은 방식).
#   버린 시안: B 얼굴 + 노란 목수건 · C 돌 메달 안 얼굴.
# 출력 ../icon_merchant.png, 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_icon_merchant.py
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
N = 24
INK = (52, 32, 32)
FUR, FUR_D, FUR_L = (179, 138, 115), (152, 105, 84), (208, 170, 145)
MASK, EAR_IN = (98, 67, 59), (227, 152, 139)
CREAM, PINK = (240, 228, 210), (227, 152, 139)
YEL, YEL_D = (237, 192, 106), (208, 153, 99)
WHITE = (255, 255, 255)


def c(col):
    return col + (255,)


def outline(a):
    f = a[..., 3] > 0
    g = f.copy()
    g[1:] |= f[:-1]; g[:-1] |= f[1:]; g[:, 1:] |= f[:, :-1]; g[:, :-1] |= f[:, 1:]
    a[g & ~f] = INK + (255,)
    return a


def face(d, ox=0, oy=0, s=1.0):
    # 정면 너구리 얼굴: 둥근 머리 · 귀 둘(분홍 속) · 눈 탈 · 크림 주둥이 · 검은 코 · 분홍 볼
    def r(x0, y0, x1, y1):
        return [ox + round(x0 * s), oy + round(y0 * s), ox + round(x1 * s), oy + round(y1 * s)]
    d.ellipse(r(1, 1, 7, 7), fill=c(FUR_D)); d.ellipse(r(13, 1, 19, 7), fill=c(FUR_D))
    d.ellipse(r(3, 3, 5, 5), fill=c(EAR_IN)); d.ellipse(r(15, 3, 17, 5), fill=c(EAR_IN))
    d.ellipse(r(0, 3, 20, 19), fill=c(FUR))
    d.ellipse(r(1, 13, 19, 19), fill=c(FUR_D)); d.ellipse(r(1, 3, 19, 17), fill=c(FUR))
    d.line([r(5, 5, 7, 5)[0:2], r(5, 5, 7, 5)[2:4]], fill=c(FUR_L))           # 머리 위 하이라이트
    d.ellipse(r(2, 8, 9, 13), fill=c(MASK)); d.ellipse(r(11, 8, 18, 13), fill=c(MASK))   # 눈 탈
    d.ellipse(r(6, 11, 14, 18), fill=c(CREAM))                                  # 주둥이
    d.rectangle(r(5, 10, 5, 11), fill=c(INK)); d.rectangle(r(14, 10, 14, 11), fill=c(INK))   # 눈
    d.point([tuple(r(5, 10, 5, 10)[0:2]), tuple(r(14, 10, 14, 10)[0:2])], fill=c(WHITE))
    d.rectangle(r(9, 12, 11, 13), fill=c(INK))                                  # 코
    d.point([tuple(r(10, 14, 10, 14)[0:2])], fill=c(INK))
    d.point([tuple(r(9, 15, 9, 15)[0:2]), tuple(r(11, 15, 11, 15)[0:2])], fill=c(INK))   # 입
    d.rectangle(r(3, 14, 4, 14), fill=c(PINK)); d.rectangle(r(16, 14, 17, 14), fill=c(PINK))  # 볼


def plain():
    im = Image.new('RGBA', (N, N)); d = ImageDraw.Draw(im)
    face(d, 2, 2)
    return outline(np.asarray(im).copy())


if __name__ == '__main__':
    Image.fromarray(plain()).save(os.path.join(HERE, '..', 'icon_merchant.png'))
    print('icon_merchant')
