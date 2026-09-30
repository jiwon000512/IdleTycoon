# 월드 값 표식 tag_cost(굴 파기 · 밭 갈기, DigTag 프리팹): 크림 알약 40×17칸, 9-slice (6,5,6,5).
# 2026-09-30 사용자 선택 A(진갈색 바탕 · 노란 테두리 → UI 패널과 같은 크림 · 진갈색 외곽선, 글자는 진갈색 #2E2320).
# 모서리 2칸 깎음, 외곽선 1칸, 위 밝은 줄 · 아래 그늘 줄. 결과는 ../tag_cost.png, 월드 복사본은 메뉴 Import UI Sprites가 만든다
import os
from PIL import Image

W, H = 40, 17
LINE = (52, 32, 32)
FILL = (240, 228, 216)
HI = (251, 244, 230)
SHADE = (217, 200, 180)
# 줄마다 양끝에서 비우는 칸 수(모서리)
CUT = {0: 2, 1: 1, H - 2: 1, H - 1: 2}

im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
px = im.load()

for y in range(H):
    cut = CUT.get(y, 0)

    for x in range(cut, W - cut):
        edge = y in (0, H - 1) or x in (cut, W - 1 - cut) or (y in (1, H - 2) and x in (cut + 1, W - 2 - cut))

        if edge:
            color = LINE
        elif y == 2 and 2 <= x <= W - 3:
            color = HI
        elif y >= H - 3:
            color = SHADE
        else:
            color = FILL

        px[x, y] = color + (255,)

im.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tag_cost.png'))
print('tag_cost', im.size)
