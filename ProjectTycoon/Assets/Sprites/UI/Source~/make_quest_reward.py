# -*- coding: utf-8 -*-
# 할 일 카드 보상 캡슐 · 진행 점(설계 55, 2026-10-12 사용자 캡슐 시안 A 「금빛 알약」): 화살표를 뺀 카드(A3-나)의 이름 아랫줄에 놓는다.
# Codex 시안 raw/quest_reward_a.png(프롬프트 raw/quest_reward_prompt.txt)의 둥근 끝 모양 · 색을 따서 칸 무늬로 그린다(도형은 칸 무늬).
# 버린 시안: B 쿠폰(양 끝 반달 홈 · 뜯는 점선) · C 진갈색 돈 알약(금빛 안쪽 테).
# quest_reward(진행 중 · 아직 못 받음, 크림) · quest_reward_on(다 함 · 받을 수 있음, 금빛): 14칸(56), 9-slice 좌우 6칸(둥근 끝 전체), 가운데 열은 줄마다 한 색.
# 틀만 그린다(코인 · 숫자는 자식). 흐리게 하지 않고 두 상태를 그림 두 장으로 나눈다.
# quest_pip(빈 진행 점) · quest_pip_on(찬 점, 왼쪽 위 밝은 한 칸): 5×5칸(20), 진갈색 외곽선 + 안 3×3칸.
# 사용: make_quest_reward.py (이 폴더에서) → 에디터 메뉴 ZooTycoon/Bake/Import UI Sprites
import os, json
import numpy as np
from PIL import Image

os.chdir(os.path.dirname(os.path.abspath(__file__)))
OUT = '../'
INK = (52, 32, 32)
# 왼쪽 끝 6칸(오른쪽은 좌우 뒤집기) + 가운데 한 열을 네 번: k 외곽선 · h 위 밝은 줄 · s 위 안쪽 선 · f 바탕 · v 끝 밝은 세로줄 · d 아래 그늘 · . 투명
END = ['.....k',
       '...kkh',
       '..khhs',
       '.kffff',
       '.kffff',
       'kfvfff',
       'kfvfff',
       'kfvfff',
       'kfvfff',
       '.kffff',
       '.kddff',
       '..kddd',
       '...kkd',
       '.....k']
MID = 'khsffffffffddk'
REWARD = {'quest_reward': {'h': (0xF6, 0xEC, 0xDD), 's': (0xC9, 0xAE, 0x9A), 'f': (0xE5, 0xD5, 0xC2), 'v': (0xF6, 0xEC, 0xDD), 'd': (0xC9, 0xAE, 0x9A)},
          'quest_reward_on': {'h': (0xFB, 0xE7, 0xA0), 's': (0xF2, 0xC1, 0x4E), 'f': (0xF2, 0xC1, 0x4E), 'v': (0xFB, 0xE7, 0xA0), 'd': (0xD6, 0x98, 0x39)}}


def capsule(pal):
    pal = dict(pal, k=INK)
    rows = [END[y] + MID[y] * 4 + END[y][::-1] for y in range(len(END))]
    a = np.zeros((len(rows), len(rows[0]), 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                a[y, x] = pal[ch] + (255,)
    return a


def pip(fill, light=None):
    a = np.zeros((5, 5, 4), np.uint8)
    for y in range(5):
        for x in range(5):
            if y in (0, 4) and x in (0, 4):
                continue
            a[y, x] = (INK if y in (0, 4) or x in (0, 4) else fill) + (255,)
    if light:
        a[1, 1] = light + (255,)
    return a


slices = json.load(open(f'{OUT}ui_slices.json', encoding='utf-8'))
for name, pal in REWARD.items():
    a = capsule(pal)
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': int(a.shape[1]), 'h': int(a.shape[0]), 'border': [6, 0, 6, 0]}
    print(name, a.shape[1], 'x', a.shape[0])
for name, a in (('quest_pip', pip((0xD9, 0xC8, 0xB4))), ('quest_pip_on', pip((0xE8, 0xB0, 0x48), (0xFF, 0xF4, 0xC8)))):
    Image.fromarray(a).save(f'{OUT}{name}.png')
    slices[name] = {'w': 5, 'h': 5, 'border': None}
    print(name, 5, 'x', 5)
json.dump(slices, open(f'{OUT}ui_slices.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
