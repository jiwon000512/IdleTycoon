# -*- coding: utf-8 -*-
# 웜뱃 숨쉬기 · 걷기 · 눈 깜빡임 프레임(부위 조립, anim_parts.py). 2026-09-29 걷기 시안 B3 확정.
#   숨쉬기 4: 기본 → 늘어남(귀 늦게) → 늘어난 채 머묾 → 기본(귀 늦게)
#   걷기 8: 딛기 → 내려앉기(몸 1칸 아래) → 지나가기(1칸 위, 다리 보임, 드는 발 2칸) → 올라오기, 반대 발로 한 번 더.
#           기울기는 발밑 축 1칸(앞·뒤는 디딘 발 쪽, 옆은 딛을 때 앞으로), 귀·앞발·꼬리는 한 박자 늦게
#   깜빡임: 숨쉬기 프레임마다 눈 감은 판(앞·옆, 뒷모습은 눈이 없다)
# 프레임 시간은 게임(WombatView m_idleSeconds · m_walkSeconds)이 갖는다. 여기 DUR은 미리보기용으로 같은 값
# 사용: python make_anim.py [미리보기 폴더]  → ../wombat_<방향>{,_1,_2,_3}.png · _walk_0~7 · _blink_0~3
import os
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from anim_parts import Sprite, save
from anim_specs import WOMBAT

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..')
IDLE_DUR = [500, 160, 520, 160]
WALK_DUR = [80, 65, 65, 65, 80, 65, 65, 65]


def sgn(v):
    return (v > 0) - (v < 0)


def lag(motion):
    """몸이 오르면 한 박자 늦는 부위는 상대적으로 아래(+1), 내리면 위(-1)"""
    return [sgn(motion[t] - motion[t - 1]) for t in range(len(motion))]


def idle_frames():
    return [dict(body_mode=b, ear=e) for b, e in zip([0, 1, 1, 0], [0, 1, 0, -1])]


def walk_frames(view):
    body_y = [0, -1, 1, 0, 0, -1, 1, 0]
    ear = lag(body_y)
    if view == 'side':
        near_x, near_l = [3, 2, 0, -2, -3, -2, 0, 2], [0, 0, 0, 0, 0, 1, 2, 1]
        far_x, far_l = [-3, -2, 0, 2, 3, 2, 0, -2], [0, 1, 2, 1, 0, 0, 0, 0]
        lean = [1, 1, 0, 0, 1, 1, 0, 0]
        frames = [dict(body_y=body_y[t], lifts=(near_l[t], far_l[t]), xs=(near_x[t], far_x[t]), ear=ear[t], tilt_cells=lean[t]) for t in range(8)]
        return frames, [t for t in range(8) if near_l[t]]
    lifts = [(0, 0), (0, 0), (2, 0), (1, 0), (0, 0), (0, 0), (0, 2), (0, 1)]
    lean = [0, 0, 1, 1, 0, 0, -1, -1]
    drag = [-body_y[t - 1] for t in range(8)]
    frames = [dict(body_y=body_y[t], lifts=lifts[t], ear=ear[t], paw=drag[t], tail=ear[t], tilt_cells=lean[t]) for t in range(8)]
    return frames, []


def main():
    issues = []
    made = {}
    for view, spec in WOMBAT.items():
        sp = Sprite(os.path.join(HERE, f'wombat_{view}_base.png'), spec)
        idle = [sp.frame(**f) for f in idle_frames()]
        walk_spec, lifted = walk_frames(view)
        walk = [sp.frame(**f) for f in walk_spec]
        issues += sp.check(idle, f'{view} idle') + sp.check(walk, f'{view} walk', lifted)
        for i, f in enumerate(idle):
            save(f, os.path.join(OUT, f'wombat_{view}{"" if i == 0 else f"_{i}"}.png'))
        for i, f in enumerate(walk):
            save(f, os.path.join(OUT, f'wombat_{view}_walk_{i}.png'))
        blink = []
        if 'blink' in spec:
            blink = [sp.frame(blink=True, **f) for f in idle_frames()]
            issues += sp.check(blink, f'{view} blink')
            for i, f in enumerate(blink):
                save(f, os.path.join(OUT, f'wombat_{view}_blink_{i}.png'))
        made[view] = (idle, walk, blink)
    print('checks:', issues or 'ok')
    if len(sys.argv) > 1:
        preview(made, sys.argv[1])


def preview(made, folder):
    """확인용 GIF(한 칸 = 6px)"""
    os.makedirs(folder, exist_ok=True)

    def im(a):
        bg = Image.new('RGBA', (a.shape[1] * 6, a.shape[0] * 6), (175, 145, 120, 255))
        bg.alpha_composite(Image.fromarray(a, 'RGBA').resize(bg.size, Image.NEAREST))
        return bg.convert('RGB')

    for view, (idle, walk, blink) in made.items():
        for name, frames, dur in (('idle', idle, IDLE_DUR), ('walk', walk, WALK_DUR)):
            ims = [im(f) for f in frames]
            ims[0].save(os.path.join(folder, f'{view}_{name}.gif'), save_all=True, append_images=ims[1:], duration=dur, loop=0)


if __name__ == '__main__':
    main()
