# -*- coding: utf-8 -*-
# 칸 문자 덤프: 정지 그림을 색마다 글자 하나로 찍는다(# = 외곽선, 나머지는 어두운 순 A~, . = 빈 칸). 부위 좌표(anim_specs.py) 잡기용
# 사용: python dump_cells.py rabbit_front [wombat_side_base ...]   (Source~ 안의 이름, .png 빼고)
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from anim_parts import load, opaque

HERE = os.path.dirname(os.path.abspath(__file__))

for name in sys.argv[1:]:
    a = load(os.path.join(HERE, name + '.png'))
    pal = sorted({tuple(int(v) for v in c) for c in a[opaque(a)][:, :3]}, key=sum)
    ch = {c: '#' if i == 0 else chr(ord('A') + i - 1) for i, c in enumerate(pal)}
    print(name, a.shape[:2], ' '.join(f'{v}={k}' for k, v in ch.items()))
    print('    ' + ''.join(str(x // 10) for x in range(a.shape[1])))
    print('    ' + ''.join(str(x % 10) for x in range(a.shape[1])))
    for y in range(a.shape[0]):
        print(f'{y:3d} ' + ''.join(ch[tuple(int(v) for v in a[y, x, :3])] if a[y, x, 3] else '.' for x in range(a.shape[1])))
