# -*- coding: utf-8 -*-
# 광장 우체통(2026-10-14 아트방, 설계 62 우편함 알림): 광장 계단 옆에 서는 우체통 두 장 — 깃발 내림 mailbox.png · 깃발 올림 mailbox_up.png. 발끝 피벗, 웜뱃 키쯤(52칸 안팎).
#   Codex가 바탕(raw/mailbox_template.png, 한 칸 10px: 흰 바탕 + 웜뱃 52칸 + 빨간 발끝 선 둘) 위에 같은 우체통을 두 번(왼쪽 깃발 내림 · 오른쪽 깃발 올림) 그린다.
#   참조: 바탕(1) · 광장 캡처(2, 결) · 시점 블록아웃(3). 떼기: 원본 격자 그대로(snap_codex raw) → 흰 바탕 · 빨간 선 지움 → 웜뱃(왼쪽 덩어리)을 빼고 남은 두 덩어리를 왼쪽부터 down · up.
#   시안 a 나무 기둥 우체통(둥근 지붕 양철통, 빨간 깃발) · b 벽돌 기둥 우체통(돌 기둥 + 놋쇠 투입구) · c 통나무 그루터기 우체통(나무 속을 판 함 + 나뭇가지 깃발). 사용자 선택 PICK.
# 출력(한 칸 2px · PPU 80, 피벗 아래 가운데): ../mailbox.png · ../mailbox_up.png
# 사용: python make_mailbox.py template <폴더>   바탕 + 프롬프트 셋
#       python make_mailbox.py props <폴더>      <폴더>/mailbox_<k>.png에서 두 장을 떼어 <폴더>/out_<k>/mailbox(_up).png
#       python make_mailbox.py                   고른 시안을 ../mailbox.png · ../mailbox_up.png로
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Fishing', 'Source~'))
import snap_codex  # noqa: E402
from make_decor import groups_of, crop  # noqa: E402

W = os.path.join(HERE, '..', '..')
CW, CH = 170, 100
FOOT = 88
MARKS = (84, 136)
PICK = 'c'

COMMON = ("Use your IMAGE GENERATION tool to paint on top of image 1 (do NOT write code, do NOT resample or copy anything). Keep the canvas size, the white background and the wombat of image 1 "
          "EXACTLY where and how they are: one art pixel = 10 screen pixels, the wombat is 52 art pixels wide and 47 tall. Remove the two short red lines. Use the same big 10-pixel art pixels everywhere, never smaller pixels.\n"
          "This is a cozy underground wombat-burrow idle game (image 2 is the burrow plaza today). Letters for the wombat arrive at a MAILBOX standing in the plaza. "
          "Paint the SAME mailbox TWICE, one at each red line, feet exactly on the red line, about 26 art pixels wide and about 44 art pixels tall (a little shorter than the wombat): "
          "LEFT copy with its little signal flag folded DOWN along the side, RIGHT copy IDENTICAL pixel for pixel except the flag is raised UP. Design: %s\n")
STYLE = ("STYLE: chunky pixel art exactly like the wombat: no anti-aliasing, no gradients, no dithering, flat soft pastel palette with low saturation (at most 10 colors), "
         "dark brown outline (#342020) one art pixel wide, one darker shade on the front and a small white highlight on the top. The flag is the one bright red accent. "
         "VIEW: orthographic three-quarter top-down exactly like the box in image 3: the top face shows, the front face is flat and parallel to the screen, no side faces, "
         "vertical edges stay vertical, never seen from below. No ground, no shadow, no text, no letters, no numbers. The two copies must not touch each other or the wombat.\n"
         "Save the image as mailbox_%s.png in the current working directory and reply with only the path.")
VARIANTS = {
    'a': "a classic rounded-top tin mailbox (cream body, slightly darker front door with a tiny round knob) on a single square wooden post, a red flag on a short arm at its right side.",
    'b': "a stout little stone pillar mailbox built of rounded pale stones, a brass letter slot and a small arched brass door on the front, a red pennant flag on a thin iron rod at its right side.",
    'c': "a hollowed tree-stump mailbox: a short thick stump with bark, a square letter hole cut in the front with a little wooden lid, a few leaves sprouting on top, a red flag tied to a twig at its right side.",
}


def template(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    sc = Image.new('RGBA', (CW, CH), (255, 255, 255, 255))
    wb = Image.open(os.path.join(W, 'Shop', 'wombat_front.png')).convert('RGBA')
    wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
    sc.alpha_composite(wb, (8, FOOT - wb.height))
    big = sc.resize((CW * 10, CH * 10), Image.NEAREST).convert('RGB')
    d = ImageDraw.Draw(big)
    for mx in MARKS:
        d.line((mx * 10 - 70, FOOT * 10, mx * 10 + 70, FOOT * 10), fill=(220, 40, 40), width=4)
    big.save(os.path.join(out_dir, 'mailbox_template.png'))
    for k, design in VARIANTS.items():
        with open(os.path.join(out_dir, 'prompt_%s.txt' % k), 'w', encoding='utf-8') as f:
            f.write(COMMON % design + STYLE % k)
    print(out_dir)


def extract(raw_path):
    cells, _ = snap_codex.snap(raw_path, square=True, min_hole=40, raw=True)
    rgb = cells[..., :3].astype(int)
    keep = cells[..., 3] > 0
    red = (rgb[..., 0] > 170) & (rgb[..., 1] < 90) & (rgb[..., 2] < 90)
    line_rows = np.nonzero(red.sum(1) >= 8)[0]                      # 빨간 발끝 선이 있는 줄만 지운다(깃발의 빨강은 남긴다)
    for y in line_rows:
        keep[y] &= ~red[y]
    gs = groups_of(keep, 20)[:3]
    assert len(gs) == 3, '덩어리 %d(웜뱃 + 우체통 둘이어야 한다)' % len(gs)
    gs.sort(key=lambda g: np.mean([x for _, x in g]))
    down, up = crop(cells, gs[1]), crop(cells, gs[2])
    # 두 장을 같은 크기로(왼쪽 · 아래 맞춤, 깃발은 오른쪽 · 위로 자란다): 발끝 가운데 피벗이 같아야 깃발이 올라갈 때 몸통이 안 움직인다
    h, w = max(down.shape[0], up.shape[0]), max(down.shape[1], up.shape[1])
    def pad(ic):
        out = np.zeros((h, w, 4), np.uint8)
        out[h - ic.shape[0]:, :ic.shape[1]] = ic
        return out
    return pad(down), pad(up)


def match(down, up):
    """깃발 올림을 내림 몸통에 맞춘다: 두 그림을 칸 단위로 가장 잘 겹치는 자리로 밀고, 깃발 쪽(오른쪽 40%% 열)만 올림 그림에서 가져온다(Codex가 나란히 그린 두 장은 몸통이 한두 칸 어긋나고 색이 조금 다르다)"""
    H, W = down.shape[:2]
    d, u = down.astype(int), up.astype(int)
    best = None
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            a = d[max(0, dy):H + min(0, dy), max(0, dx):W + min(0, dx)]
            b = u[max(0, -dy):H + min(0, -dy), max(0, -dx):W + min(0, -dx)]
            n = (np.abs(a - b).sum(-1) > 40).sum()
            if best is None or n < best[0]:
                best = (n, dx, dy)
    _, dx, dy = best
    shifted = np.zeros_like(up)
    for y in range(H):
        for x in range(W):
            sy, sx = y - dy, x - dx
            if 0 <= sy < H and 0 <= sx < W:
                shifted[y, x] = up[sy, sx]
    out = down.copy()
    split = int(W * 0.6)
    out[:, split:] = shifted[:, split:]
    return out, best


def save(ic, path):
    Image.fromarray(ic, 'RGBA').resize((ic.shape[1] * 2, ic.shape[0] * 2), Image.NEAREST).save(path)


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'template':
        template(sys.argv[2])
    elif len(sys.argv) > 2 and sys.argv[1] == 'props':
        for k in VARIANTS:
            src = os.path.join(sys.argv[2], 'mailbox_%s.png' % k)
            if not os.path.exists(src):
                print('없음', src); continue
            out = os.path.join(sys.argv[2], 'out_' + k)
            os.makedirs(out, exist_ok=True)
            down, up = extract(src)
            save(down, os.path.join(out, 'mailbox.png')); save(up, os.path.join(out, 'mailbox_up.png'))
            print(k, down.shape[1], 'x', down.shape[0], '/', up.shape[1], 'x', up.shape[0], '칸')
    else:
        down, up = extract(os.path.join(HERE, 'raw', 'mailbox_%s.png' % PICK))
        up, shift = match(down, up)
        print('올림 맞춤(다른 칸, dx, dy):', shift)
        save(down, os.path.join(HERE, '..', 'mailbox.png')); save(up, os.path.join(HERE, '..', 'mailbox_up.png'))
        print('mailbox.png · mailbox_up.png <-', PICK, down.shape[1], 'x', down.shape[0], '/', up.shape[1], 'x', up.shape[0], '칸')
