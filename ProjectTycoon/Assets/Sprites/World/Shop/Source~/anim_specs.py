# -*- coding: utf-8 -*-
# 부위 조립 애니메이션의 부위 좌표(원본 정지 그림의 칸, 행·열). 동물을 더하면 방향마다 한 벌씩 더한다
#   feet: 떼어 낼 발(rows·cols, z 1 = 몸 앞, 0 = 몸 뒤). far = 가까운 발을 어둡게 칠한 먼 발(옆모습)
#         leg: 몸이 떴을 때 발 위에 이을 줄. 기본 발 윗줄, 'above' = 발 바로 위 몸 줄(발 윗줄이 발바닥 색일 때)
#   ear · ears: 귀가 끝나는 행과 귀 열 범위(귀만 한 박자 늦게) · stretch: 늘어날 때 겹칠 행(위아래 폭이 같은 행)
#   paw · tail: 한 박자 늦게 오르내릴 몸 안쪽 블록(r0, r1, c0, c1). 몸 윤곽에 닿는 블록은 외곽선이 두 겹이 되어 쓰지 않는다
#   eyes: 기울일 때 떼었다 찍을(두리번 때 옮길) 눈 상자 · blink: 눈 감기(칸별 '.' = 털, 'A' = 외곽선) · blink_fill: 털 색을 가져올 칸
#   parts: 딴짓에서 흔들 부위. rect 사각형(몸과 선 없이 이어진 귀 · 날개는 여기서 자른다) · seed(외곽선으로 갈린 꼬리: 이 칸에서 닿는 칸만)
#          axis 안 움직이는 행(칸 경계, 부위가 몸에 붙은 곳) · z 0 = 몸 뒤, 1 = 몸 앞
WOMBAT = {
    'front': {
        'feet': [
            {'rows': (38, 40), 'cols': (9, 15)},
            {'rows': (38, 40), 'cols': (26, 32)},
        ],
        'ear': 4, 'ears': [(5, 13), (28, 36)],
        'stretch': 14,
        'paw': (31, 36, 15, 26),
        'eyes': [(17, 19, 12, 13), (17, 19, 28, 29)],
        'blink': [(17, 12, '.'), (17, 13, '.'), (18, 12, '.'), (18, 13, '.'), (19, 12, 'A'), (19, 13, 'A'),
                  (17, 28, '.'), (17, 29, '.'), (18, 28, '.'), (18, 29, '.'), (19, 28, 'A'), (19, 29, 'A')],
        'blink_fill': (15, 20),
    },
    'back': {
        'feet': [
            {'rows': (42, 43), 'cols': (9, 14), 'leg': 'above'},
            {'rows': (42, 43), 'cols': (27, 32), 'leg': 'above'},
        ],
        'ear': 5, 'ears': [(4, 12), (29, 37)],
        'stretch': 16,
        'tail': (33, 37, 17, 24),
    },
    'side': {
        'feet': [
            {'rows': (38, 40), 'cols': (11, 20)},
            {'far': True, 'z': 0, 'from': 0, 'dy': -1, 'dx': 4,
             'recolor': [((156, 120, 108), (132, 96, 84))], 'claw': (1, 6, (60, 60, 60))},
        ],
        'clear_rows': [38, 39, 40],
        'ear': 5, 'ears': [(15, 28)],
        'stretch': 17,
        'eyes': [(19, 20, 27, 28)],
        'blink': [(19, 27, '.'), (19, 28, '.'), (20, 27, 'A'), (20, 28, 'A')],
        'blink_fill': (17, 20),
    },
}


# 손님(2026-09-29): 발·귀 좌표는 옛 make_walk_frames.py 값(발 시작 행 ~ 맨 아래, 왼·오른발 열). 눈·깜빡임·늘어날 줄은 자동(auto_eyes = 찾을 눈 수)
def _visitor(foot, h, left, right, ear=0, ears=(), eyes=0, eye_boxes=None, blink=True, stretch=None, parts=None):
    s = {'feet': [{'rows': (foot, h - 1), 'cols': left}, {'rows': (foot, h - 1), 'cols': right}],
         'ear': ear, 'ears': list(ears), 'parts': parts or {},
         'stamp_features': True}   # 기울일 때 눈 · 코 · 입 · 앞발 선을 모양 그대로 옮긴다
    if stretch is not None:
        s['stretch'] = stretch   # 자동은 눈·입이 없는 줄을 고르지만 가시·귀 사이 선 때문에 못 고르는 종은 손으로
    if eye_boxes:
        # 자동으로 못 찾는 눈(가시·귀 안쪽 외곽선과 헷갈림)은 손으로. blink=False면 깜빡임 없음(여우는 눈이 이미 웃는 모양)
        s['eyes'] = eye_boxes
        if blink:
            s['blink'] = [(r, c, 'A' if r == r1 else '.') for r0, r1, c0, c1 in eye_boxes for r in range(r0, r1 + 1) for c in range(c0, c1 + 1)]
            s['blink_fill'] = (eye_boxes[0][0] - 1, eye_boxes[0][2])
    elif eyes:
        s['auto_eyes'] = eyes
    return s


# 딴짓 부위(2026-09-29): 토끼 귀(귀 밑줄에서 자른다) · 펭귄 날개(어깨 아래 바깥 열) · 여우 꼬리(외곽선 안쪽을 seed로)
VISITORS = {
    'rabbit': {
        'front': _visitor(39, 42, (7, 12), (15, 20), 12, [(2, 12), (15, 25)], eyes=2, stretch=20, parts={
            'ear_l': {'rect': (0, 12, 2, 12), 'axis': 13},
            'ear_r': {'rect': (0, 12, 15, 25), 'axis': 13}}),
        'back': _visitor(36, 39, (7, 12), (15, 20), 11, [(3, 12), (15, 24)], stretch=20, parts={
            'ear_l': {'rect': (0, 11, 3, 12), 'axis': 12},
            'ear_r': {'rect': (0, 11, 15, 24), 'axis': 12}}),
        'side': _visitor(39, 42, (8, 15), (16, 19), 14, [(7, 20)], eyes=1, stretch=20, parts={
            'ears': {'rect': (0, 14, 7, 20), 'axis': 15}}),
    },
    'penguin': {
        'front': _visitor(32, 36, (10, 15), (21, 26), eyes=2, parts={
            'fl_l': {'rect': (16, 25, 0, 5), 'axis': 17, 'z': 1},
            'fl_r': {'rect': (16, 25, 31, 36), 'axis': 17, 'z': 1}}),
        'back': _visitor(32, 36, (10, 15), (21, 26), parts={
            'fl_l': {'rect': (17, 25, 0, 5), 'axis': 18, 'z': 1},
            'fl_r': {'rect': (17, 25, 31, 36), 'axis': 18, 'z': 1}}),
        'side': _visitor(33, 36, (11, 16), (17, 23), eyes=1),
    },
    'fox': {
        'front': _visitor(42, 45, (18, 23), (28, 33), 5, [(11, 22), (29, 39)], eye_boxes=[(16, 18, 19, 21), (16, 18, 30, 32)], parts={
            'tail': {'rect': (17, 39, 38, 49), 'seed': (27, 45), 'axis': 39}}),
        'back': _visitor(42, 45, (18, 23), (28, 33), 5, [(11, 22), (29, 39)], parts={
            'tail': {'rect': (17, 33, 33, 49), 'seed': (25, 44), 'axis': 34}}),
        'side': _visitor(41, 45, (17, 22), (23, 29), 9, [(16, 31)], eye_boxes=[(17, 19, 28, 30)], parts={
            'tail': {'rect': (16, 26, 0, 14), 'seed': (20, 5), 'axis': 27}}),
    },
    # 설계 31 유물 행상 너구리(make_tanuki.py, 발 가운데 피벗으로 여백을 붙인 좌표). 꼬리: 앞은 외곽선 안쪽 seed, 옆은 몸과 선 없이 붙어 사각형.
    #   앞발 흔들기 paw_r: 가슴 앞 오른 앞발(윗줄이 더 움직이고 밑줄 axis는 그대로)
    'tanuki': {
        'front': _visitor(42, 45, (17, 24), (29, 36), 7, [(11, 21), (33, 44)], eyes=2, parts={
            'tail': {'rect': (22, 39, 44, 53), 'seed': (28, 49), 'axis': 39},
            'paw_r': {'rect': (26, 30, 29, 34), 'axis': 31, 'z': 1}}),
        'back': _visitor(42, 45, (9, 15), (24, 30), 7, [(4, 15), (26, 36)], parts={
            'ear_l': {'rect': (0, 7, 4, 15), 'axis': 8},
            'ear_r': {'rect': (0, 7, 26, 36), 'axis': 8}}),
        'side': dict(_visitor(42, 45, (14, 21), (22, 27), 8, [(11, 30)], eye_boxes=[(16, 17, 28, 28)], parts={
            'tail': {'rect': (26, 38, 0, 8), 'axis': 39}}), blink_fill=(15, 29)),   # 눈가 짙은 칸 말고 탈 색으로 감는다
    },
    'hedgehog': {
        'front': _visitor(35, 38, (10, 15), (21, 26), eye_boxes=[(16, 17, 13, 13), (16, 17, 23, 23)], stretch=14),
        'back': _visitor(35, 38, (10, 15), (21, 26), stretch=18),
        'side': _visitor(35, 38, (12, 21), (22, 25), eye_boxes=[(16, 17, 26, 26)], stretch=12),
    },
}
