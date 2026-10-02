# 설계 40 별 평가 평가단장 부엉이(2026-10-02 사용자: 종 부엉이 → 디자인 A 「안경 평론가」 → 턴어라운드 셋 중 「다시 뽑기」 → 「2에서 옆모습 좀 더 통통」 → 옆 1).
#   프롬프트 shop_raw/owl_prompt.txt. 앞 · 뒤: shop_raw/owl_sheet_raw_2.png(여우 완성 시트를 크기 기준으로 준 턴어라운드),
#   옆: shop_raw/owl_side_raw_1.png 가운데(앞 | 빈칸 | 뒤를 한 칸 10px로 준 owl_side_sheet_in.png를 채운 그림).
#   너구리(make_tanuki.py)와 같은 방식: 원본 격자 그대로, 발 가운데가 그림 가운데(피벗)가 되게 여백.
#   다만 턴어라운드 2번은 그림마다 찾은 주기가 7.9 · 10.3 · 8.8px로 갈려 중앙값을 쓰면 앞모습이 46 → 42칸으로 줄었다.
#   앞모습 자기 주기(약 8px)로 앞 · 뒤를 옮기면 46×48 · 47×47칸이고, 옆 시트(세 그림 모두 11px)의 옆 42×47칸과 키가 맞는다.
# 출력: owl_front.png · owl_back.png · owl_side.png(한 칸 2px). 다음: make_anim.py(anim_specs.py 'owl')
import os
import numpy as np
from PIL import Image
from make_tanuki import RAW, MIN_HOLE, ink_columns, crop, snap_fixed, centered, save, snap_codex

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    tmp = os.path.join(HERE, '_owl_tmp.png')
    try:
        sheet = Image.open(os.path.join(RAW, 'owl_sheet_raw_2.png')).convert('RGB')
        runs = ink_columns(sheet)
        assert len(runs) == 3, runs
        for name, run in (('front', runs[0]), ('back', runs[2])):
            save(centered(snap_fixed(crop(sheet, run, tmp), 8.0, 0.02)), 'owl_' + name)
        side = Image.open(os.path.join(RAW, 'owl_side_raw_1.png')).convert('RGB')
        runs = ink_columns(side)
        assert len(runs) == 3, runs
        period = float(np.median([snap_codex.snap(crop(side, r, tmp), 12, square=True, min_hole=MIN_HOLE)[1][0] for r in runs]))
        save(centered(snap_fixed(crop(side, runs[1], tmp), period, 0.03)), 'owl_side')
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


if __name__ == '__main__':
    main()
