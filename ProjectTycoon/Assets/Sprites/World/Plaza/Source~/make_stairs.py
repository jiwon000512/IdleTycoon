# 광장 지상 계단 입구(2026-10-01 사용자 「지상과 연결되는 굴 디자인이 게임과 안 맞아」 → 시안 B 「나무 들보 입구」). 프롬프트 raw/stairs_prompt.txt
#   옛 계단(make_plaza.py 시트 f, 칸 수 고정 48칸)은 벽 · 계단이 위로 모이는 원근이라 버렸다.
#   blockout(): 문 아치(../../Shop/arch.png, 60×49칸) 실루엣의 안쪽을 폭이 같은 계단 다섯 단 · 곧은 벽 · 위쪽 햇빛으로 칠한 시점 블록아웃(한 칸 16px)을
#   Codex 참조로 줘서 원근 없이 그리게 했다(raw/stairs_blockout16.png).
#   그림은 원본 격자 그대로 옮긴다(World/Source~/snap_codex.py, 색이 많아 mean_merge, 하이라이트는 min_hole). 피벗 아래 가운데 = 구멍 가운데.
# 출력: ../stairs.png(한 칸 2px · PPU 80). 실행: Windows Python(Pillow · numpy) make_stairs.py [blockout]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
ARCH = os.path.join(HERE, '..', '..', 'Shop', 'arch.png')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2


def blockout(light=(250, 226, 160), path=os.path.join(RAW, 'stairs_blockout16.png')):
    """light = 맨 위 빛(광장은 햇빛, 농장 올라가는 계단은 윗층 등불빛 make_stairs_up.py)"""
    a = np.asarray(Image.open(ARCH).convert('RGBA'))[::PX, ::PX].copy().astype(int)
    h, w = a.shape[:2]
    op = a[..., 3] > 0
    rim = op & (a[..., :3] >= (144, 96, 72)).all(-1)        # 밝은 흙 테두리
    inner = np.zeros((h, w), bool)
    stack = [(h - 4, w // 2)]
    while stack:
        y, x = stack.pop()
        if 0 <= y < h and 0 <= x < w and op[y, x] and not rim[y, x] and not inner[y, x]:
            inner[y, x] = True
            stack += [(y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)]
    pad = np.pad(inner, 1, constant_values=True)
    body = inner & pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    ys = np.nonzero(body)[0]
    bottom = ys.max()
    for y in range(ys.min(), bottom + 1):
        row = np.nonzero(body[y])[0]
        if len(row) == 0:
            continue
        l, r = row.min(), row.max()
        k = bottom - y
        for x in range(l, r + 1):
            if x < l + 3 or x > r - 3:
                c = (92, 60, 46) if x in (l, r) else (116, 78, 58)      # 곧은 굴 벽
            elif k < 30:
                c = (214, 170, 118) if k % 6 in (4, 5) else (150, 104, 74)   # 같은 폭의 디딤판 · 챌판
            else:
                c = light                                               # 맨 위 빛
            a[y, x, :3] = c
    big = Image.fromarray(a.astype(np.uint8)).resize((w * 16, h * 16), Image.NEAREST)
    out = Image.new('RGBA', (big.width + 128, big.height + 128), (255, 255, 255, 255))
    out.alpha_composite(big, (64, 64))
    out.convert('RGB').save(path)
    print('blockout', w, 'x', h)


def main():
    cells, _ = snap_codex.snap(os.path.join(RAW, 'stairs_b.png'), 12, square=True, min_hole=40, mean_merge=True)
    Image.fromarray(np.repeat(np.repeat(cells, PX, 0), PX, 1)).save(os.path.join(HERE, '..', 'stairs.png'))
    print('stairs cells', cells.shape[1], 'x', cells.shape[0])


if __name__ == '__main__':
    blockout() if sys.argv[1:] == ['blockout'] else main()
