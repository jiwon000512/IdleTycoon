---
name: unity-editor
description: 이 프로젝트(ProjectTycoon)의 Unity 에디터를 CLI로 다룰 때 읽는다 — 컴파일 확인, EditMode 테스트, 플레이 검증, 화면 캡처, 프리팹 직접 수정, 굽기 메뉴, 에디터 멈춤 복구. Unity를 건드리기 전에 먼저 읽는다.
---

# Unity 에디터 조작

모든 명령은 `ProjectTycoon/` 폴더에서 Git Bash로 돌린다. 스크립트 경로는 저장소 루트 기준이다.

## 자주 쓰는 것

| 하려는 일 | 명령 |
|---|---|
| 컴파일 + 콘솔 확인 | `bash .claude/skills/unity-editor/scripts/compile.sh` |
| EditMode 테스트 | `bash .claude/skills/unity-editor/scripts/test.sh` |
| 플레이 시작(세로 캡처 준비 포함) | `bash .claude/skills/unity-editor/scripts/play.sh` |
| 캡처 | `bash .claude/skills/unity-editor/scripts/capture.sh <이름>` → `<scratchpad>/<이름>.png` 경로를 출력 |
| 플레이 끝 + 임시 캡처 삭제 | `bash .claude/skills/unity-editor/scripts/stop.sh` |
| 에디터 열기 / 연결 확인 | `unity projects open ProjectTycoon` / `unity pipeline list` |
| 명령 목록 | `unity list` |

## 명령 문법

- 에디터 명령은 `unity cmd <이름>`이다(`unity editor_status`는 없는 명령).
- 인자는 `--이름 값`: `unity cmd menu --path "ZooTycoon/Bake/Bakery"`, `unity cmd capture_game_view --source camera --width 1080 --height 1920 --save_path "Temp/x.png"`.
- 한 줄 C#: `unity cmd eval --code '... return "ok";'`. `using`을 못 쓰니 타입은 전체 이름으로.
- 여러 줄 C#: 파일로 쓰고 `unity cmd run_script --file <절대경로.cs> --entry 클래스.메서드 --mode ephemeral [--timeout 300]`. 메서드는 `public static string`.
- 결과 읽기: `| tail -1 | grep -o '"result":"[^"]*"'`. JSON이 필요하면 `--json`을 붙이고 python으로 파싱한다(`data.result`).
- 상태: `unity cmd editor_status`의 `playMode`(playing/stopped) · `compiling` · `domainReloadInProgress`.
- 굽기 메뉴: `Import UI Sprites`(UI 조각 + 월드 복사본) · `Fonts` · `Bakery`(월드 프리팹) · `Import Visitor Sheets`. 오래 걸리는 메뉴는 `eval`(5초 타임아웃) 대신 `run_script`로 `EditorApplication.ExecuteMenuItem`.

## 프리팹 직접 수정

`scripts/PrefabEdit.cs`를 scratchpad에 복사해 `Edit()` 안만 고쳐 `run_script`로 돌린다. 직렬화 필드 연결은 `SerializedObject`로. 프리팹 diff가 몇 줄로 끝나야 정상이다. 스크립트로 붙인 `HorizontalLayoutGroup`·`VerticalLayoutGroup`은 `childControlWidth/Height`를 직접 켠다(에디터의 `AddComponent`는 `Reset()`이 둘을 끈다. 플레이 중 목업에서는 켜져 있어 결과가 달라진다).

## 플레이 검증

- 게임 뷰는 가로 1280×720이라 `--source screen` 캡처는 가로로 나오고 멈춘 프레임을 줄 때가 있다. **세로 UI 캡처는 캔버스를 카메라로 돌린 뒤 `--source camera`**로 찍는다(`play.sh`가 `UiCamera.cs`로 해 준다. 팝업을 새로 연 뒤 캔버스가 바뀌면 `UiCamera.Run`을 다시).
- 버튼은 `onClick.Invoke()`, 눌림은 `ExecuteEvents.Execute(..., pointerDownHandler)`로 흉내 낸다. private 필드는 리플렉션으로.
- 1초짜리 연출·소리는 캡처 대신 상태 값을 읽는다(`GameManager.Instance`, `SoundManager`의 AudioSource).
- 시간을 멈춰 찍으려면 `Time.timeScale = 0`(끝나고 1로).
- 프리팹을 고친 뒤에는 플레이를 새로 시작한다.
- 캡처 `save_path`는 프로젝트 안만 되고 `Temp/x.png`는 `Assets/Temp/`에 생긴다 → `stop.sh`가 지운다.

## 함정

- **에셋 조작(삭제·임포트·굽기·프리팹 저장)과 `recompile`·`run_tests`는 `playMode=stopped`에서만.** 플레이 중 굽기는 실행 중 값이 프리팹에 저장되고, 플레이 중 재컴파일은 도메인 리로드로 오류가 난다. 사용자가 작업을 요청한 상태면 `editor_stop`으로 끄고 진행한다.
- 플레이 종료 직후 바로 에셋을 지우면 에디터가 멈춘 적이 있다 → `stopped`를 확인하고 2초 뒤에.
- `run_tests`는 한 번만 부른다. 같은 명령을 겹쳐 부르면 테스트 러너가 깨진다.
- 재컴파일은 플레이가 끝날 때까지 미뤄진다. 플레이 중의 「컴파일 0」은 새 코드를 검사한 결과가 아니다.
- 콘솔은 세션을 넘어 남는다. 판정 전에 `clear_console`. 잘못된 명령을 부르면 그 호출이 콘솔 오류 1건으로 남는다.
- 콘솔의 `Failed to handle /api/exec request: Main thread operation timed out`은 도메인 리로드 중에 보낸 명령이 기다리다 난 도구 오류다(게임 오류 아님). `compile.sh`를 한 번 더 돌리면 사라진다.
- 콘솔의 `TreeViewController NullReference`·`GUIClips`는 에디터 내부 오류다. 게임 오류는 `error CS`와 게임 스택만 본다.
- Run In Background는 프로젝트 설정으로 늘 켠다(`ProjectSettings.asset` `runInBackground: 1`, 2026-09-29 사용자). 에디터가 포커스를 잃어도 플레이가 돈다. 0으로 바뀐 diff는 되돌리지 말고 1로 고친다. 플레이 중에 바꾼 값은 플레이가 끝나면 에디터가 되돌리므로, 멈춘 상태에서 `PlayerSettings.runInBackground` + `AssetDatabase.SaveAssets()`로 바꾼다.
- 재굽기는 fileID를 새로 만들어 diff가 커진다. 내용이 같은 부산물(손님 시트 메타, `CoinPopup.prefab` 등)은 `git checkout` 뒤 `AssetDatabase.Refresh`.
- 같은 경로에 에셋을 다시 만들 때는 기존 파일에 덮어써 GUID를 지킨다(프리팹 참조가 그대로 넘어간다). 이름만 바꿀 때는 `AssetDatabase.MoveAsset`.
- `GetComponent ?? AddComponent`는 가짜 null로 실패한다 → `TryGetComponent`.
- libsndfile로 OGG를 쓸 때는 1초씩 나눠 쓴다(한 번에 쓰면 조용히 죽는다).

## 에디터가 멈췄을 때

`editor_status`가 **한 번** 타임아웃되면 진단하지 말고 재시작한다(재시도 루프 금지).

```
powershell Stop-Process -Name Unity -Force
rm -f ProjectTycoon/Temp/UnityLockfile
unity projects open ProjectTycoon        # 백그라운드로
# unity cmd editor_status 가 ready 가 될 때까지 기다린 뒤 하던 일을 이어 한다
```

재시작한 사실은 보고에 한 줄로 적는다.

## 환경

- 이미지·소리 처리는 Windows Python `C:/Users/jiwon/AppData/Local/Programs/Python/Python312/python.exe`(Pillow·numpy). PATH의 `python3`은 MSYS(패키지 없음)라 텍스트 처리에만 쓴다.
- 한글이 든 파일은 셸 heredoc보다 Write 도구로 쓴다. 파일 안 줄바꿈은 CRLF가 섞여 있으니 문자열 치환 전에 확인한다.
- 모니터 1(왼쪽, DISPLAY1)에 에디터를 두는 것이 기본이다.
