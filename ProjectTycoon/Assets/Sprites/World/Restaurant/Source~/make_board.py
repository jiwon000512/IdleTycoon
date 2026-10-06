# 횟집 둥근 도마(2026-10-06 사용자 선택 A 「통나무 그루터기」, 프롬프트 raw/prompt_board.txt).
#   시안 raw/board_a.png는 한 칸 9.27px 정사각으로 그려졌다. 원본 그대로 옮긴다: 원본 픽셀 하나 = 2텍셀, 색 그대로.
#   격자 시작점은 고정 (6.5, 7.5): 자동으로 찾은 시작점에서는 생선 눈(칸보다 작은 크림 고리 + 눈동자)이 사라진다.
#   이 시작점은 눈을 크림 · 눈동자 · 크림 세 칸으로 남긴다. 바깥 흰 번짐은 make_tank.clear_fringe로 지운다.
#   빈 도마(2026-10-06, 웜뱃이 수조에서 가져온 물고기를 도마에 놓고 뜬다): raw/board_a_empty.png는 board_a를 Codex로 「물고기만 지우기」 한 판
#   (프롬프트 raw/prompt_board.txt 끝). 같은 격자로 옮기면 왼쪽에 한 칸 늘어나 있어 한 칸 밀어 맞추고, 물고기 칸(두 판이 다른 가장 큰
#   덩어리, 윗면 20줄 안) + 둘레 한 칸만 빈 판 칸으로 바꾼다. 나머지(칼 · 테두리 · 다리)는 board_a 그대로라 크기 · 피벗이 같다.
# 출력: ../board.png(빈 도마, PPU 80, 피벗 밑변 가운데 = 그루터기 다리 발끝). 다른 곳에 쓰려면 인자로 경로
# 사용: python make_board.py [출력.png]
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
sys.path.insert(0, HERE)
import snap_codex  # noqa: E402
from make_dig import blobs  # noqa: E402
from make_tank import clear_fringe  # noqa: E402


def cells_of(name):
    tmp = os.path.join(HERE, 'raw', '_board_crop.png')
    Image.open(os.path.join(HERE, 'raw', name)).convert('RGB').crop((560, 0, 1536, 1024)).save(tmp)   # 웜뱃을 뺀 오른쪽
    cells, _ = snap_codex.snap(tmp, raw=True, fixed=(9.27, 6.5, 9.27, 7.5))
    os.remove(tmp)
    return clear_fringe(cells)


if __name__ == '__main__':
    out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'board.png')
    board = cells_of('board_a.png')
    empty = cells_of('board_a_empty.png')[:, 1:1 + board.shape[1]]
    diff = np.abs(board.astype(int) - empty.astype(int)).sum(-1) > 40
    diff[20:] = False
    fish = np.zeros_like(diff)
    for y, x in max(blobs(diff, diag=True), key=len):
        fish[y, x] = True
    grow = fish.copy()
    grow[1:] |= fish[:-1]
    grow[:-1] |= fish[1:]
    grow[:, 1:] |= fish[:, :-1]
    grow[:, :-1] |= fish[:, 1:]
    grow &= board[..., 3] > 0
    board[grow] = empty[grow]
    out = np.repeat(np.repeat(board, 2, 0), 2, 1)
    Image.fromarray(out, 'RGBA').save(out_path)
    print('board.png %d x %d px, %.3f x %.3f 유닛, 물고기 자리 %d칸 바꿈' % (out.shape[1], out.shape[0], out.shape[1] / 80, out.shape[0] / 80, grow.sum()))
