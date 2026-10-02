# 설계 22 이모지 말풍선 시트(2026-10-02 사용자 선택 C 「색 + 그늘 + 움직임」, 시안 A 굵은 한 색 · B 색 + 그늘(정지)은 버림).
# 틀과 기다림 점 셋은 wait_sheet(52×36, 2026-09-25 시안 A) 그대로, 안의 기호 일곱을 칸(2px) 무늬로 찍는다. 기호는 10줄 안,
# 바탕색 + 아래 · 오른쪽 한 톤 어둡게 + 왼쪽 위 밝은 한 획(사물 그림의 빛), 기호마다 두 칸이 번갈아 돈다.
# 칸 순서(BubbleTable frame · frames): 0~2 점 셋 · 3~4 ♪(통통) · 5~6 ?(통통) · 7~8 !(통통) · 9~10 ♥(콩닥) · 11~12 !!(좌우 떨림)
#   · 13~14 💬(두 말풍선 번갈아 들썩) · 15~16 🤢(메스꺼운 얼굴, 내려앉으며 볼이 부풂)
#   · 17~18 ✨ 감탄(설계 40 별 평가, 2026-10-02 사용자 선택 B 「큰 반짝이」 · 시안 A 반짝 셋 · C 별 + 반짝은 버림): 금빛 반짝이가 + ↔ ×로 돌고 작은 점이 자리를 바꾼다
# 출력 ../bubble_sheet.png(칸 폭 52px · 19칸). 실행: Windows Python(Pillow · numpy) make_bubble_sheet.py
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..')
CELL_W = 26  # 칸 수(한 칸 = 2px)
CREAM = (240, 228, 216)
DOT = (92, 76, 66)

# '#' 바탕 · 'x' 얼굴 이목구비 · 'c' 크림(말풍선 안 점)
G = {
    'note': [
        "......##....",
        "......####..",
        "......##.##.",
        "......##..##",
        "......##...#",
        "......##....",
        "...#####....",
        "..######....",
        "..#####.....",
        "...###......",
    ],
    'question': [
        "..####..",
        ".######.",
        "##....##",
        "......##",
        "....###.",
        "...###..",
        "...##...",
        "........",
        "...##...",
        "...##...",
    ],
    'alert': [
        "##",
        "##",
        "##",
        "##",
        "##",
        "##",
        "..",
        "##",
        "##",
    ],
    'heart': [
        ".###....###.",
        "#####..#####",
        "############",
        "############",
        "############",
        ".##########.",
        "..########..",
        "...######...",
        "....####....",
        ".....##.....",
    ],
    'heart_small': [
        "..##....##..",
        ".####..####.",
        ".##########.",
        ".##########.",
        "..########..",
        "...######...",
        "....####....",
        ".....##.....",
    ],
    'angry': [
        "##..##",
        "##..##",
        "##..##",
        "##..##",
        "##..##",
        "##..##",
        "......",
        "##..##",
        "##..##",
    ],
    'chat_l': [
        ".######.",
        "########",
        "#c#c#c##",
        "########",
        ".######.",
        ".##.....",
        ".#......",
    ],
    'chat_r': [
        ".######.",
        "########",
        "##c#c#c#",
        "########",
        ".######.",
        ".....##.",
        "......#.",
    ],
    # 🤢 꼭 감은 눈(> <) · 물결 입
    'yuck': [
        "..######..",
        ".########.",
        "##x####x##",
        "###x##x###",
        "##x####x##",
        "##########",
        "###x##x###",
        "##x#xx#x##",
        ".########.",
        "..######..",
    ],
}

# 바탕 · 그늘 · 하이라이트
TONE = {
    'note': ((200, 120, 72), (156, 86, 52), (240, 192, 150)),
    'question': ((84, 132, 170), (60, 98, 134), (176, 210, 228)),
    'alert': ((206, 82, 62), (156, 54, 44), (244, 172, 152)),
    'heart': ((226, 96, 112), (178, 62, 82), (250, 198, 202)),
    'angry': ((206, 82, 62), (156, 54, 44), (244, 172, 152)),
    'chat_l': ((104, 160, 138), (74, 122, 104), (190, 226, 210)),
    'chat_r': ((214, 150, 92), (168, 108, 62), (244, 208, 166)),
    'yuck': ((150, 178, 104), (110, 140, 74), (206, 224, 170)),
}
TONE['heart_small'] = TONE['heart']
FEATURE = (58, 76, 42)


def mask(name):
    rows = G[name]
    m = np.array([[c == '#' for c in r] for r in rows])
    x = np.array([[c in 'xc' for c in r] for r in rows])
    return m | x, x


def place(name, dy=0, dx=0):
    # 말풍선 안쪽(크림 1~13줄) 가운데, 10줄 기호는 3~12줄
    m, _ = mask(name)
    h, w = m.shape
    return 3 + (10 - h) // 2 + dy, 13 - (w + 1) // 2 + dx


def paint(rgb, name, dy=0, dx=0):
    m, x = mask(name)
    h, w = m.shape
    top, left = place(name, dy, dx)
    base, shade, light = TONE[name]
    for j in range(h):
        for i in range(w):
            if not m[j, i]:
                continue
            if G[name][j][i] == 'c':
                col = CREAM
            elif x[j, i]:
                col = FEATURE
            else:
                # 그늘: 아래 · 오른쪽이 바깥인 칸(빛은 왼쪽 위에서)
                below = j + 1 >= h or not m[j + 1, i]
                right = i + 1 >= w or not m[j, i + 1]
                col = shade if (below or right) else base
            rgb[top + j, left + i] = col
    # 하이라이트 한 획: 위가 바깥이고 아래 · 오른쪽이 바탕인 첫 칸에서 가로 2칸(♪ · ! · !!은 1칸)
    body = m & ~x
    for j in range(h):
        hits = [i for i in range(w) if body[j, i] and (j == 0 or not m[j - 1, i]) and i + 1 < w and body[j, i + 1]
                and j + 1 < h and m[j + 1, i]]
        if hits:
            i = hits[0]
            rgb[top + j, left + i] = light
            if name not in ('alert', 'angry', 'note') and body[j, i + 1]:
                rgb[top + j, left + i + 1] = light
            break


# ✨ 반짝이: 금빛 바탕, 가운데 흰 칸 · 그 둘레 밝은 칸, 오른쪽 아래 끝은 그늘(가는 팔이라 사물 그늘 규칙 대신)
GOLD, GOLD_D, GOLD_L, GLINT = (240, 190, 72), (198, 144, 46), (252, 228, 150), (255, 252, 240)
SPARK = ["....#....", "....#....", "...###...", "..#####..", "#########", "..#####..", "...###...", "....#....", "....#...."]
SPARK_X = ["#.....#", ".#...#.", "..#.#..", "...#...", "..#.#..", ".#...#.", "#.....#"]
SPARK_DOT = [".#.", "###", ".#."]


def sparkle(f, pat, cy, cx):
    h, w = len(pat), len(pat[0])
    r = max(h, w) // 2
    for j, row in enumerate(pat):
        for i, ch in enumerate(row):
            if ch != '#':
                continue
            dy, dx = j - h // 2, i - w // 2
            col = GOLD_D if dx + dy >= r - 1 and (dx > 0 or dy > 0) else GOLD
            if dx == 0 and dy == 0:
                col = GLINT
            elif abs(dx) + abs(dy) == 1:
                col = GOLD_L
            f[cy + dy, cx + dx] = col + (255,)


def wow(blank, k):
    f = blank.copy()
    sparkle(f, SPARK if k == 0 else SPARK_X, 7, 13)
    sparkle(f, SPARK_DOT, *((4, 6) if k == 0 else (10, 20)))
    return f


def blank_frame(wait):
    # 빈 말풍선 틀: 기다림 첫 칸에서 점을 크림으로 지운다
    f = wait[:, :CELL_W].copy()
    f[(f[..., :3] == DOT).all(-1) & (f[..., 3] > 0)] = CREAM + (255,)
    return f


def glyph(blank, parts):
    f = blank.copy()
    for name, dy, dx in parts:
        paint(f[..., :3], name, dy, dx)
    return f


def yuck_puffed(blank):
    # 🤢 두 번째 칸: 한 칸 내려앉으며 양 볼이 한 칸씩 부푼다
    f = glyph(blank, [('yuck', 1, 0)])
    top, left = place('yuck', 1, 0)
    for j in (top + 5, top + 6):
        for i in (left - 1, left + 10):
            f[j, i] = TONE['yuck'][1] + (255,)
    return f


def make_sheet():
    wait = np.asarray(Image.open(os.path.join(SHOP, 'wait_sheet.png')).convert('RGBA'))[::2, ::2]
    blank = blank_frame(wait)
    frames = [wait[:, i * CELL_W:(i + 1) * CELL_W] for i in range(3)]
    frames += [glyph(blank, [('note', 0, 0)]), glyph(blank, [('note', -1, 1)])]
    frames += [glyph(blank, [('question', 0, 0)]), glyph(blank, [('question', -1, 0)])]
    frames += [glyph(blank, [('alert', 0, 0)]), glyph(blank, [('alert', -1, 0)])]
    frames += [glyph(blank, [('heart', 0, 0)]), glyph(blank, [('heart_small', 0, 0)])]
    frames += [glyph(blank, [('angry', 0, 0)]), glyph(blank, [('angry', 0, 1)])]
    # 💬 왼쪽 위 · 오른쪽 아래 두 말풍선, 두 번째 칸은 오른쪽이 올라오고 왼쪽이 내려간다
    frames += [glyph(blank, [('chat_l', -2, -4), ('chat_r', 2, 5)]), glyph(blank, [('chat_l', -1, -4), ('chat_r', 1, 5)])]
    frames += [glyph(blank, [('yuck', 0, 0)]), yuck_puffed(blank)]
    frames += [wow(blank, 0), wow(blank, 1)]
    sheet = np.concatenate(frames, axis=1)
    Image.fromarray(np.repeat(np.repeat(sheet, 2, 0), 2, 1)).save(os.path.join(SHOP, 'bubble_sheet.png'))
    print('bubble_sheet', sheet.shape[1] // CELL_W, 'frames', sheet.shape[1] * 2, 'x', sheet.shape[0] * 2)


if __name__ == '__main__':
    make_sheet()
