# 횟집 작은 탁자(2026-10-06 사용자 선택 B 「둥근 통나무 탁자」, 프롬프트 raw/prompt_table.txt).
#   시안 raw/table_b.png는 한 칸 약 8.2px 정사각으로 그려졌다. 원본 그대로 옮긴다: 원본 픽셀 하나 = 2텍셀, 색 그대로(격자는 snap_codex가 정사각으로 찾음).
#   바깥 번짐은 make_tank.clear_fringe로 지운다.
# 출력: ../table.png(PPU 80, 피벗 밑변 가운데 = 기둥 발끝). 다른 곳에 쓰려면 인자로 경로
# 사용: python make_table.py [출력.png]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, HERE)
import snap_codex  # noqa: E402
from make_tank import clear_fringe  # noqa: E402

if __name__ == '__main__':
    out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'table.png')
    tmp = os.path.join(HERE, 'raw', '_table_crop.png')
    Image.open(os.path.join(HERE, 'raw', 'table_b.png')).convert('RGB').crop((560, 0, 1536, 1024)).save(tmp)   # 웜뱃을 뺀 오른쪽
    cells, _ = snap_codex.snap(tmp, raw=True, square=True)
    os.remove(tmp)
    out = np.repeat(np.repeat(clear_fringe(cells), 2, 0), 2, 1)
    Image.fromarray(out, 'RGBA').save(out_path)
    print('table.png %d x %d px, %.3f x %.3f 유닛' % (out.shape[1], out.shape[0], out.shape[1] / 80, out.shape[0] / 80))
