---
name: asset-codex
description: 그림 리소스(캐릭터·캐릭터 동작·소품·배경·UI 조각·화면 시안)를 새로 만들거나 고칠 때 읽는다 — Codex CLI로 시안 생성, 칸 단위 픽셀 변환, 게임에 끼워 캡처, 비교 페이지, 적용과 옛 조각 정리, 새 동물 · 새 동작(부위 조립)까지의 절차.
---

# 그림 리소스 만들기 (Codex CLI)

규칙은 `.claude/rules/art.md`(스타일·검수)와 `.claude/rules/ui.md`(UI 조각). 여기는 절차와 요령이다.

## 순서

1. **있는 것부터 찾는다.** UI면 `ui.md` 2장 공용 조각, 월드면 `Assets/Sprites/World/`.
2. **시안 3장**을 Codex로 병렬 생성(1분쯤). 방향이 다른 A·B·C.
3. **칸 단위로 줄인다**(아래 「격자」).
4. **게임에 끼워 캡처한다.** 에셋을 바꾸지 않고, 플레이 중 런타임 스프라이트(`Texture2D.LoadImage` + `Sprite.Create`)를 실제 부품에 끼워 게임 폰트 그대로 찍는다.
5. **비교 페이지**(아티팩트)에 지금 모습과 시안을 나란히 놓고 장단점을 적어 고르게 한다.
6. **적용**: 원본 시안은 `Source~/raw/`, 만드는 스크립트는 `Source~/make_*.py`, 결과 PNG는 제자리. UI 조각은 `ui_slices.json` + 메뉴 `Import UI Sprites`. 쓰던 곳을 모두 바꾸고 옛 조각(PNG·스크립트 줄·json 항목)을 지운다. 캡처로 확인한다.

사용자가 「하나씩」이라고 했으면 조각 하나를 끝낸 뒤 다음 조각으로 간다.

## Codex 호출

```bash
cd <scratchpad>/<작업>    # 결과가 떨어질 폴더
echo "<프롬프트> Save the image as x.png in the current working directory and reply with only the path." \
  | codex exec --skip-git-repo-check -s workspace-write -c model_reasoning_effort=low -i 참조1.png -i 참조2.png - > log.txt 2>&1 &
```

- 프롬프트는 stdin으로 넘긴다(`-i` 뒤에 두면 먹힌다). 여러 장은 `&`로 병렬, 파일이 생길 때까지 기다린다.
- ChatGPT 구독 한도를 쓴다. 필요한 장수만.
- **참조**: 결을 맞출 확정 시안이나 게임 캡처를 준다. 캐릭터를 뽑을 때 웜뱃을 단독 참조로 주면 다른 종도 웜뱃처럼 나온다 → 사물·배경을 결 참조로 주고 종·자세는 글로.
- **지우기 편집**: 시안에서 캐릭터·글자만 지운 판이 필요하면 그 그림을 `-i`로 주고 「remove …, keep everything else exactly the same」.
- 프롬프트에 넣을 것: 굵은 사각 픽셀·안티에일리어싱 없음·그라데이션 없음·평면 5~7색·진갈색 외곽선 1칸·순백 배경·글자와 아이콘 없음. 시점은 「orthographic three-quarter top-down, parallel projection, no vanishing point, vertical edges stay vertical, never seen from below」를 따로 강하게 쓰고, 판 · 다리가 있는 가구는 **시점 블록아웃 그림**(`Shop/Source~/shop_raw/view_blockout.png`, `make_view_blockout.py`)을 `-i` 참조로 준다(말로만 시키면 판을 사다리꼴로 그린다, `art.md` 시점). UI 조각은 「9-slice용: 모서리에만 장식, 변은 길이 방향으로 균일, 안은 한 색」.
- 재질(흙·돌·나무결)은 「재료 시트」 한 장에 항목을 넓은 간격으로 그리게 하면 팔레트가 맞는다.

## 격자 (칸 단위로 줄이기)

- 만든 그림은 한 칸이 4~28px로 들쑥날쑥하다. **자동 측정(FFT)은 자주 틀린다**(반 주기·다른 배수). 순서대로 시도한다.
  1. 같은 참조 크기로 그린 시안은 **기준 시안의 격자를 그대로** 쓴다(점원·협상 시안은 941×1672, 한 칸 4.454px).
  2. **외곽선 두께 = 한 칸**으로 재서 그림 상자를 칸 수로 나눈다(`make_pill.py`의 `cells(path, wc, hc)`).
  3. 목표 칸 수를 직접 준다(`make_pixel.py --cells`).
- 칸 색은 칸 가운데 40%의 중앙값. 비슷한 색(거리 28 안)을 한 색으로 모아 잡티를 없앤다.
- 흰 바탕은 **가장자리에서 이어진 흰 칸만** 투명으로 한다(안쪽 밝은 선이 같이 지워지지 않게).
- 9-slice로 늘어나는 구간은 줄마다 한 색으로 고르게 만든다.
- 단순한 도형(말풍선·화살표·막대·눈금)은 시안에서 색만 뽑아 **칸 무늬 문자열**로 직접 그린다(`make_nego_ui.py`의 `pattern`).
- 틀이 잘려 나왔으면 시안 틀을 버리고 공통 외곽선을 직접 두른다.

## 재사용할 코드

| 파일 | 쓰임 |
|---|---|
| `Assets/Sprites/UI/Source~/make_nego_ui.py` | `Grid`(시안 → 칸), `pattern`(칸 무늬), `framed`, `trim_band`, `clear_bg` |
| `Assets/Sprites/UI/Source~/make_panel.py` · `make_chip.py` · `make_pill.py` | 조각 하나를 시안에서 줄이는 짧은 본보기 |
| `Assets/Sprites/World/Source~/make_pixel.py` | 월드 그림 격자 강제(`--px --cells --thin --th`) |
| `Assets/Sprites/World/Shop/Source~/make_anim.py` · `anim_parts.py` · `anim_specs.py` | 정지 그림 한 장 → 걷기·숨쉬기·깜빡임·딴짓 프레임(부위 조립, 자동 검사 포함, 아래 「캐릭터 동작」) |
| `Assets/Scripts/Editor/UISpriteImporter.cs` | `ui_slices.json` → 임포트, 월드 복사본(`tag_cost`·`bubble`·`bubble_tail`) |
| `Assets/Scripts/Editor/VisitorSheetImporter.cs` | 캐릭터 시트 자르기(칸 폭 104px) |

## 캐릭터 동작 (새 동물 · 새 동작)

기준은 지금 동물들(웜뱃 · 회색 점원 · 토끼 · 펭귄 · 여우 · 고슴도치, 2026-09-29 확정)이다. 새로 만든 것은 이들 옆에 세웠을 때 박자 · 움직임 폭이 같아 보여야 한다.

1. **정지 그림 앞 · 뒤 · 옆**: 위 「순서」대로 시안 3장 → 선택. 이것이 원본이다(`Shop/Source~/<종>_<방향>.png`).
2. **부위 좌표 한 벌**(`anim_specs.py`): 몸이 비슷한 종을 복사해 시작한다(긴 두 귀 토끼 · 큰 꼬리 여우 · 옆 날개 펭귄 · 귀 없는 고슴도치 · 앞발 블록이 있는 웜뱃). 좌표는 칸 문자 덤프(`Shop/Source~/dump_cells.py <이름>`, 색마다 글자 하나)로 잡는다. 눈은 자동(`eyes=개수`), 틀리면 `eye_boxes`. 늘어날 줄도 자동, 가시 · 귀 사이 선 때문에 틀리면 `stretch`.
3. **딴짓 한 벌**(`make_anim.py` `FIDGETS`, 앞 · 뒤 · 옆): 공통 동작(`look_around` · `look_up` · `tap` · `waddle` · `shake` · `sniff`) + 그 종만의 부위 하나(`flick` 귀 · 날개, `wag` 꼬리, `rub` 앞발). 1.5~2.5초, 한 칸 80ms, 머묾은 같은 프레임 되풀이. 몸 높이는 바꾸지 않는다.
4. **새 동작**(앉기 · 빵 들기 등)도 같은 부품으로 먼저 짠다: 몸 오르내림 `body_y` · 기울기 `tilt_cells` · 발 들기 `lifts` · `xs` · 늘어남 `body_mode` · 눈 옮기기 `look` · 귀 `ear` · 부위 흔들기 `swing` · 앞발 · 꼬리 블록 `paw` · `tail`. 부품으로 안 되는 자세만 픽셀 AI(PixelLab 등)를 선택지로 낸다.
5. **확인**: `python make_anim.py <미리보기 폴더>` → 종 · 방향마다 idle · walk · fidget · stand(숨쉬기 두 번 → 딴짓) GIF, 자동 검사(색 · 발 기준선 · 외곽선 · 두리번 눈 자리). GIF(한 칸 6px)를 프레임별로 보며 떨어진 선 조각 · 두 겹 외곽선을 찾는다. 생성기 코드를 고쳤으면 이미 있는 걷기 결과와 픽셀 비교로 안 바뀐 것을 확인한다.
6. **등록**: `VisitorTable` 행(시트 경로) → 메뉴 `Import Visitor Sheets`, 웜뱃은 `Bake/Bakery`. 플레이에서 애니메이터의 그림 이름을 여러 번 기록해 딴짓 · 깜빡임이 나오는지 본다.

함정
- 몸 윤곽에 닿는 부위를 블록 옮기기(`paw` · `tail`)로 움직이면 외곽선이 두 겹 → `parts` 흔들기로.
- 부위를 돌리기(다수결)로 움직이면 가는 선이 끊긴다 → 줄마다 가로 밀기(`swing_part`, 붙은 곳 `axis`는 안 움직인다).
- 몸과 외곽선을 같이 쓰는 꼬리는 `seed`로 떼고 공유 선은 몸에 남긴다(같이 옮기면 떨어진 선 조각).
- 칸 폭 104px(52칸)을 넘으면 `sheet()`가 멈춘다 → 그쪽 흔들기 폭을 줄인다(여우 앞 꼬리는 바깥 1칸).
- 시트 가로가 2048px를 넘으면 Unity 기본 최대 크기에 줄어 칸 위(귀)가 잘리고 칸 수가 모자랐다 → 가져오기 최대 4096(`VisitorSheetImporter`), 생성기가 4096(39칸) 안으로 막는다. 게임 캡처로 한 번은 확인한다.
- 두리번에서 눈 옮기기와 기울기를 겹치면 눈이 2칸씩 간다 → 눈이 먼저 1칸, 기울 때는 눈을 제자리로.
- 기울일 때 얼굴 선은 위아래로 붙은 것끼리 한 무리로 옮긴다(`stamp_features`). 따로 옮기면 입이 「3」자가 된다.

## 비교 페이지

- 시안마다 「틀만 확대」와 「게임에 끼운 캡처」를 같이 보여 준다. 지금 모습도 나란히.
- 장단점은 사실로 적는다(새로 만들 조각 수, 규칙과 어긋나는 점).
- 페이지 머리의 `<style>`이 닫혔는지 확인하고 올린다.
- 픽셀 그림은 1배(한 칸 = 1px)로 올리고, 화면 픽셀 기준 정수 배로만 키운다(`img[data-unit]` + `devicePixelRatio`로 폭 계산, `image-rendering: pixelated`). `width: 100%`로 칸에 맞추면 어중간한 배율에 모니터 배율(125%·150%)이 겹쳐 픽셀이 깨진다(2026-09-30 사용자). UI 캡처는 `data-unit="4"`(1 UI px = 캡처 4px), 월드 캡처는 부드럽게 축소한다.
- 적용이 끝나 다음 작업으로 넘어가면 페이지를 지운다.
