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
- shop_raw/: Codex 원본 24장(항목별 a·b·c) + 고른 후처리본. 선택: 바닥 B·입구 A·진열대 A·오븐 A(+B는 굽기 속도 5단계부터 oven_2)·계산대 B·빵 A/B/A. (진열대·오븐·계산대는 2026-09-29 C 「웜뱃 굴 공방」으로 바꾸며 원본을 지웠다, 아래)
- make_bubble.py: 말풍선·화남. make_shop_sections.py: 구역 배경 4장(둥근 방 벽). compose.py: 시안 목업.
- 모든 소품 PPU 80(BakeryBaker.k_Ppu).
- fix_fox.py (2026-09-23): fox_{front,back}_unpadded.png → 왼쪽 9칸 여백(몸 중심 = 피벗), 뒷모습 꼬리를 3칸 안쪽 + 외곽선을 몸 위에 다시 그려 앞으로. 시트 칸 폭 88 → 104.
- fix_fox_tail.py: 뒷모습 꼬리를 베지어 곡선(허리 아래 가운데 → 오른쪽 위)으로 다시 그려 몸 위에 얹음. 1차 시도(fix_fox.py의 꼬리 옮기기)는 "아직 이상하다"로 반려.

옆모습 시안 (2026-09-23, 손님 동선 설계 v0.3 10장)
- rabbit_side_raw_{a,b,c}.png: rabbit_front_raw.png를 참조로 Codex가 뽑은 오른쪽 보는 옆모습 3장(A 완전 옆 · B 살짝 비스듬 · C 몸은 옆 얼굴은 정면). 프롬프트는 side_prompt_{a,b,c}.txt.
- rabbit_side_{a,b,c}.png = make_pixel.py --px=2 --cells=42 뒤 앞모습(rabbit_front.png) 5색 팔레트로 가장 가까운 색 맞춤.
- 게임은 지금 옆모습 자리에 앞모습 사본(더미)을 쓴다: Resources/Sprites/Visitors/<종>/<종>_SideIdle·_SideMove, Sprites/World/Shop/wombat_side*. 고른 결로 5종을 만들어 같은 이름에 덮어쓴다.
- 2차(2026-09-23, 사용자: 「A인데 두 발로 걸어다니게. 앞모습은 앞발을 들고 있는데 옆모습 A는 네 발로 서 있는 듯」): rabbit_side2_raw_{a,b,c}.png = 앞모습 원본 + 1차 A 두 장을 참조로 뽑은 두 발 서기(A2 앞발 모으기 · B2 고개 들고 앞발 앞으로 · C2 한 발 내디딘 걸음). 프롬프트 side2_prompt_{a,b,c}.txt.
  rabbit_side2_{a,b,c}.png = make_pixel.py --px=2 --cells=42 + 앞모습 팔레트. 숨쉬기·걷기 미리보기는 preview_side_frames.py(칸 지도를 보고 정한 SPEC을 인자로. A2: low 37 foot 39 발 (5,10)·(11,14) 귀 0~12행 (1,16)).
- 3차(2026-09-23, 사용자: 「앞·뒷모습은 위 30도인데 옆모습은 정확히 옆에서 만든 느낌이라 비율이 안 맞아」): 2차 프롬프트의 "body taller than wide · round head on a round body"가 눈높이 비율(폭 22칸, 눈 20줄)을 만들었다. 공통 카메라 문단(한 덩어리·낮은 얼굴·짧은 몸, 목·긴 몸통·작은 머리 금지)을 넣고 방식만 셋으로:
  A 턴어라운드 = side3_sheet_in.png(앞·뒷모습 격자 ×6을 같은 바닥선에 놓고 가운데를 비운 시트)를 채우게 한 뒤 가운데 캐릭터만 잘라 rabbit_side3_raw_a.png · B 앞·뒤 원본 참조 + 카메라 설명 · C 앞모습만 주고 턴테이블로 90도. 프롬프트 side3_prompt_{a,b,c}.txt.
  rabbit_side3_{a,b,c}.png = make_pixel.py --px=2 --cells=42 + 앞모습 팔레트(셋 다 28×42칸, 눈 24~25줄로 앞모습과 같음). A·C는 눈만 2×2 점으로 칸 편집(A (22,25) 추가, C (21,24)(20,25)(20,26) 지움).
  미리보기 SPEC: A low 32 foot 39 발 (8,15)·(16,19) 귀 0~14행 (7,20) / B low 31 foot 39 발 (10,16)·(17,20) 귀 0~15행 (5,21) / C low 35 foot 39 발 (9,15)·(16,21) 귀 0~15행 (7,22).
- 선택(2026-09-23): 3차 A. 사용자 「입을 좀 더 짧게, 너무 길게 나왔어」 → 턱 밑 입선 (20,28)(20,29)(21,29)를 볼 색으로 지워 2칸만 남김. 「볼 도트가 ㅜ처럼」 → 볼을 앞모습처럼 위 2칸(19~20,27)·아래 3칸(18~20,28)으로. 이것이 rabbit_side.png(= rabbit_side3_a.png).
  rabbit_side_{1,2,3}.png·rabbit_side_walk_{0..3}.png = make_breath_frames.py·make_walk_frames.py의 rabbit_side 줄(low 32, foot 39, 발 (8,15)·(16,19), 귀 0~14행 (7,20), 걷기 갸웃은 tip 6). make_visitor_sheets.py가 _side.png가 있는 종만 Side 시트를 묶는다(나머지 종은 더미 유지).
- 펭귄·여우·고슴도치·웜뱃 옆모습 시안(2026-09-23): 토끼 3차 A 방식. side_sheet_in_<종>.png(앞 | 빈칸 | 뒤, 한 칸 12px) + side_sheet_ref_rabbit.png(완성 토끼 앞·옆·뒤, 카메라 예시)를 참조로 같은 프롬프트(side_prompt_<종>.txt)를 3번 돌림.
  <종>_side_raw_{a,b,c}.png = 시트 가운데 자름, <종>_side_{a,b,c}.png = make_pixel.py --px=2 --cells=앞모습 키(펭귄 36·여우 45·고슴도치 38·웜뱃 41) + 그 종 앞모습 팔레트.
- 선택(2026-09-23, 사용자 「여우만 B, 나머지는 추천대로」): 펭귄 A·여우 B·고슴도치 A·웜뱃 B. 발 가운데 = 그림 가운데가 되게 투명 여백(펭귄·고슴도치 오른쪽 3칸, 여우 오른쪽 10칸, 웜뱃 왼쪽 2칸)을 붙여 <종>_side.png, 웜뱃은 ../wombat_side.png(더미 덮어씀).
  숨쉬기·걷기는 두 스크립트의 <종>_side 줄. 펭귄 low 27 foot 33 · 여우 low 33 foot 41 귀 0~9행 tip 3 · 고슴도치 low 26 foot 35 · 웜뱃 low 31 foot 38 귀 0~5행.

오븐 굽기 표시 (2026-09-23)
- make_bar.py: 칸 편집 시안 3개(A 빵 배지 캡슐 40×12 · B 원형 타이머 20×20 · C 구워지는 빵 28×18칸), 진행 16단계 프레임 가로 시트.
- 선택: B(사용자). oven_timer.png = make_bar.py의 bar_b(20×20칸 × 16프레임, 한 칸 2px). 12시부터 시계 방향으로 주황 부채꼴이 찬다. 게임 반영: 프레임 16장을 ../oven_timer_00~15.png로 잘라 넣고 OvenView가 진행에 맞는 프레임을 고른다(ShopBaker가 Timer 조립). 옛 bar_bg·bar_fill 삭제.

오븐 상태 표시 (2026-09-23)
- make_oven_idle.py: 칸 편집 시안 3개(A 빈 타이머 + 식빵 · B 아래 화살표 · C 아궁이 속 점선 식빵).
- 선택(사용자 「b를 빈 오븐에 쓰고 A를 다 된 빵을 알려주는걸로 완성된 오븐에」): ../oven_empty_mark.png = B, ../oven_ready_mark.png = A. 둘 다 타이머 자리, 가운데 피벗, PPU 80. C는 버림.

식빵 정면 (2026-09-23) — 2026-09-29 새 시점(앞쪽 위에서 내려다본 평행 투영)으로 바꾸며 원본을 지웠다(아래 빵집 소품 C)
- 사용자: 「식빵 리소스 변경, 동물들과 같이 정면 살짝 위에서 바라본 느낌으로」. 결 참조 = 옛 b01, 카메라 참조 = oven.png(정면·위 30도).
- shop_raw/bread_front_raw_{a,b,c}.png: Codex 3장(A 자른 단면 정면 · B 긴 옆면 한 봉우리 · C 산형 세 봉우리). 프롬프트 bread_front_prompt_{a,b,c}.txt.
- 선택 A: make_pixel.py --px=2 --cellsw=26 (26×25칸, 8색) → shop_raw/bread_front_a_px.png = Resources/Sprites/Shop/Breads/b01.png. 폭 26칸은 옛 b01과 같게(머리 위 층 간격이 빵 크기에 맞춰져 있음).

굴 환경 A2 「벽 높이」 (2026-09-24)
- 콘셉트: 게임 캡처를 참조로 Codex가 환경만 다시 그린 A 벽 높이 · B 결만 · C 아늑한 가게 내부 중 A(C는 뒤에 내부 업그레이드로). 윗벽 지층은 곧은 가로줄, 방 모서리·턱은 둥글게, 둥근 건 입구 구멍만(수정 2). 턱 4칸.
- 재료 품질 시안 3벌: A 스크립트(바닥 알갱이 무늬 옮김) · B·C Codex 재료 시트(shop_raw/burrow_mat_{b,c}.png, 프롬프트 burrow_mat_prompt_{b,c}.txt). 선택: 입구 B, 지층 띠·바깥 흙 C.
- make_burrow_a.py: 시트에서 세 덩어리를 잘라 make_pixel.py로 칸 격자(띠 세로 40칸, 흙 32칸, 입구 가로 60칸, --th=0.001) → 띠는 이음이 가장 매끄러운 64칸 구간 + 맨 아래 그늘 한 줄 → 드문 색을 합쳐 7색 → ../wall_face.png(64×40) · ../wall_tile.png(32×32) · ../arch.png(60×49칸 × 2px). 방 둘레 턱 색은 입구 턱 (168,120,96)에 맞춰 BurrowPainter.k_Ledge.
- make_arch.py·shop_raw/arch_hole.png(옛 구멍 입구)는 삭제. make_burrow_tiles.py는 floor_tile·slot_empty만 만든다.

캐릭터 애니메이션 B3 (2026-09-29)
- 숨쉬기·걷기·눈 깜빡임은 make_anim.py 하나가 만든다(부위 조립: anim_parts.py, 부위 좌표 anim_specs.py). 정지 그림 <종>_<방향>.png(웜뱃은 wombat_<방향>_base.png)이 원본이다.
- 옛 make_breath_frames.py · make_walk_frames.py · make_visitor_sheets.py · make_wombat_palette.py · preview_side_frames.py와 그 결과 프레임(_1~3, _walk_0~3)은 지웠다(git 이력에 있다). 위 기록의 low·foot·귀 값은 anim_specs.py로 옮겼다.

숨쉬기 · 딴짓 (2026-09-29)
- 사용자 「숨쉬기 깜빡임에서 모든 동물이 상반신 내지 머리만 움직임. 좀 다채롭게」. 숨쉬기 4는 늘어날 줄 겹치기 대신 발 위 몸 전체가 1칸 내려앉았다 올라온다(귀 · 꼬리 늦게). 게임 m_idleBob이 든 빵을 같이 내린다.
- 딴짓은 make_anim.py FIDGETS(동물 · 방향마다 한 벌, 한 칸 TICK 80ms, 머묾은 같은 프레임 되풀이): 웜뱃 두리번 + 앞발 비비기 / 엉덩이 흔들기 / 발 구르기 + 올려다보기, 토끼 귀 쫑긋 + 발 구르기, 펭귄 뒤뚱 + 날개 파닥 / 킁킁, 여우 꼬리 살랑 + 두리번, 고슴도치 부르르 + 두리번 / 킁킁.
  결과: 웜뱃 ../wombat_<방향>_fidget.png(시트), 손님 <종>_{,Back,Side}Fidget.png. 게임이 서 있을 때 몇 초마다 숨쉬기 한 바퀴 끝에 한 번 끼워 넣는다.
- 부위 흔들기(anim_specs parts): 귀 · 날개는 사각형으로, 여우 꼬리는 외곽선 안쪽 seed로 떼어 줄마다 axis 행에서 먼 만큼 가로로 민다. 처음엔 돌리기(8×8 다수결)였으나 가는 선이 끊기고 몸과 같이 쓰던 외곽선이 떨어진 조각으로 남아 바꿨다. 여우 앞모습 꼬리는 칸 폭 104px 때문에 바깥으로 1칸만.

빵집 소품 C 「웜뱃 굴 공방」 (2026-09-29)
- 사용자 「오븐, 진열대, 계산대 이미지를 개선해보자」. 시안 셋(A 다듬기 · B 아기자기 빵집 · C 웜뱃 굴 공방)을 벌마다 한 장 시트(오븐 1·2단 · 진열대 · 계산대)로 Codex에 뽑아 게임에 끼워 비교 → 선택 C(강돌 · 흙 · 뿌리 · 통나무).
- 사용자 「진열대가 원근감 있게 표현된다(식빵 포함)」 → 앞에서 본 모습으로 판 윗면을 줄여 봤으나 반려: 「모든 리소스는 대각선 위에서 내려다본 뷰」. 이것이 art.md 「시점」(앞쪽 위에서 비스듬히 내려다본 평행 투영)이 됐다.
- 각도 30 · 45 · 60도로 다시 뽑아도(shop_raw/props_c45_sheet.png) Codex는 판을 사다리꼴로 그리고 기둥을 벌렸다. 계산대 통나무는 칸 무늬로 직접 그렸다(켠 윗면, 끝 나이테 면 없음).
- 진열대는 칸 무늬로 그린 판 · 기둥 안이 거듭 반려(다리 둘뿐 · 앞다리가 판을 뚫는 듯 · 뒤판이 좁고 꼬임) → 「새 디자인을 셋씩 뽑아 고른다」. 시점 블록아웃 그림(shop_raw/view_blockout.png, make_view_blockout.py: 윗면 · 앞면만 보이고 뒷다리가 안쪽 · 위인 탁자와 상자)을 -i 참조로 주자 Codex가 원근 없이 그렸다.
  1 뿌리 다리 2단 판 · 2 강돌 기둥 계단 판(2-1 두꺼운 판 · 2-2 앞 턱과 옆 난간 · 2-3 뒤판까지 쟁반) · 3 나무 빵 상자 둘 · 3-1 상자 하나 → 선택 3-1.
  shop_raw/shelf_crate_raw.png(프롬프트 shelf_prompt_crate.txt) → make_shop_props.py crate_shelf(): 상자 둘의 가운데 칸막이를 지우고 상자 가운데 결을 이어 한 상자로(빵 아이콘이 하나라 두 상자 사이에 걸쳤다).
- make_shop_props.py: Codex 그림은 자르기 → make_pixel.py(가로 칸 수 = 옛 그림: 오븐 60 · 진열대 55 · 계산대 84 · 식빵 20) → 색 줄이기(가중 k-평균) → 갇힌 흰 바탕 지우기(판 사이 · 다리 사이 틈, 둘레가 어두운 흰 덩어리) → 끊긴 외곽선 메우기 → 외곽선 (48,24,24) · 외톨이 칸 정리.
  오븐 1·2단 = 처음 C 시트(shop_raw/props_c_sheet.png), 식빵 · 계산대 돌 받침 = 45도 시트. 식빵은 「너무 크다」로 가로 20칸.
- 사용자 「인게임에서 진열대 높이가 높다」 → 받침대 다리를 7줄 줄였다(앞 · 뒷다리 함께 있는 곳 3줄 + 앞다리만 있는 곳 4줄, 진열대 55×33칸).
- 빵 아이콘은 밑이 빵 상자 바닥(천 위)에 앉는다(ShelfView.m_seat 0.575, 빵마다 키가 달라서). 영수증 출력기는 통나무 윗면 앞쪽(계산대 밑에서 16칸, 가운데 0.75). 오븐 불빛은 make_oven_fx.py가 새 몸통의 아궁이(상자 안 외곽선 색 칸)에서 다시 그린다. 굴뚝 · 타이머 높이는 옛 오븐과 같다.
- 옛 소품 원본(shop_raw/oven_* · shelf_* · counter_*, 불 그린 원본 oven_lit · oven_2_lit)과 옛 식빵 원본(shop_raw/bread_front_*, bread_front_prompt_*)은 지웠다.

웜뱃 똥 더미 (2026-09-29, 설계 24)
- make_poop.py: 앞쪽 위에서 내려다본 네모 똥(10×9칸, 윗면 3줄 · 앞면 4줄) + 냄새 김 2프레임 → ../poop_0.png · poop_1.png(피벗 아래 가운데, PPU 80). 실제 그림은 시안 셋에서 고른다.
- make_bubble_sheet.py: 10번째 칸 yuck(똥 + 냄새 김, 두 번째 색 '+'), Resources/Sprites/Actions/clean.png(세운 빗자루. 기운 자루는 「금지」 표시로 읽혀 세웠다).
