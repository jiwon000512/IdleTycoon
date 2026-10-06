# 곳마다 다른 점원 머리쓰개(2026-10-06 사용자 「웜뱃 점원들 리소스: 농부 · 낚시장 · 횟집」, 곳마다 시안 셋 중 선택):
#   농장 A 「밀짚모자」 · 낚시터 A 「벙거지 + 찌」 · 횟집 C 「남색 두건」. 빵집은 요리사 모자(make_clerk.py) 그대로.
#   원본 shop_raw/clerk_hat_<곳>.png: 회색 점원 웜뱃 앞 · 옆 · 뒤를 한 칸 10px로 그려 넣은 바탕(shop_raw/clerk_hat_template.png) 위에
#   머리쓰개만 그리게 한 한 장(프롬프트 shop_raw/clerk_hat_prompt.txt). Codex가 몸은 칸 그대로 두어, 원본 격자 그대로 옮기면 몸이 원래 그림과 같다.
#   1. 방향마다 따로 잘라 그 그림의 격자로 옮긴다(snap_codex raw, 옆모습은 바탕에서 반 칸 어긋나 있다)
#   2. 원래 회색 웜뱃과 몸 아래 절반이 겹치는 자리로 맞추고, 원래 그림과 다른 칸 중 머리에서 이어진 덩어리만 머리쓰개로 가져온다(몸 · 얼굴은 원래 칸 그대로)
#   3. 바깥 가장자리는 외곽선 색으로(make_anim의 close_outline이 프레임마다 가장자리를 외곽선으로 닫으므로 정지 그림도 같아야 깜빡이지 않는다),
#      흰 바탕이 번진 밝은 가장자리 칸은 뺀다. 머리쓰개 안쪽 색은 원본 그대로
#   4. 챙이 몸보다 넓으면 좌우를 같은 칸 수만큼 넓힌다(발 가운데 = 시트 칸 가운데)
# make_anim.py가 build()로 바로 만들어 쓴다(늘린 줄 · 열만큼 WOMBAT 좌표를 옮긴다). 이 파일을 직접 돌리면 확인용 clerk_<곳>_<방향>_base.png를 쓴다
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'Source~'))
import snap_codex  # noqa: E402

INK = (36, 24, 24)
RAW = os.path.join(HERE, 'shop_raw')
# 곳 → (시트 폴더, 원본)
HATS = {'farm': 'WombatFarmer', 'fishing': 'WombatAngler', 'restaurant': 'WombatSushi'}
CROP = {'front': (0, 520), 'side': (520, 1010), 'back': (1010, 1536)}   # 바탕에서 방향마다 차지하는 가로 범위(px)


def gray_base(view, gray):
    a = np.asarray(Image.open(os.path.join(HERE, 'wombat_%s_base.png' % view)).convert('RGBA'))[::2, ::2].copy()
    for src, dst in gray.items():
        a[(a[..., :3] == src).all(-1) & (a[..., 3] > 0), :3] = dst
    return a


def draft(place, view):
    im = Image.open(os.path.join(RAW, 'clerk_hat_%s.png' % place)).convert('RGB')
    x0, x1 = CROP[view]
    tmp = os.path.join(HERE, '_hat_tmp.png')
    try:
        im.crop((x0, 0, x1, im.height)).save(tmp)
        return snap_codex.snap(tmp, cell=10, square=True, min_hole=400, raw=True)[0]
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


def align(base, d):
    """시안 d[y, x] ↔ 원래 그림 base[y - dy, x - dx]: 아래 절반의 불투명 칸이 가장 많이 겹치는 (dy, dx)"""
    hb, wb = base.shape[:2]
    hd, wd = d.shape[:2]
    op = base[hb // 2:, :, 3] > 0
    best = None
    for dy in range(hd - hb - 3, hd - hb + 4):
        for dx in range(-8, 9):
            got = np.zeros_like(op)
            ys = np.arange(hb // 2, hb) + dy
            xs = np.arange(wb) + dx
            oky, okx = (ys >= 0) & (ys < hd), (xs >= 0) & (xs < wd)
            got[np.ix_(oky, okx)] = d[np.ix_(ys[oky], xs[okx])][..., 3] > 0
            score = int((got == op).sum())
            if best is None or score > best[0]:
                best = (score, dy, dx)
    assert best[0] == op.size, '시안 몸이 원래 그림과 어긋난다(%d / %d칸)' % (best[0], op.size)
    return best[1], best[2]


def build(place, view, gray):
    """(칸 배열, 위로 늘린 줄 수, 좌우로 늘린 열 수)"""
    from make_dig import blobs   # make_dig가 make_anim을 불러와 맨 위에서 가져오면 서로 물린다
    base = gray_base(view, gray)
    d = draft(place, view)
    dy, dx = align(base, d)
    hb, wb = base.shape[:2]
    hd, wd = d.shape[:2]
    top = max(dy, 0)
    side = max(dx, wd - dx - wb, 0)
    H, W = hb + top, wb + 2 * side
    old = np.zeros((H, W, 4), np.uint8)
    old[top:, side:side + wb] = base
    new = np.zeros((H, W, 4), np.uint8)
    y0, x0 = top - dy, side - dx                      # 시안의 (0, 0)이 놓일 자리
    ys, xs = slice(max(y0, 0), min(y0 + hd, H)), slice(max(x0, 0), min(x0 + wd, W))
    new[ys, xs] = d[ys.start - y0:ys.stop - y0, xs.start - x0:xs.stop - x0]
    # 원래 그림과 다른 칸(알파가 다르거나 색 차이가 큼) 중, 머리(눈 줄 위)에서 이어진 덩어리 = 머리쓰개
    diff = ((old[..., 3] > 0) != (new[..., 3] > 0)) | ((old[..., 3] > 0) & (np.abs(old[..., :3].astype(int) - new[..., :3].astype(int)).sum(-1) > 40))
    head = top + hb * 2 // 5                          # 눈 줄 위쪽(원래 그림 높이의 2/5)
    hat = np.zeros((H, W), bool)
    for gr in blobs(diff, diag=True):
        if len(gr) >= 6 and min(y for y, _ in gr) < head:
            for y, x in gr:
                hat[y, x] = True
    out = old.copy()
    out[hat] = new[hat]
    # 가장자리: 흰 바탕이 번진 밝은 칸은 빼고(되풀이), 남은 가장자리는 외곽선 색으로
    for _ in range(3):
        op = out[..., 3] > 0
        pad = np.pad(op, 1)
        edge = op & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
        bleed = edge & hat & (out[..., :3].min(-1) > 200)
        if not bleed.any():
            break
        out[bleed] = 0
    op = out[..., 3] > 0
    pad = np.pad(op, 1)
    edge = op & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    out[edge & hat, :3] = INK
    # 외곽선 색과 거의 같은 어두운 칸(합 차이 16 안)은 외곽선 색으로: make_anim은 가장 어두운 색을 외곽선으로 보고 눈 · 가장자리를 그 색으로 찾는다
    #   (원본에는 (35,24,25) · (38,23,23)처럼 한두 단계 다른 외곽선 칸이 섞여 있어, 그대로 두면 기울 때 눈을 못 찾아 눈 모양이 깨졌다)
    dark = op & (out[..., :3].astype(int).sum(-1) <= sum(INK) + 16)
    out[dark, :3] = INK
    return out, top, side


if __name__ == '__main__':
    from make_anim import GRAY
    for place in HATS:
        for view in ('front', 'side', 'back'):
            a, top, side = build(place, view, GRAY[1])
            Image.fromarray(np.repeat(np.repeat(a, 2, 0), 2, 1)).save(os.path.join(HERE, 'clerk_%s_%s_base.png' % (place, view)))
            print('clerk_%s_%s_base' % (place, view), a.shape[1], 'x', a.shape[0], 'top', top, 'side', side)
