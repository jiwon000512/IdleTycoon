# -*- coding: utf-8 -*-
# 계산대 계산 진행: 영수증 출력기(칸 편집, AI 없음). 한 칸 = 2px(PPU 80). 진행 16단계(0% … 100%)를 미리 그려 끼운다(늘이지 않음)
# 2026-09-25 시안 A 가로 막대 · B 작은 원 · C 영수증 출력기 중 C(사용자 선택). 오븐 타이머(make_bar.py)와 같은 크림 판 · 주황 · 진갈색 외곽선
# 22×28칸: 출력기 위로 영수증이 올라오며 줄이 찍히고, 100%면 주황 발바닥 도장. 00 = 빈 출력기(평소 모습)
# 자리: 계산대 윗면 왼쪽. 출력기 밑 외곽선 = 계산대 윗면 맨 앞줄(13줄, 오른쪽 금전등록기와 같은 높이) → 스프라이트 가운데 = 계산대 피벗에서 (−0.6, +0.725)유닛
# 사용: make_counter_timer.py [<출력 폴더>=..] → counter_timer_c_00~15.png
import os, sys
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
N = 16
H = lambda h: np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)] + [255], np.uint8)
LINE, TROUGH, TROUGH_D = H('#342020'), H('#5C4C42'), H('#46382F')
PANEL, PANEL_HI, PANEL_LO = H('#F0E4D8'), H('#FBF4E6'), H('#D9C8B4')
OR, OR_HI = H('#D87848'), H('#F0A070')


def ring(mask):
    # 마스크 바깥 1칸 테두리
    g = mask.copy()
    g[1:] |= mask[:-1]; g[:-1] |= mask[1:]; g[:, 1:] |= mask[:, :-1]; g[:, :-1] |= mask[:, 1:]
    return g & ~mask


def paw(c, x0, y0, col):
    # 발바닥 도장: 발가락 셋 + 패드
    for dx in (0, 2):
        c[y0, x0 + dx] = col
    c[y0 - 1, x0 + 1] = col
    c[y0 + 1:y0 + 3, x0:x0 + 3] = col
    c[y0 + 2, x0] = c[y0 + 2, x0 + 2] = 0


def frame(p):
    W, Hh = 22, 28
    c = np.zeros((Hh, W, 4), np.uint8)
    # 출력기 몸통: 아래 9줄, 폭 18(x 2..19)
    body = np.zeros((Hh, W), bool); body[18:27, 2:20] = True
    body[18, 2] = body[18, 19] = False
    c[ring(body)] = LINE
    c[body] = PANEL_LO
    c[19, 3:19] = PANEL
    c[24:27, 3:19] = TROUGH  # 받침
    c[24, 3:19] = TROUGH_D
    c[21:23, 15:18] = OR  # 버튼
    c[21, 15:18] = OR_HI
    # 영수증: 폭 10(x 6..15), 투입구에서 위로 h칸
    c[18, 5:17] = LINE  # 투입구
    h = round(15 * p)
    if h:
        top = 18 - h
        paper = np.zeros((Hh, W), bool); paper[top:18, 6:16] = True
        edge = ring(paper) & ~body
        edge[18:, :] = False
        c[edge] = LINE
        c[paper] = PANEL_HI
        for yy in range(16, top, -2):  # 찍힌 줄: 아래(먼저 나온 곳)부터
            c[yy, 7:7 + (7 if yy % 4 else 5)] = PANEL_LO
        if p >= 1:
            paw(c, 10, top + 2, OR)
    return c


for i in range(N):
    a = frame(i / (N - 1))
    Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(os.path.join(OUT, 'counter_timer_c_%02d.png' % i))
print('counter_timer_c 22x28 cells x', N, 'frames ->', OUT)
