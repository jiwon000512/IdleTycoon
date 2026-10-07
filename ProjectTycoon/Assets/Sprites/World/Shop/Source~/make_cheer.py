# -*- coding: utf-8 -*-
# 웜뱃 「만세」 앞모습 시트(2026-10-07 아트방, 설계 52 손맛 낚시: 낚은 물고기를 머리 위로 올리는 연출).
#   굴 파기 시트 ../wombat_front_dig.png(make_dig.py 결과)의 던지기 칸(SEQ 9번째, 두 앞발을 머리 위로 든 자세)에서 흙 부스러기(몸과 떨어진 덩어리)만 뺀 것.
#   2칸: [0] 팔 든 채 서기 · [1] 살짝 뜀(몸 1칸 위). 칸 폭 120px · 높이 · 발끝 기준선 · 색 · 외곽선은 dig와 같다(BakeryBaker가 VisitorSheetImporter.Import(경로, true, 120)로 자른다).
# 출력: ../wombat_front_cheer.png. 든 두 앞발 사이(물고기를 올릴 자리)의 높이를 발끝 기준 칸 · 유닛(1칸 = 1/40)으로 찍는다.
# 사용: python make_cheer.py [미리보기 폴더]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from anim_parts import load, save  # noqa: E402
from make_dig import blobs, SEQ  # noqa: E402

CELL = 60      # 칸 폭 120px = 60칸
THROW = SEQ.index(2)


def main():
    sheet = load(os.path.join(HERE, '..', 'wombat_front_dig.png'))
    f = sheet[:, THROW * CELL:(THROW + 1) * CELL].copy()
    body = max(blobs(f[..., 3] > 0), key=len)            # 4방향: 앞발에 모서리로만 붙은 부스러기 둘이 떨어진다
    keep = np.zeros(f.shape[:2], bool)
    for y, x in body:
        keep[y, x] = True
    f[~keep] = 0
    f = np.concatenate([np.zeros_like(f[:1]), f])          # 위에 한 줄 더(뜀 칸 자리) → 높이 50칸 = 100px
    hop = np.zeros_like(f)
    hop[:-1] = f[1:]
    out = np.concatenate([f, hop], axis=1)
    save(out, os.path.join(HERE, '..', 'wombat_front_cheer.png'))
    H = f.shape[0]
    m = f[..., 3] > 0
    foot = np.nonzero(m.any(1))[0].max()
    paws = []
    for half in (m[:, :CELL // 2], m[:, CELL // 2:]):
        top = np.nonzero(half.any(1))[0].min()
        xs = np.nonzero(half[top:top + 4].any(0))[0]
        paws.append((foot - top, (xs.min() + xs.max()) / 2))
    (lh, lx), (rh, rx) = paws
    rx += CELL // 2
    print('wombat_front_cheer.png 2칸 · 칸 %dpx · 높이 %dpx' % (CELL * 2, H * 2))
    print('든 앞발 꼭대기: 왼쪽 발끝에서 %d칸(%.3f유닛) · 오른쪽 %d칸(%.3f유닛) · 두 앞발 사이 = 높이 %.1f칸(%.3f유닛), x 칸 가운데에서 %+.1f칸(%.3f유닛)'
          % (lh, lh / 40, rh, rh / 40, (lh + rh) / 2, (lh + rh) / 80, (lx + rx) / 2 + 0.5 - CELL / 2, ((lx + rx) / 2 + 0.5 - CELL / 2) / 40))
    if len(sys.argv) > 1:
        os.makedirs(sys.argv[1], exist_ok=True)
        ims = []
        for fr in (f, hop, f, hop):
            im = Image.new('RGBA', (CELL, H), (138, 102, 78, 255))
            im.alpha_composite(Image.fromarray(fr, 'RGBA'))
            ims.append(im.resize((CELL * 6, H * 6), Image.NEAREST).convert('RGB'))
        ims[0].save(os.path.join(sys.argv[1], 'wombat_cheer.gif'), save_all=True, append_images=ims[1:], duration=160, loop=0)
        ims[0].save(os.path.join(sys.argv[1], 'cheer_0.png')); ims[1].save(os.path.join(sys.argv[1], 'cheer_1.png'))


if __name__ == '__main__':
    main()
