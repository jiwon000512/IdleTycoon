# 점원 웜뱃(설계 21): 빵집 웜뱃 24장(앞·뒤·옆 × 정지·숨쉬기 3·걷기 4)의 털 5색만 바꿔 손님 시트 형식(Resources/Sprites/Visitors/<이름>/, 칸 폭 104px)으로 뽑는다.
# 외곽선·눈·코·귀 안은 그대로. 팔레트를 더하면 VisitorTable에 role clerk 행을 하나 더 두면 된다(외형은 clerk 행 중 균등 난수).
# 사용: python make_wombat_palette.py [gray_d ...]  (인자 없으면 PALETTES 전부)
import os
import sys
from PIL import Image

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(SRC, '..', '..', '..', 'Resources', 'Sprites', 'Visitors')
CELL = 104
# 역할: 밝은 털(앞·옆), 밝은 털(뒤), 그늘, 어두운 그늘, 뒷모습 꼬리
ROLES = [(192, 168, 144), (204, 168, 156), (156, 120, 108), (132, 96, 84), (168, 132, 120)]
# 이름 → (폴더·파일 이름, 5색). gray_d = 남부 털코웜뱃 실사 회갈색(2026-09-26 사용자 선택)
PALETTES = {
    'gray_d': ('WombatGray', [(178, 170, 156), (188, 178, 166), (136, 126, 112), (100, 92, 82), (156, 146, 134)]),
}
SHEETS = {
    'Idle': ['front', 'front_1', 'front_2', 'front_3'],
    'Move': ['front_walk_0', 'front_walk_1', 'front_walk_2', 'front_walk_3'],
    'BackIdle': ['back', 'back_1', 'back_2', 'back_3'],
    'BackMove': ['back_walk_0', 'back_walk_1', 'back_walk_2', 'back_walk_3'],
    'SideIdle': ['side', 'side_1', 'side_2', 'side_3'],
    'SideMove': ['side_walk_0', 'side_walk_1', 'side_walk_2', 'side_walk_3'],
}


def recolor(im, palette):
    table = dict(zip(ROLES, palette))
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) in table:
                px[x, y] = table[(r, g, b)] + (a,)
    return out


def frame(name, palette):
    im = Image.open(os.path.join(SRC, f'wombat_{name}.png')).convert('RGBA')
    return recolor(im, palette)


# 발끝 가운데 피벗을 한 칸(2px) 경계로: BakeryBaker.CellBottom과 같은 규칙(폭 // 4 * 2)
def cell(im):
    out = Image.new('RGBA', (CELL, im.height), (0, 0, 0, 0))
    out.alpha_composite(im, (CELL // 2 - im.width // 4 * 2, 0))
    return out


def bake(key):
    folder, palette = PALETTES[key]
    out_dir = os.path.join(OUT, folder)
    os.makedirs(out_dir, exist_ok=True)
    cell(frame('front', palette)).save(os.path.join(out_dir, folder + '.png'))
    for sheet, names in SHEETS.items():
        frames = [cell(frame(n, palette)) for n in names]
        height = max(f.height for f in frames)
        strip = Image.new('RGBA', (CELL * len(frames), height), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.alpha_composite(f, (i * CELL, height - f.height))
        strip.save(os.path.join(out_dir, f'{folder}_{sheet}.png'))
    print(key, '->', out_dir)


if __name__ == '__main__':
    for key in (sys.argv[1:] or PALETTES):
        bake(key)
