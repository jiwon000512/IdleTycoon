# -*- coding: utf-8 -*-
# 진열대 재고 표지판(칠판 입간판, 2026-09-24 사용자 선택 S1). 20×24칸 + 외곽선 1칸, 한 칸 2px.
# 숫자는 그리지 않는다(ShelfSignView의 TMP 분필 글자). 칠판 가운데 = 발끝에서 15.5칸 위
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))


def hx(s):
    return tuple(int(s[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


OUT = hx("#301818")
W_LIGHT, W_MAIN, W_MID, W_DARK, W_DEEP = hx("#F0C09C"), hx("#CC906C"), hx("#B47860"), hx("#845448"), hx("#543030")
BOARD, BOARD_HI = hx("#34403A"), hx("#44524A")

img = Image.new("RGBA", (20, 24), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
# 다리: 앞 다리 바깥, 뒤 다리는 안쪽으로 비친다
d.rectangle((1, 17, 2, 23), fill=W_DARK)
d.rectangle((17, 17, 18, 23), fill=W_DARK)
d.rectangle((4, 18, 5, 22), fill=W_DEEP)
d.rectangle((14, 18, 15, 22), fill=W_DEEP)
# 나무 틀 + 손잡이
d.rectangle((0, 1, 19, 18), fill=W_MAIN)
d.rectangle((0, 1, 19, 1), fill=W_LIGHT)
d.rectangle((0, 18, 19, 18), fill=W_DARK)
d.rectangle((7, 0, 12, 0), fill=W_MID)
# 칠판과 분필 자국
d.rectangle((2, 3, 17, 16), fill=BOARD)
d.rectangle((2, 3, 17, 3), fill=OUT)
for p in ((4, 6), (15, 13), (5, 14), (13, 5)):
    img.putpixel(p, BOARD_HI)

# 불투명 칸 바깥 1칸 외곽선
out = Image.new("RGBA", (22, 26), (0, 0, 0, 0))
out.paste(img, (1, 1))
px = out.load()
solid = {(x, y) for y in range(26) for x in range(22) if px[x, y][3] > 0}
for y in range(26):
    for x in range(22):
        if (x, y) not in solid and any((x + dx, y + dy) in solid for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            px[x, y] = OUT

out.resize((44, 52), Image.NEAREST).save(os.path.join(HERE, "..", "shelf_sign.png"))
print("shelf_sign", out.size, "cells")
