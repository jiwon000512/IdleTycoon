웜뱃(가게) 정면·뒷모습 원본 (2026-09-18)
- *_raw.png: Codex 생성 원본(위 30도 시점). 정면은 기존 웜뱃 → 정면 B → 위 30도 H, 뒷모습은 B → 뒷모습 E → 위 30도 J 순으로 참조해 뽑았다.
- wombat_front.png = make_pixel.py wombat_front_raw.png --px=2 --cellsw=42 뒤 허리 행(칸 16~29)만 양옆 한 칸씩 안으로 옮긴 손 편집본(사용자 가이드).
- wombat_back.png = make_pixel.py wombat_back_raw.png --px=2 --cellsw=42.
- 한 칸 = 2px, PPU 80, 스케일 1 = 가게 유닛 기본 크기(ShopBaker.k_UnitPpu).
- wombat_{front,back}_{1,2,3}.png: 숨쉬기 프레임 = make_breath_frames.py 결과(A안, 2026-09-18). 몸은 발 바로 위 한 줄을 빼 최대 1칸만 오르내리고, 귀가 한 박자 늦게 따라온다: 1 몸↓ 귀 제자리 · 2 몸·귀↓ · 3 몸 제자리 귀↓. 재생 0→1→2→3, 1.6초(2.4초에서 1.5배). (배 부풀리기·머리 2칸은 어색해서 버림)
