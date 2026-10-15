# -*- coding: utf-8 -*-
# 떠내려온 상자(2026-10-14 아트방, 설계 61 지하 호수의 섬들): 유목섬 아래 물에 떠서 왼쪽으로 흘러가는 상자. 둑에서 끌어내면 코인(FishingConfigTable crateCoins).
#   Codex가 바탕(raw/crate_template.png, 한 칸 10px: 흰 바탕 + 둑 띠 A + 흙 면 + 평면 물 + 웜뱃 + 물 위 빨간 선 하나) 위에 그린다(make_wide.py 물 시트 방식).
#   참조: 바탕(1) · 시점 블록아웃(2, 판이 사다리꼴로 기울지 않게) · 물 위 바위 water_rock 10배(3, 물결 테 본보기).
#   떼기: 원본 격자 그대로 칸으로 옮기고(snap_codex raw) 흰 바탕 · 물 색 · 둑 띠 줄 · 빨간 선을 지운 뒤 물 위 가장 큰 덩어리(상자 + 물결 테)를 자른다(make_wide.extract와 같다).
#   시안 a 널 상자(X자 널 + 밧줄 손잡이) · b 쇠테 상자(검은 쇠 띠 두 줄 + 리벳) · c 둥근 뚜껑 궤짝(놋쇠 걸쇠). 사용자 선택 PICK.
# 출력(한 칸 2px · PPU 80, 피벗 아래 가운데 = 물결 테 아래): ../crate.png
# 사용: python make_crate.py template <폴더>   바탕 + 프롬프트 셋(<폴더>/prompt_<k>.txt) + 참조 그림
#       python make_crate.py props <폴더>      <폴더>/crate_<k>.png에서 상자를 떼어 <폴더>/out_<k>/crate.png
#       python make_crate.py                   고른 시안을 ../crate.png로
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Shop', 'Source~'))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import make_bank as mb  # noqa: E402
from make_decor import groups_of, crop  # noqa: E402
import snap_codex  # noqa: E402

CW, CH, BANK_ROW = 130, 140, 60
MARK = (84, 128)          # 물 위 빨간 선: 상자의 가장 아랫픽셀(물결 테) 자리 (x, y)
PICK = 'c'

COMMON = ("Use your IMAGE GENERATION tool to paint on top of image 1 (do NOT write code, do NOT resample or copy anything). Keep the canvas size, the white background, "
          "the wombat, the riverbank strip, the dirt face and the flat teal water of image 1 EXACTLY where and how they are: one art pixel = 10 screen pixels, the wombat is "
          "52 art pixels wide. Remove the short red line. Use the same big 10-pixel art pixels everywhere, never smaller pixels.\n"
          "This is a cozy underground wombat-burrow idle game. The wombat fishes from the bank; in the dark lake below, a lost cargo crate has drifted in and floats on the water. "
          "Paint ONE floating crate at the red line: the crate's lowest visible pixel (the bottom of its waterline ripple) exactly on the red line, the crate rising above it. "
          "The crate is about 30 art pixels wide and about 22 art pixels tall above the water. Its bottom quarter is under water, so where it meets the water draw a thin pale ripple "
          "ring around it exactly like the rock in image 3 (pale lines on the teal, one art pixel thick). Design: %s\n")
STYLE = ("STYLE: chunky pixel art exactly like the wombat and the bank: no anti-aliasing, no gradients, no dithering, flat soft pastel palette with low saturation, "
         "dark brown outline (#342020) one art pixel wide around the crate, one darker shade for the front face and a small white highlight on the top edge. "
         "VIEW: orthographic three-quarter top-down exactly like the box in image 2: the top face shows as a flat parallelogram-free rectangle, the front face is flat and parallel to the screen, "
         "no side faces, vertical edges stay vertical, never seen from below, parallel projection with no vanishing point. No ground shadow, no text, no letters, no numbers. "
         "The crate must not touch the wombat or the bank.\nSave the image as crate_%s.png in the current working directory and reply with only the path.")
VARIANTS = {
    'a': "a pale weathered wooden crate with an X of two planks nailed across its front face, a short frayed rope loop on top, a little moss on one corner.",
    'b': "a darker wooden cargo crate bound by two black iron bands with small rivets, the lid slightly lifted on one side showing a thin dark gap.",
    'c': "a small treasure-chest-like box with a rounded barrel lid of curved planks, a brass clasp on the front and brass corner caps, slightly tilted as it floats.",
}


def template(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    sc = mb.scene(CW, CH, BANK_ROW, mb.STRIPS['a'](), wombat=False, floor_on=False)
    a = np.asarray(sc).copy()
    F = mb.tex('Fishing/stream_face.png').shape[0]
    a[BANK_ROW + F + 1:, :, :3] = mb.LIGHT                      # 물은 평면 한 색(상자를 떼기 쉽게)
    sc = Image.fromarray(a, 'RGBA')
    wb = Image.open(os.path.join(mb.W, 'Shop', 'wombat_front.png')).convert('RGBA')
    wb = wb.resize((wb.width // 2, wb.height // 2), Image.NEAREST)
    sc.alpha_composite(wb, (6, BANK_ROW - 10 - wb.height))
    big = sc.resize((CW * 10, CH * 10), Image.NEAREST).convert('RGB')
    d = ImageDraw.Draw(big)
    mx, my = MARK
    d.line((mx * 10 - 60, my * 10, mx * 10 + 60, my * 10), fill=(220, 40, 40), width=4)
    big.save(os.path.join(out_dir, 'crate_template.png'))
    rock = Image.open(os.path.join(mb.W, 'Fishing', 'water_rock.png')).convert('RGBA')
    rock_big = Image.new('RGBA', (rock.width * 5 + 40, rock.height * 5 + 40), mb.LIGHT + (255,))
    rock_big.alpha_composite(rock.resize((rock.width * 5, rock.height * 5), Image.NEAREST), (20, 20))
    rock_big.convert('RGB').save(os.path.join(out_dir, 'ref_water_rock.png'))
    for k, design in VARIANTS.items():
        with open(os.path.join(out_dir, 'prompt_%s.txt' % k), 'w', encoding='utf-8') as f:
            f.write(COMMON % design + STYLE % k)
    print(out_dir)


def extract(raw_path):
    """Codex 결과 → 상자 칸 배열. 원본 격자 그대로 → 흰 바탕 · 물 색 · 둑 띠 줄 · 빨간 선 지움 → 물 위 가장 큰 덩어리(상자 + 물결 테)"""
    cells, _ = snap_codex.snap(raw_path, square=True, min_hole=40, raw=True)
    rgb = cells[..., :3].astype(int)
    water = (np.abs(rgb - mb.LIGHT).max(-1) <= 28) & (cells[..., 3] > 0)
    wtop = int(np.nonzero(water.mean(1) > 0.5)[0].min())
    keep = (cells[..., 3] > 0) & ~water
    keep[:wtop] = False
    red = (rgb[..., 0] > 170) & (rgb[..., 1] < 90) & (rgb[..., 2] < 90)
    keep &= ~red
    gs = groups_of(keep, 12)
    assert gs, '물 위 덩어리 없음'
    return crop(cells, gs[0])


def props(folder):
    for k in VARIANTS:
        src = os.path.join(folder, 'crate_%s.png' % k)
        if not os.path.exists(src):
            print('없음', src)
            continue
        out = os.path.join(folder, 'out_%s' % k)
        os.makedirs(out, exist_ok=True)
        ic = extract(src)
        Image.fromarray(ic, 'RGBA').resize((ic.shape[1] * 2, ic.shape[0] * 2), Image.NEAREST).save(os.path.join(out, 'crate.png'))
        print(k, ic.shape[1], 'x', ic.shape[0], '칸')


if __name__ == '__main__':
    if len(sys.argv) > 2 and sys.argv[1] == 'template':
        template(sys.argv[2])
    elif len(sys.argv) > 2 and sys.argv[1] == 'props':
        props(sys.argv[2])
    else:
        # raw/crate_<k>.png는 Codex 바탕째 원본이 아니라 거기서 뗀 상자(한 칸 2px)다: 원본 캔버스는 같은 이름의 게임 캡처에 덮여 사라졌다(2026-10-14, 캡처 이름을 시안과 겹치게 지은 실수).
        # 뗀 칸은 원본 픽셀 색 그대로라 그림은 같고, 둘레(웜뱃 · 둑 · 물)만 없다. 다시 뽑으면 props 흐름을 쓴다
        src = os.path.join(HERE, 'raw', 'crate_%s.png' % PICK)
        im = Image.open(src).convert('RGBA')
        if im.width > 400:
            ic = extract(src)
            im = Image.fromarray(ic, 'RGBA').resize((ic.shape[1] * 2, ic.shape[0] * 2), Image.NEAREST)
        im.save(os.path.join(HERE, '..', 'crate.png'))
        print('crate.png <-', PICK, im.width // 2, 'x', im.height // 2, '칸')
