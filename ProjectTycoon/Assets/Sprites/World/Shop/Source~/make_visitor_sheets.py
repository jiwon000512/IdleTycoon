# -*- coding: utf-8 -*-
# 손님 프레임(make_breath_frames.py·make_walk_frames.py 결과) → Resources 시트(가로 1행, 칸 폭 104px(여우 걷기 104px: 왼쪽 여백 9칸 + 꼬리), 발끝 맞춤).
# 경로는 visitors.json 칼럼과 같다. 슬라이스는 에디터 메뉴 ZooTycoon/Bake/Import Visitor Sheets
import os
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../../../../Resources/Sprites/Visitors/'
W = 104


def sheet(names, out):
    frames = [Image.open(n + '.png').convert('RGBA') for n in names]
    h = max(f.height for f in frames)
    o = Image.new('RGBA', (W * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        o.alpha_composite(f, (i * W + (W - f.width) // 2, h - f.height))
    o.save(OUT + out)


for name, folder in (('rabbit', 'Rabbit'), ('penguin', 'Penguin'), ('fox', 'Fox'), ('hedgehog', 'Hedgehog')):
    os.makedirs(OUT + folder, exist_ok=True)
    Image.open(f'{name}_front.png').save(f'{OUT}{folder}/{folder}.png')
    for side, tag in (('front', ''), ('back', 'Back')):
        sheet([f'{name}_{side}'] + [f'{name}_{side}_{i}' for i in (1, 2, 3)], f'{folder}/{folder}_{tag}Idle.png')
        sheet([f'{name}_{side}_walk_{i}' for i in range(4)], f'{folder}/{folder}_{tag}Move.png')
    print(folder)
