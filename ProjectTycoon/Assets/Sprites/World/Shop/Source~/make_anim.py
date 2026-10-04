# -*- coding: utf-8 -*-
# 캐릭터 숨쉬기 · 걷기 · 눈 깜빡임 · 딴짓 프레임(부위 조립, anim_parts.py). 2026-09-29 걷기 시안 B3 확정.
#   숨쉬기 4: 기본 → 몸 전체 1칸 내려앉기(귀 · 꼬리 늦게) → 머묾 → 기본(귀 · 꼬리 늦게)
#   걷기 8: 딛기 → 내려앉기(몸 1칸 아래) → 지나가기(1칸 위, 다리 보임, 드는 발 2칸) → 올라오기, 반대 발로 한 번 더.
#           기울기는 발밑 축 1칸(앞·뒤는 디딘 발 쪽, 옆은 딛을 때 앞으로), 귀·앞발·꼬리는 한 박자 늦게
#   깜빡임: 숨쉬기 프레임마다 눈 감은 판(눈이 있는 방향만)
#   딴짓: 동물 · 방향마다 한 벌(FIDGETS). 게임이 서 있을 때 가끔 숨쉬기 사이에 한 번 끼워 넣는다
# 프레임 시간은 게임(WombatView · VisitorView의 m_idleSeconds · m_walkSeconds · m_fidgetFrameSeconds)이 갖는다. 여기 DUR · TICK은 미리보기용으로 같은 값
# 결과
#   웜뱃: ../wombat_<방향>{,_1,_2,_3}.png · _walk_0~7 · _blink_0~3 · _fidget(시트) (BakeryBaker가 임포트)
#   손님: Resources/Sprites/Visitors/<이름>/<이름>_{Idle,Move,BackIdle,BackMove,SideIdle,SideMove,Blink,SideBlink,Fidget,BackFidget,SideFidget}.png
#         (가로 1행, 칸 폭 104px, 발끝 = 아래 끝. 슬라이스는 ZooTycoon/Bake/Import Visitor Sheets)
#   점원 회색 웜뱃: 웜뱃 기본 그림의 털 5색을 바꾸고 요리사 모자를 얹은 그림(make_clerk.py)을 웜뱃 좌표로 조립한 시트(WombatGray)
# 사용: python make_anim.py [미리보기 폴더]
import os
import sys
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from anim_parts import Sprite, save, opaque
from anim_specs import WOMBAT, VISITORS
from make_clerk import build as clerk_base

HERE = os.path.dirname(os.path.abspath(__file__))
SHOP = os.path.join(HERE, '..')
SHEETS = os.path.join(HERE, '..', '..', '..', '..', 'Resources', 'Sprites', 'Visitors')
CELL = 52   # 시트 칸 폭(칸) = 104px
# 꼬리가 옆으로 나와 104px에 안 드는 종만 넓게(VisitorSheetImporter도 같은 폴더를 같은 폭으로 자른다). 너구리 앞모습은 발 가운데 피벗으로 54칸 + 꼬리 살랑 1칸
CELLS = {'Tanuki': 56}
IDLE_DUR = [500, 160, 520, 160]
WALK_DUR = [80, 65, 65, 65, 80, 65, 65, 65]
FOLDERS = {'rabbit': 'Rabbit', 'penguin': 'Penguin', 'fox': 'Fox', 'hedgehog': 'Hedgehog', 'tanuki': 'Tanuki', 'owl': 'Owl'}
# 점원 회색 웜뱃(2026-09-26 gray_d): 빵집 웜뱃 털 5색 → 남부 털코웜뱃 회갈색. 외곽선·눈·코·귀 안은 그대로
GRAY = ('WombatGray', {
    (192, 168, 144): (178, 170, 156),   # 밝은 털(앞·옆)
    (204, 168, 156): (188, 178, 166),   # 밝은 털(뒤)
    (156, 120, 108): (136, 126, 112),   # 그늘
    (132, 96, 84): (100, 92, 82),       # 어두운 그늘
    (168, 132, 120): (156, 146, 134),   # 뒷모습 꼬리
})


def sgn(v):
    return (v > 0) - (v < 0)


def lag(motion):
    """몸이 오르면 한 박자 늦는 부위는 상대적으로 아래(+1), 내리면 위(-1)"""
    return [sgn(motion[t] - motion[t - 1]) for t in range(len(motion))]


def idle_frames():
    """숨쉬기 4: 발 위 몸 전체가 1칸 내려앉았다 올라온다(든 빵도 같이, 게임 m_idleBob). 귀 · 꼬리는 한 박자 늦게"""
    body_y = [0, -1, -1, 0]
    late = lag(body_y)
    return [dict(body_y=body_y[t], ear=late[t], tail=late[t]) for t in range(4)]


# 딴짓(2026-09-29): 서 있을 때 가끔 한 번 보이는 동작. 한 칸 TICK ms, 머묾은 같은 프레임을 되풀이한다.
# 몸 높이는 바꾸지 않는다(든 빵이 따로 놀지 않게). 눈이 없는 방향(뒤)은 look이 빠지고 기울기만 남는다
# ponytail: 머묾을 되풀이로 만들어 시트가 넓다(4096px = 39칸까지, VisitorSheetImporter 최대 크기). 더 길어지면 겹친 프레임을 합치고 프레임별 시간으로
TICK = 80
SHEET_MAX = 4096
REST = ({}, 1)


def seq(steps):
    return [dict(f) for f, n in steps for _ in range(n)]


def look_around():
    """두리번: 눈이 먼저 1칸 돌아가고 몸이 따라 기운다(왼쪽 → 오른쪽). 기울면 눈이 몸과 같이 1칸 가므로 그때는 눈을 제자리로
    (둘을 겹치면 눈이 2칸씩 가서 사용자 「눈이 너무 좌우로 움직임」)"""
    return [(dict(look=(-1, 0)), 2), (dict(tilt_cells=-1), 6), REST,
            (dict(look=(1, 0)), 2), (dict(tilt_cells=1), 6), REST]


def look_up():
    """옆모습 올려다보기: 눈을 올리고 몸을 뒤로 젖힌다"""
    return [(dict(look=(0, -1)), 2), (dict(look=(0, -1), tilt_cells=-1), 8), (dict(look=(0, -1)), 1), REST]


def tap(i, n=2):
    """발 구르기: i번 발을 1칸 들었다 딛기"""
    return [(dict(lifts=(1, 0) if i == 0 else (0, 1)), 1), REST] * n


def thump(i):
    """토끼 발 구르기: 높이 들었다가 쿵"""
    return [(dict(lifts=(1, 0) if i == 0 else (0, 1)), 1), (dict(lifts=(2, 0) if i == 0 else (0, 2)), 3), REST, REST]


def waddle(n=2):
    """뒤뚱: 기운 쪽 발로 딛고 반대 발을 든다"""
    return [(dict(tilt_cells=-1, lifts=(0, 1)), 3), REST, (dict(tilt_cells=1, lifts=(1, 0)), 3), REST] * n


def shake(n=3):
    """부르르: 좌우로 빠르게"""
    return [(dict(tilt_cells=-1), 1), (dict(tilt_cells=1), 1)] * n + [REST]


def sniff(n=3):
    """킁킁: 앞으로 살짝 숙였다 든다(옆모습)"""
    return [(dict(tilt_cells=1), 1), REST] * n


def wag(part, right=2, left=2, n=2):
    """꼬리 살랑: 양쪽으로(칸 폭 104px에 걸리는 쪽은 덜)"""
    return [(dict(swing={part: right}), 2), REST, (dict(swing={part: -left}), 2), REST] * n


def flick(parts, n=2):
    """귀 쫑긋 · 날개 파닥: 바깥으로 갔다 돌아온다. parts = {부위: 칸}"""
    return [(dict(swing=parts), 2), REST] * n


def rub(n=3):
    """앞발 비비기(웜뱃 앞모습, 앞발 블록 1칸 위)"""
    return [(dict(paw=-1), 1), REST] * n


FIDGETS = {
    'wombat': {
        'front': look_around() + rub() + [REST],
        'back': [(dict(tilt_cells=-1, tail=-1), 2), REST, (dict(tilt_cells=1, tail=-1), 2), REST] * 2 + [(dict(ear=-1), 2), REST],
        'side': tap(0) + look_up(),
    },
    'rabbit': {
        'front': flick({'ear_r': 2}) + flick({'ear_l': -2}, 1) + thump(0),
        'back': flick({'ear_l': -2}) + flick({'ear_r': 2}, 1) + thump(1),
        'side': flick({'ears': -2}) + thump(0),
    },
    'penguin': {
        'front': waddle() + flick({'fl_l': 2, 'fl_r': -2}, 3),
        'back': waddle() + flick({'fl_l': 2, 'fl_r': -2}, 3),
        'side': sniff(2) + tap(1),
    },
    'fox': {
        'front': wag('tail', right=1) + look_around(),
        'back': wag('tail', n=3) + [(dict(ear=(-1, 0)), 2), REST, (dict(ear=(0, -1)), 2), REST],
        'side': wag('tail') + look_up(),
    },
    # 설계 31 유물 행상: 꼬리 살랑 + 오른 앞발 흔들기(손님에게 손짓) · 뒤는 귀 쫑긋 + 발 구르기
    'tanuki': {
        'front': wag('tail', right=1, left=1, n=1) + flick({'paw_r': 1}, 3) + look_around(),   # 112px 칸이라 36프레임까지
        'back': flick({'ear_l': -1, 'ear_r': 1}) + tap(0) + tap(1),
        'side': wag('tail', right=1, left=1) + look_up(),
    },
    # 설계 40 평가단장 부엉이: 날개 · 귀깃을 떼지 않아 몸 동작만. 앞 두리번(부엉이 고개) + 뒤뚱, 뒤 뒤뚱 + 발 구르기,
    #   옆은 고개 까딱 + 발 구르기(올려다보기는 눈 바로 위를 안경다리가 지나가 눈을 옮기면 안경알이 깨진다)
    'owl': {
        'front': look_around() + waddle(1),
        'back': waddle() + tap(0),
        'side': sniff(2) + tap(0),
    },
    'hedgehog': {
        'front': shake() + look_around(),
        'back': shake() + tap(0) + tap(1),
        'side': sniff() + shake(2),
    },
}


def walk_frames(view, far_foot):
    """far_foot: 옆모습 먼 발을 가까운 발로 만들었나(웜뱃). 아니면 두 발을 원본에서 떼어 앞·뒤로 옮긴다(손님: 왼쪽 = 뒷발)"""
    body_y = [0, -1, 1, 0, 0, -1, 1, 0]
    ear = lag(body_y)
    if view == 'side':
        front_x, front_l = [3, 2, 0, -2, -3, -2, 0, 2], [0, 0, 0, 0, 0, 1, 2, 1]
        back_x, back_l = [-3, -2, 0, 2, 3, 2, 0, -2], [0, 1, 2, 1, 0, 0, 0, 0]
        lean = [1, 1, 0, 0, 1, 1, 0, 0]
        if far_foot:
            xs, lifts = list(zip(front_x, back_x)), list(zip(front_l, back_l))
        else:
            xs, lifts = list(zip(back_x, front_x)), list(zip(back_l, front_l))
        frames = [dict(body_y=body_y[t], lifts=lifts[t], xs=xs[t], ear=ear[t], tilt_cells=lean[t]) for t in range(8)]
        lifted = [t for t in range(8) if lifts[t][0] and lifts[t][1] == 0 or lifts[t][1] and lifts[t][0] == 0]
        return frames, lifted
    lifts = [(0, 0), (0, 0), (2, 0), (1, 0), (0, 0), (0, 0), (0, 2), (0, 1)]
    lean = [0, 0, 1, 1, 0, 0, -1, -1]
    drag = [-body_y[t - 1] for t in range(8)]
    frames = [dict(body_y=body_y[t], lifts=lifts[t], ear=ear[t], paw=drag[t], tail=ear[t], tilt_cells=lean[t]) for t in range(8)]
    return frames, []


def animate(path, spec, view, name, issues):
    sp = Sprite(path, spec)
    idle = [sp.frame(**f) for f in idle_frames()]
    walk_spec, lifted = walk_frames(view, any(f.get('far') for f in spec['feet']))
    walk = [sp.frame(**f) for f in walk_spec]
    blink = [sp.frame(blink=True, **f) for f in idle_frames()] if 'blink' in sp.s else []
    fidget_spec = seq(FIDGETS[name][view])
    fidget = [sp.frame(**f) for f in fidget_spec]
    issues += sp.check(idle, f'{name} {view} idle') + sp.check(walk, f'{name} {view} walk', lifted) + sp.check(blink, f'{name} {view} blink')
    issues += sp.check(fidget, f'{name} {view} fidget', [i for i, f in enumerate(fidget_spec) if any(f.get('lifts', ()))])
    if sp.look_bad:
        issues.append(f'{name} {view} look covers {sp.look_bad} non-fur cells')
    return idle, walk, blink, fidget


# 점원 요리사 모자(make_clerk.py): 모자 칸(12~29열)은 귀가 한 박자 늦게 움직이는 범위에서 뺀다(같이 밀리면 모자가 찢어진다).
#   옆모습은 귀가 모자 띠 밑에 끼어 있어 귀 늦게 움직이기를 끈다
CLERK_EARS = {'front': [(5, 11), (30, 36)], 'back': [(4, 11), (30, 37)], 'side': []}


def shifted(spec, dy, ears):
    """WOMBAT 좌표를 dy줄 내린 사본(점원 모자 높이만큼 늘린 줄) + 모자 칸을 뺀 귀 범위"""
    s = {k: v for k, v in spec.items()}
    s['feet'] = [dict(f, rows=(f['rows'][0] + dy, f['rows'][1] + dy)) if 'rows' in f else dict(f) for f in spec['feet']]
    for k in ('ear', 'stretch'):
        if k in s:
            s[k] += dy
    for k in ('paw', 'tail'):
        if k in s:
            r0, r1, c0, c1 = s[k]
            s[k] = (r0 + dy, r1 + dy, c0, c1)
    if 'eyes' in s:
        s['eyes'] = [(r0 + dy, r1 + dy, c0, c1) for r0, r1, c0, c1 in s['eyes']]
    if 'blink' in s:
        s['blink'] = [(r + dy, c, ch) for r, c, ch in s['blink']]
    if 'blink_fill' in s:
        s['blink_fill'] = (s['blink_fill'][0] + dy, s['blink_fill'][1])
    if 'clear_rows' in s:
        s['clear_rows'] = [r + dy for r in s['clear_rows']]
    s['ears'] = ears
    return s


def sheet(frames, path, cell=CELL):
    """칸 폭 cell칸(기본 104px) 가로 1행. 위 빈 줄은 잘라 시트 높이 = 가장 높은 프레임. 가로는 캔버스 가운데가 칸 가운데(BakeryBaker.CellBottom과 같은 규칙)"""
    top = min(np.nonzero(opaque(f).any(1))[0].min() for f in frames)
    frames = [f[top:] for f in frames]
    w = frames[0].shape[1]
    if w > cell:
        cut = (w - cell + 1) // 2
        assert not any(opaque(f)[:, :cut].any() or opaque(f)[:, w - cut:].any() for f in frames), f'{path}: 칸 폭 {cell * 2}px를 넘는다'
        frames = [f[:, cut:w - cut] for f in frames]
        w = frames[0].shape[1]
    h = max(f.shape[0] for f in frames)
    assert cell * len(frames) * 2 <= SHEET_MAX, f'{path}: 시트 폭 {cell * len(frames) * 2}px > {SHEET_MAX}px(Unity가 줄여 칸이 잘린다)'
    out = np.zeros((h, cell * len(frames), 4), np.uint8)
    x0 = cell // 2 - w // 2
    for i, f in enumerate(frames):
        out[h - f.shape[0]:, i * cell + x0:i * cell + x0 + w] = f
    save(out, path)


def sheets(folder, views):
    """views: 방향 → (숨쉬기, 걷기, 깜빡임, 딴짓)"""
    d = os.path.join(SHEETS, folder)
    os.makedirs(d, exist_ok=True)
    cell = CELLS.get(folder, CELL)
    for view, tag in (('front', ''), ('back', 'Back'), ('side', 'Side')):
        idle, walk, blink, fidget = views[view]
        sheet(idle, os.path.join(d, f'{folder}_{tag}Idle.png'), cell)
        sheet(walk, os.path.join(d, f'{folder}_{tag}Move.png'), cell)
        sheet(fidget, os.path.join(d, f'{folder}_{tag}Fidget.png'), cell)
        if blink:
            sheet(blink, os.path.join(d, f'{folder}_{tag}Blink.png'), cell)


def main():
    issues = []
    made = {}
    # 웜뱃(빵집 웜뱃 그림은 한 장씩, BakeryBaker가 임포트)
    wombat = {}
    for view, spec in WOMBAT.items():
        idle, walk, blink, fidget = animate(os.path.join(HERE, f'wombat_{view}_base.png'), spec, view, 'wombat', issues)
        for i, f in enumerate(idle):
            save(f, os.path.join(SHOP, f'wombat_{view}{"" if i == 0 else f"_{i}"}.png'))
        for i, f in enumerate(walk):
            save(f, os.path.join(SHOP, f'wombat_{view}_walk_{i}.png'))
        for i, f in enumerate(blink):
            save(f, os.path.join(SHOP, f'wombat_{view}_blink_{i}.png'))
        # 딴짓은 프레임이 많아 시트 한 장(칸 폭 104px, BakeryBaker가 자른다)
        sheet(fidget, os.path.join(SHOP, f'wombat_{view}_fidget.png'))
        wombat[view] = (idle, walk, blink, fidget)
    made['wombat'] = wombat
    # 점원 회색 웜뱃 + 요리사 모자(2026-10-02): 털색을 바꾸고 모자를 얹은 기본 그림을 웜뱃 좌표(모자 줄만큼 내림)로 조립
    folder, table = GRAY
    gray = {}
    for view, spec in WOMBAT.items():
        base, pad = clerk_base(view, table)
        path = os.path.join(HERE, f'clerk_{view}_base.png')
        save(base, path)
        gray[view] = animate(path, shifted(spec, pad, CLERK_EARS[view]), view, 'wombat', issues)
    sheets(folder, gray)
    sheet([gray['front'][0][0]], os.path.join(SHEETS, folder, folder + '.png'))
    made['gray'] = gray
    # 손님
    for animal, views in VISITORS.items():
        frames = {v: animate(os.path.join(HERE, f'{animal}_{v}.png'), spec, v, animal, issues) for v, spec in views.items()}
        sheets(FOLDERS[animal], frames)
        # 정지 그림(VisitorTable sprite)은 없는 종만 앞모습 숨쉬기 첫 프레임으로(옛 종의 그림은 그대로)
        still = os.path.join(SHEETS, FOLDERS[animal], FOLDERS[animal] + '.png')
        if not os.path.exists(still):
            sheet([frames['front'][0][0]], still, CELLS.get(FOLDERS[animal], CELL))
        made[animal] = frames
    print('checks:', issues or 'ok')
    if len(sys.argv) > 1:
        preview(made, sys.argv[1])


def preview(made, folder):
    """확인용 GIF(한 칸 = 6px). stand = 게임에서 서 있을 때(숨쉬기 두 번 → 딴짓 → 숨쉬기)"""
    os.makedirs(folder, exist_ok=True)

    def im(a):
        bg = Image.new('RGBA', (a.shape[1] * 6, a.shape[0] * 6), (175, 145, 120, 255))
        bg.alpha_composite(Image.fromarray(a, 'RGBA').resize(bg.size, Image.NEAREST))
        return bg.convert('RGB')

    for who, views in made.items():
        for view, (idle, walk, blink, fidget) in views.items():
            stand = idle * 2 + fidget + idle
            for name, frames, dur in (('idle', idle, IDLE_DUR), ('walk', walk, WALK_DUR), ('fidget', fidget, [TICK] * len(fidget)),
                                      ('stand', stand, IDLE_DUR * 2 + [TICK] * len(fidget) + IDLE_DUR)):
                ims = [im(f) for f in frames]
                ims[0].save(os.path.join(folder, f'{who}_{view}_{name}.gif'), save_all=True, append_images=ims[1:], duration=dur, loop=0)


if __name__ == '__main__':
    main()
