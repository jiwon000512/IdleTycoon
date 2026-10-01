# -*- coding: utf-8 -*-
# 반짝돌 뽑기 돌(설계 36 개편, 2026-10-01 사용자 「카드형이 아니라 돌을 깨고 나오는걸로」 · 시안 A 「반짝돌 원석」 선택):
#   뽑기 메인 가운데에 놓이고 결과에서 깨져 유물이 나오는 원석. 회갈색 바위 틈으로 민트 결정(반짝돌 색).
#   원본 raw/draw_stone_a~c.png(Codex, 프롬프트 raw/draw_stone_prompt.txt)에서 A를 원본 격자 그대로 옮긴다(World/Source~/snap_codex.py, 정사각 칸, 흰 하이라이트 남김).
#   시안 B 알 돌 · C 금줄 바위는 버림.
# 출력 ../draw_stone.png(68×66 UI칸, 1 UI px = 화면 4px), 임포트는 ui_slices.json + 메뉴 Import UI Sprites. 실행: Windows Python(Pillow · numpy) make_draw_stone.py
import os
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'World', 'Source~'))
import snap_codex  # noqa: E402

if __name__ == '__main__':
    cells, _ = snap_codex.snap(os.path.join(HERE, 'raw', 'draw_stone_a.png'), 12, square=True, min_hole=40)
    Image.fromarray(cells).save(os.path.join(HERE, '..', 'draw_stone.png'))
    print('draw_stone', cells.shape[1], 'x', cells.shape[0])
