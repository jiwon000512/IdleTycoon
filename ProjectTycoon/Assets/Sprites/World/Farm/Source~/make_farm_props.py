# 농장 입구 기본 장식(2026-09-30 사용자 선택): 입구 아치 왼쪽 씨앗 자루 · 웜뱃 허수아비(시안 B), 오른쪽 모종 작업대 · 삽과 쇠스랑(시안 C).
# 원본: raw/props_a~c.png(Codex 시트, 프롬프트 raw/prompt_props.txt)에서 고른 물체를 잘라 둔 raw/prop_<이름>_part.png.
# 칸으로 옮기기(2026-09-30 사용자 「원본 그대로」): 넷 모두 원본 그림의 픽셀 격자를 찾아 그대로 옮긴다(World/Source~/snap_codex.py, 손질 없음).
#   처음엔 칸 수를 정해 7색으로 줄였는데, 허수아비 눈 · 모종판 싹 · 모종삽 날이 뭉개져 버렸다.
# 출력: ../farm_prop_<이름>.png(한 칸 2px · PPU 80, 피벗 아래 가운데). 실행: Windows Python(Pillow · numpy) make_farm_props.py
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
FARM = os.path.join(HERE, '..')
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402
PX = 2
# (이름, 최대 색 수, 격자)
# 작업대는 외곽선이 얇아 자동 칸 크기(9px)로 옮기면 76칸이라 입구 오른쪽에 도구와 같이 안 들어간다 → 처음 고른 격자를 그대로 적어 둔다(가로 12.48px 시작 9.75 · 세로 11.12px 시작 3.75, 56×31칸)
# 삽과 쇠스랑은 작업대와 같은 시트라 작업대 주기로(자기 격자 7.9px면 63칸 높이로 작업대보다 훨씬 커진다), 시작점만 찾는다
FAITHFUL = [('sack', 12, None), ('scarecrow', 12, None), ('bench', 10, (12.48, 9.75, 11.12, 3.75)), ('tools', 12, 'bench_period')]


def faithful(name, max_colors, grid):
    # 원본 격자 그대로(공용 World/Source~/snap_codex.py). grid: 없으면 찾는다, 네 값이면 그 격자, 'bench_period'면 작업대 주기 근처에서 찾는다
    path = os.path.join(RAW, 'prop_%s_part.png' % name)
    if grid == 'bench_period':
        cells, _ = snap_codex.snap(path, max_colors, cell=(12.48, 11.12))
    else:
        cells, _ = snap_codex.snap(path, max_colors, fixed=grid)
    return cells


def save(a, path):
    Image.fromarray(np.repeat(np.repeat(a, PX, axis=0), PX, axis=1)).save(path)
    n = len(np.unique(a[a[..., 3] > 0][:, :3], axis=0))
    print(os.path.relpath(path, HERE), 'cells', a.shape[1], 'x', a.shape[0], 'colors', n)


if __name__ == '__main__':
    for name, limit, fixed in FAITHFUL:
        save(faithful(name, limit, fixed), os.path.join(FARM, 'farm_prop_%s.png' % name))
