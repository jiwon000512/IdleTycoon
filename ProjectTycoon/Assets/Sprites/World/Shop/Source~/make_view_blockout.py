"""Codex 시점 참조(회색 블록아웃, 2026-09-29): 이 그림을 -i로 주면 원근 없이 그린다. 말로만 평행 투영을 시키면 판을 사다리꼴로 그렸다.
시점 참조(회색 블록아웃): 앞쪽 위에서 내려다본 평행 투영. 윗면 · 앞면만 보이고 옆선은 곧다. 탁자는 앞다리 둘(모서리) + 뒷다리 둘(안쪽, 윗면 깊이만큼 위)"""
import os
from PIL import Image, ImageDraw

C = 10                       # 한 칸 = 10px
W, H = 100, 50               # 칸
OUT, TOPC, FRONT, LEG, BACK = (48, 24, 24), (215, 215, 215), (160, 160, 160), (130, 130, 130), (95, 95, 95)
img = Image.new('RGB', (W * C, H * C), (255, 255, 255))
d = ImageDraw.Draw(img)


def box(x0, y0, x1, y1, fill):
    d.rectangle([x0 * C, y0 * C, (x1 + 1) * C - 1, (y1 + 1) * C - 1], fill=fill, outline=OUT, width=C // 2)


# 탁자: 판 윗면 12칸 깊이, 앞면 4칸. 뒷다리는 안쪽 3칸 · 12칸 위에서 끝난다(판 밑에서만 보인다)
tx0, tx1, ty = 6, 50, 10
box(tx0 + 5, ty + 16, tx0 + 9, ty + 16 + 18 - 12, BACK)        # 뒷다리(왼쪽)
box(tx1 - 9, ty + 16, tx1 - 5, ty + 16 + 18 - 12, BACK)        # 뒷다리(오른쪽)
box(tx0, ty, tx1, ty + 11, TOPC)                               # 윗면
box(tx0, ty + 12, tx1, ty + 15, FRONT)                         # 앞면
box(tx0 + 1, ty + 16, tx0 + 5, ty + 16 + 18, LEG)              # 앞다리(왼쪽 모서리)
box(tx1 - 5, ty + 16, tx1 - 1, ty + 16 + 18, LEG)              # 앞다리(오른쪽 모서리)
# 상자: 윗면 + 앞면
box(62, 14, 92, 25, TOPC)
box(62, 26, 92, 43, FRONT)
img.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'shop_raw', 'view_blockout.png'))
print(img.size)
