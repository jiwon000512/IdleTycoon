# 설계 39 농장 내려가는 계단(2026-10-02 사용자 선택 B3): 층을 다 판 뒤 맨 아래 줄 밑 계단 굴에 서는 바닥 구멍 계단.
# 원본: raw/stairs_down_b.png(Codex, 흙 테 + 조약돌 + 늘어진 뿌리 + 흙 디딤판, 프롬프트 raw/prompt_stairs_down.txt). 원본 격자 그대로 옮긴 뒤(World/Source~/snap_codex.py)
# 「올라가는 계단처럼 보인다」를 줄이려고 칸 단위로만 고쳤다(B3, 다시 뽑지 않음):
#   - 위 테 · 안쪽 벽 띠 · 빛나던 맨 윗단(0~11줄)을 걷어 진짜 굴 바닥이 첫 단이 되게 한다. 바닥 색은 그림에 넣지 않는다(층마다 바닥 색조가 다르다).
#     바닥과 구멍 사이는 외곽선 한 줄(12줄).
#   - 앞 테(43~46줄)를 39~42줄로 올려 맨 아래 어둠을 덮는다(단이 앞 테 밑으로 사라진다).
# blockout(): 폭이 같은 다섯 단 · 아래로 어두워짐 · 아래 어둠(50×46칸, 한 칸 16px)을 Codex 참조로 줘 원근 없이 그리게 했다.
# 출력: ../stairs_down.png(52×31칸, 한 칸 2px · PPU 80). 실행: Windows Python(Pillow · numpy) make_stairs_down.py [blockout]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2
LINE = (52, 32, 32)


def blockout():
    w, h = 50, 46
    a = np.full((h, w, 3), 255, np.uint8)
    for y in range(h):
        for x in range(w):
            if x in (0, w - 1) or y in (0, h - 1):
                a[y, x] = LINE
            elif x < 4 or y < 4:
                a[y, x] = (170, 124, 88)       # 테(빛 받는 쪽)
            elif x > w - 5 or y > h - 5:
                a[y, x] = (140, 98, 70)        # 테(그늘 쪽)
    for k in range(5):
        t = 0.18 * k
        y0 = 4 + k * 6
        a[y0:y0 + 4, 4:w - 4] = [int(c * (1 - t)) for c in (214, 170, 118)]   # 디딤판
        a[y0 + 4:y0 + 6, 4:w - 4] = [int(c * (1 - t)) for c in (150, 104, 74)]  # 챌판
    a[34:h - 4, 4:w - 4] = (44, 30, 26)                                        # 어둠
    big = Image.fromarray(a).resize((w * 16, h * 16), Image.NEAREST)
    out = Image.new('RGB', (big.width + 128, big.height + 128), 'white')
    out.paste(big, (64, 64))
    out.save(os.path.join(RAW, 'stairs_down_blockout16.png'))
    print('blockout', w, 'x', h)


def main():
    a, _ = snap_codex.snap(os.path.join(RAW, 'stairs_down_b.png'), 12, square=True, min_hole=40, mean_merge=True)
    a = a.copy()
    a[39:43] = a[43:47].copy()
    a = a[12:43].copy()
    a[0, :, :3] = LINE
    a[0, :, 3] = 255
    Image.fromarray(np.repeat(np.repeat(a, PX, 0), PX, 1)).save(os.path.join(HERE, '..', 'stairs_down.png'))
    print('stairs_down cells', a.shape[1], 'x', a.shape[0])


if __name__ == '__main__':
    blockout() if sys.argv[1:] == ['blockout'] else main()
