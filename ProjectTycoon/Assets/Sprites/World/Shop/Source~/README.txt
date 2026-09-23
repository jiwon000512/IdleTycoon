웜뱃(가게) 정면·뒷모습 원본 (2026-09-18)
- *_raw.png: Codex 생성 원본(위 30도 시점). 정면은 기존 웜뱃 → 정면 B → 위 30도 H, 뒷모습은 B → 뒷모습 E → 위 30도 J 순으로 참조해 뽑았다.
- wombat_front.png = make_pixel.py wombat_front_raw.png --px=2 --cellsw=42 뒤 허리 행(칸 16~29)만 양옆 한 칸씩 안으로 옮긴 손 편집본(사용자 가이드).
- wombat_back.png = make_pixel.py wombat_back_raw.png --px=2 --cellsw=42.
- 한 칸 = 2px, PPU 80, 스케일 1 = 가게 유닛 기본 크기(ShopBaker.k_UnitPpu).
- wombat_{front,back}_{1,2,3}.png: 숨쉬기 프레임 = make_breath_frames.py 결과(A안, 2026-09-18). 몸은 발 바로 위 한 줄을 빼 최대 1칸만 오르내리고, 귀가 한 박자 늦게 따라온다: 1 몸↓ 귀 제자리 · 2 몸·귀↓ · 3 몸 제자리 귀↓. 재생 0→1→2→3, 1.6초(2.4초에서 1.5배). (배 부풀리기·머리 2칸은 어색해서 버림)

펭귄 손님 (2026-09-23)
- penguin_front_raw.png → make_pixel.py --px=2 (37×36칸). penguin_back_raw_{a,b,c}.png는 앞모습을 참조로 뽑은 뒷모습 3장, --cells=36으로 맞춘 뒤 A를 고르고 앞모습 팔레트로 통일한 것이 penguin_back.png.
- 숨쉬기·걷기는 웜뱃·토끼와 같은 스크립트(ears 비움, low 27, foot 32). 시트 칸 폭은 60 → 80 → 88(여우 걷기 86px)로 올렸다(모든 종 시트를 다시 묶는다).

여우·고슴도치 손님 (2026-09-23)
- {fox,hedgehog}_front_raw.png: 굴을 결 참조로 준 시안(여우 A·고슴도치 B). make_pixel.py --px=2 --cells=45 / --cells=38.
- {fox,hedgehog}_back_raw_{a,b,c}.png: 앞모습을 참조로 뽑은 뒷모습 3장. 둘 다 B를 골라 앞모습 팔레트로 통일. 실루엣이 앞과 칸 단위로 같아 breath/walk 설정을 공유.
- 여우: low 33, foot 42, 발 (9,14)·(19,24), 귀 0~5행 (2,13)·(20,30), 걷기 갸웃은 tip 3. 고슴도치: low 22, foot 35, 발 (10,15)·(21,26), 귀 없음.

빵집 소품·배경 (2026-09-23)
- shop_raw/: Codex 원본 24장(항목별 a·b·c) + 고른 후처리본. 선택: 바닥 B·입구 A·진열대 A·오븐 A(+B는 굽기 속도 5단계부터 oven_2)·계산대 B·빵 A/B/A.
- make_arch.py: arch_a_px에서 구멍 덩어리만 골라 1.7배 + 테두리 3칸 → arch_hole.png. make_bubble.py: 말풍선·화남. make_shop_sections.py: 구역 배경 4장(둥근 방 벽). compose.py: 시안 목업.
- 계산대는 counter_b_th의 20~32열을 두 번 끼워 84칸. 모든 소품 PPU 80(ShopBaker.k_Ppu).
- fix_fox.py (2026-09-23): fox_{front,back}_unpadded.png → 왼쪽 9칸 여백(몸 중심 = 피벗), 뒷모습 꼬리를 3칸 안쪽 + 외곽선을 몸 위에 다시 그려 앞으로. 시트 칸 폭 88 → 104.
- fix_fox_tail.py: 뒷모습 꼬리를 베지어 곡선(허리 아래 가운데 → 오른쪽 위)으로 다시 그려 몸 위에 얹음. 1차 시도(fix_fox.py의 꼬리 옮기기)는 "아직 이상하다"로 반려.

옆모습 시안 (2026-09-23, 손님 동선 설계 v0.3 10장)
- rabbit_side_raw_{a,b,c}.png: rabbit_front_raw.png를 참조로 Codex가 뽑은 오른쪽 보는 옆모습 3장(A 완전 옆 · B 살짝 비스듬 · C 몸은 옆 얼굴은 정면). 프롬프트는 side_prompt_{a,b,c}.txt.
- rabbit_side_{a,b,c}.png = make_pixel.py --px=2 --cells=42 뒤 앞모습(rabbit_front.png) 5색 팔레트로 가장 가까운 색 맞춤. 비교 페이지 https://claude.ai/artifact/VBBmUmkPzaepNfdyeYL3sR (선택 대기).
- 게임은 지금 옆모습 자리에 앞모습 사본(더미)을 쓴다: Resources/Sprites/Visitors/<종>/<종>_SideIdle·_SideMove, Sprites/World/Shop/wombat_side*. 고른 결로 5종을 만들어 같은 이름에 덮어쓴다.

