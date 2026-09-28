---
paths:
  - "ProjectTycoon/Assets/Scripts/UI/**"
  - "ProjectTycoon/Assets/Resources/UI/**"
  - "ProjectTycoon/Assets/Sprites/UI/**"
---

# UI 규칙

UI(팝업·버튼·HUD·말풍선)를 만들거나 고칠 때 따른다. 이 문서가 UI 규칙의 단일 원본이다(옛 `기획/UI-작업-규칙.md`·`UI-디자인-규칙.md`를 합침, 2026-09-28).

## 1. 원칙

1. **공용 조각이 먼저다.** 새 화면·부품은 2장의 공용 조각으로 만든다. 맞는 조각이 없을 때만 새로 뽑는다(스킬 `asset-codex`).
2. **한 역할에 한 조각.** 한 팝업 안에서 같은 역할의 조각 두 벌이 섞이면 버그다.
3. **조각에 특정 리소스를 박지 않는다.** 코인·아이콘·글자가 들어간 틀을 만들지 않는다. 틀은 틀만, 아이콘·글자는 자식으로 얹는다.
4. **이름은 역할로.** 공용 조각은 `btn_primary`·`bubble`처럼, 화면 접두사(`clerk_`·`nego_`)는 그 화면에만 있는 그림에만.
5. **새 조각은 하나씩.** 시안을 뽑아 게임에 끼운 캡처로 보여 주고, 확인받은 뒤 적용한다.
6. **한 글에 정보 하나.** 「·」「/」로 정보 둘을 한 텍스트에 잇지 않는다. 칸을 나눈다.
7. **UI 프리팹이 원본이다.** 값은 프리팹을 직접 고친다(`PrefabUtility.LoadPrefabContents` → 값 → `SaveAsPrefabAsset`). 크기·위치는 프리팹에서 읽는다.
8. **픽셀은 정수 배.** 스프라이트는 원본의 정수 배 크기로 놓는다(9-slice 가운데만 예외).
9. **가로로 늘어나는 둥근 조각은 끝 비율을 지킨다.** 9-slice 경계는 좌우만(둥근 끝 전체), 높이는 원본 칸 수의 정수 배, `pixelsPerUnitMultiplier` = 4 / 배율.
10. **폰트는 그대로.** 정자체를 굵히거나 그림자를 넣지 않는다(세 번 반려됨). 제목 14 · 굵은 11 · 보조 9.

## 2. 공용 조각

`Assets/Sprites/UI/`. 원본 스크립트는 `Sprites/UI/Source~/`.

| 역할 | 조각 | 비고 |
|---|---|---|
| 닫기 | `btn_close` | 76×76 |
| 주 버튼 | `btn_primary` (+ `_pressed`, `_disabled`) | Button 상태 스프라이트 세 장을 같이 쓴다 |
| 보조 버튼 | `btn_secondary` (+ `_pressed`, `_disabled`) | |
| 행동 버튼(원형) | `btn_act` | |
| 팝업 틀 | `panel` | 9-slice 8칸. `make_panel.py` |
| 카드·줄 | `row` | |
| 칩·탭·카드 | `chip` · `chip_selected` | 9-slice 6칸. `make_chip.py` |
| 알약 | `pill`(값·HUD·토스트) · `pill_tag`(이름표) | 원본 15칸 · 18칸. `make_pill.py` |
| 말풍선 | `bubble` + `bubble_tail` | 꼬리는 말하는 쪽으로 옮기고 오른쪽은 좌우 뒤집기. 월드도 같은 그림(복사본 `World/Shop/`) |
| 코인 | `World/coin` | UI·월드 공용, 40×44 |

- 말풍선 몸통·꼬리·글자는 늘 함께 켜고 끈다.
- 예외: 머리 위 이모지 말풍선 `World/Shop/bubble_sheet`(틀과 이모지가 한 칸)는 지금 그림을 쓴다.
- 화면 전용: 협상 `nego_scene`·`nego_bar`·`nego_bar_tick`·`nego_arrow`·`nego_ribbon`, 점원 `clerk_frame`·`clerk_frame_empty`·`clerk_badge_*`·`clerk_table`·`clerk_gauge*`.
- 새 공용 조각이 생기면 이 표를 고치고, 옛 조각은 쓰던 곳을 모두 바꾼 뒤 PNG·생성 스크립트 줄·`ui_slices.json` 항목을 지운다.

## 3. 단위와 임포트

| 항목 | 값 |
|---|---|
| 기준 해상도 | 1080×1920, CanvasScaler Scale With Screen Size, Match 0.5 |
| UI 픽셀 | 1 UI px = 화면 4px(캔버스 270×480 UI px) |
| 임포트 | `ui_slices.json` + 메뉴 `ZooTycoon/Bake/Import UI Sprites`. PPU 25(한 칸 4px), Point, 압축 없음, Full Rect. `ppu` 20인 조각은 한 칸 5px |
| 터치 최소 | 11 UI px(화면 44px) |

## 4. 팔레트

| 이름 | HEX | 쓰임 |
|---|---|---|
| 외곽선 | #342020 | 모든 부품 1칸 외곽선(월드와 같음) |
| 패널 | #F0E4D8 | 바탕 |
| 밝은 테두리 / 그늘 | #FBF4E6 / #D9C8B4 | 안쪽 위·왼쪽 / 아래·오른쪽 |
| 강조 | #D87848 | 주 버튼, 선택 테두리 |
| 코인 | #F2C14E | 코인 숫자 |
| 가능 / 부족 | #3E7A4C / #A64B3C | 비용 글자 |
| 글자 / 보조 | #2E2320 / #5C4C42 | 본문 / 상태·효과 |
| 딤 | #3A2422 45% | 팝업 뒤 |

부품 하나에 최대 5~7색, 그라데이션 없음.

## 5. 폰트와 모션

- 갈무리 비트맵 폰트(`Assets/Fonts/Galmuri`): 제목 Galmuri14 · 본문 Galmuri11-Bold · 보조 Galmuri9 · 말풍선 Galmuri11. TMP 글자 크기는 UI px × 4.
- 폰트 에셋은 **정적 아틀라스**다. 문구에 새 글자가 생기면 `FontAssetTests`가 실패하고, 메뉴 `ZooTycoon/Bake/Fonts`로 다시 굽는다. 폰트 에셋은 `git checkout`으로 되돌리지 않는다.
- 눌림은 공용 `PressScale`(0.95배, 가운데 기준). 시트 열기 0.15초, 닫기 0.10초. UI에 흔들림·입자·줌을 넣지 않는다.
