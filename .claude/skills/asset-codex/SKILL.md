---
name: asset-codex
description: 그림 리소스(캐릭터·소품·배경·UI 조각·화면 시안)를 새로 만들거나 고칠 때 읽는다 — Codex CLI로 시안 생성, 칸 단위 픽셀 변환, 게임에 끼워 캡처, 비교 페이지, 적용과 옛 조각 정리까지의 절차.
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
- 프롬프트에 넣을 것: 굵은 사각 픽셀·안티에일리어싱 없음·그라데이션 없음·평면 5~7색·진갈색 외곽선 1칸·순백 배경·글자와 아이콘 없음. UI 조각은 「9-slice용: 모서리에만 장식, 변은 길이 방향으로 균일, 안은 한 색」.
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
| `Assets/Scripts/Editor/UISpriteImporter.cs` | `ui_slices.json` → 임포트, 월드 복사본(`tag_cost`·`bubble`·`bubble_tail`) |
| `Assets/Scripts/Editor/VisitorSheetImporter.cs` | 캐릭터 시트 자르기(칸 폭 104px) |

## 비교 페이지

- 시안마다 「틀만 확대」와 「게임에 끼운 캡처」를 같이 보여 준다. 지금 모습도 나란히.
- 장단점은 사실로 적는다(새로 만들 조각 수, 규칙과 어긋나는 점).
- 페이지 머리의 `<style>`이 닫혔는지 확인하고 올린다.
- 적용이 끝나 다음 작업으로 넘어가면 페이지를 지운다.
