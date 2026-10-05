# 농장 올라가는 계단(2026-10-05 사용자 선택 A 「흙 계단」): 2층부터 위 구멍(문 아치 자리)에 선다. 광장 지상 계단(햇빛 · 위 화살표 팻말)을 빌려 쓰던 것을 바꾼다.
# 원본: raw/stairs_up_a.png(Codex, 흙을 깎은 다섯 단 + 조약돌 + 뿌리 + 윗층 등불빛, 프롬프트 raw/prompt_stairs_up.txt). b · c는 고르지 않은 시안(나무 받침 굴 · 사다리 굴)
# blockout: 광장 계단과 같은 기하(문 아치 실루엣 안에 폭이 같은 다섯 단 · 곧은 굴 벽, Plaza/Source~/make_stairs.py)에 맨 위만 등불빛 → raw/stairs_up_blockout16.png
# 그림은 원본 격자 그대로 옮긴다(World/Source~/snap_codex.py, 광장 계단처럼 색이 많아 mean_merge, 하이라이트는 min_hole). 피벗 아래 가운데 = 구멍 가운데.
# 출력: ../stairs_up.png(59×49칸, 한 칸 2px · PPU 80). 실행: Windows Python(Pillow · numpy) make_stairs_up.py [blockout]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Plaza', 'Source~'))
import snap_codex  # noqa: E402
import make_stairs  # noqa: E402
PX = 2
LAMP = (196, 132, 70)   # 윗층 등불빛


def main():
    cells, _ = snap_codex.snap(os.path.join(RAW, 'stairs_up_a.png'), 12, square=True, min_hole=40, mean_merge=True)
    Image.fromarray(np.repeat(np.repeat(cells, PX, 0), PX, 1)).save(os.path.join(HERE, '..', 'stairs_up.png'))
    print('stairs_up cells', cells.shape[1], 'x', cells.shape[0])


if __name__ == '__main__':
    if sys.argv[1:] == ['blockout']:
        make_stairs.blockout(LAMP, os.path.join(RAW, 'stairs_up_blockout16.png'))
    else:
        main()
