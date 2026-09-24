# 동물원 타이쿤 (Project Tycoon)

1인 개발 모바일 방치형 경영 타이쿤. Unity로 만들어 Android(우선)·iOS 출시가 목표.
기획서: `기획/동물원-타이쿤-기획서.md` (현재 v0.22). **세션 시작 시 `기획/진행상황.md`를 먼저 읽고, 세션 끝에 갱신한다.** 진행상황은 세 절(현재 상태 · 다음에 할 것 · 도구 함정) 각 10줄 안쪽으로 유지하고, 지난 일은 쌓지 않는다(과거 원문은 `기획/진행상황-이력.md`, 읽지 않아도 된다). 포트폴리오(AI 활용·기술 중심, 기능마다 갱신): `기획/포트폴리오.md`. 데이터 테이블 규칙: `기획/데이터-테이블-규칙.md`. 코드 규칙: `기획/코드-규칙.md`. 프로그래밍 규약: `기획/프로그래밍-규약.md`.
초기 세팅 절차의 원본은 Claude 스킬 `~/.claude/skills/unity-project-setup/`(GitHub jiwon000512/Claude_UnitySkill). 저장소의 `기획/유니티-프로젝트-초기-세팅-가이드.md`는 안내만.
원격 저장소: https://github.com/jiwon000512/IdleTycoon (main). 저장소 루트는 `C:\project\Tycoon`.
기획과 개발을 병행하며 기획서는 결정이 바뀔 때마다 버전을 올린다.

## 폴더

```
Tycoon/
├─ 기획/                 기획서(.md). 결정·숫자·범위는 여기가 단일 출처
└─ ProjectTycoon/        Unity 프로젝트 (Unity 6000.3.24f1, URP 2D)
   └─ Assets/
      ├─ Scenes/Main.unity   단일 씬 (세로 고정 1080×1920, XZ 평면 월드 + 55° 원근 카메라 팬, HUD는 uGUI 오버레이)
      ├─ Scripts/
      │  ├─ Core/    순수 C# 게임 로직 (UnityEngine 의존 금지: 수입 계산·가차 확률·비용 곡선·오프라인 계산·세이브 모델)
      │  ├─ Data/    JSON 테이블에 대응하는 C# 레코드·로더·검증기 (UnityEngine 비의존)
      │  ├─ Game/    MonoBehaviour 계층 (GameManager, 틱, 저장/로드, 씬 부트스트랩)
      │  ├─ UI/      uGUI HUD·팝업 (상단 바, 가차 버튼, 결과 카드, 홍보/도감 팝업, 오프라인 팝업)
      │  ├─ World/   빵집 굴 화면(굴 그림·사물·손님·웜뱃, 웜뱃을 따라가는 카메라) — 설계 03에서 asmdef 확정, 설계 09에서 지상 폐기
      │  └─ Editor/  에디터 전용 도구 (Editor 폴더라 빌드에서 제외됨)
      ├─ Resources/Data/     JSON 테이블 12개 (XXTable.json = 행 클래스 이름: ConfigTable·BakeryConfigTable·VisitorTable·BreadTable 등)
      ├─ Resources/Sprites/Animals/<종>/  동물 아트: 정지 1장 + idle·move 시트. 경로는 animals.json 칼럼. 임포트 규칙은 데이터-테이블-규칙 5장
      ├─ Prefabs/, Prefabs/UI/
      ├─ Sprites/UI/, Fonts/, Audio/
      └─ Tests/EditMode/     Core 로직 유닛 테스트 (Unity Test Framework)
```

Assets 바로 아래에 종류별 폴더를 둔다. Scripts의 각 폴더와 Tests/EditMode는 asmdef 하나씩(`ZooTycoon.Core/.Data/.Game/.UI/.Editor/.Tests.EditMode`)이며 의존은 UI→Game→Data→Core 아래로만 흐른다. Core·Data는 UnityEngine을 참조할 수 없다(`noEngineReferences`). 빈 폴더는 `.gitkeep`으로 유지(Unity는 점으로 시작하는 파일을 무시).

## 기술 결정

| 항목 | 결정 |
|---|---|
| 엔진 | Unity 6000.3.24f1, URP 2D Renderer |
| UI | uGUI(Canvas) + TextMeshPro. UI Toolkit 사용 안 함 |
| 입력 | Input System 패키지 (`Assets/Settings/InputSystem_Actions.inputactions`). uGUI 버튼 + 플로팅 조이스틱(`UI/Joystick`, 설계 09) |
| 화면 | 세로 고정(Portrait only), 기준 해상도 1080×1920, CanvasScaler Scale With Screen Size. 월드는 **원근 카메라(55° 내려다봄, FOV 60)가 보는 XZ 평면** 위의 SpriteRenderer: 땅·잔디는 눕힌 타일, 동물·관광객은 카메라를 향해 세운 카드 + 눕힌 그림자. 팬은 초점(XZ)을 옮김. 깊이는 월드 Z축 정렬(URP 2D Renderer Custom Axis Z, 정렬점 Pivot=발끝. 설계 06 C5) |
| 숫자 | `double` + K/M/B 표기. 가게 시뮬(`ShopSim.Tick`)은 매 프레임(웜뱃은 조이스틱, 설계 09) |
| 저장 | 로컬 JSON 1파일 (`Application.persistentDataPath`), 마지막 저장 시각 UTC. 시간 조작 방어는 첫 버전에 없음 |
| 코드 구조 | asmdef 6개로 계층 강제 · UI는 MVP(View MonoBehaviour + Presenter 순수 C#) · 게임 상태·서비스는 `GameManager`(MonoSingleton, `Init()`·틱)가 만들고, 씬 스크립트(`MainScene`)가 그 씬의 View·Presenter만 조립 · 서비스·Presenter는 생성자 주입 · 순수 C# event + 동기 틱. 상세 `기획/코드-규칙.md` |
| 공통 기반 | GameKit UPM 패키지(`com.jiwon.gamekit`, 저장소 `C:\project\UnityGameKit`, GitHub jiwon000512/UnityGameKit). `MonoSingleton<T>` 기반 Manager(UI·Table·Data·Event·Pool)는 lazy 자기 초기화, Game·UI 계층에서만 접근. 규약 `기획/프로그래밍-규약.md` 10장 |
| 데이터 | JSON 단일 원본(`Assets/Resources/Data/*.json`, 테이블당 1파일) + Newtonsoft.Json. ScriptableObject 사용 안 함. 에셋은 JSON 경로 칼럼(`VisitorTable.sprite` 등)으로 참조하고, 더미 리소스를 먼저 만들어 두면 사용자가 같은 경로로 실제 리소스를 교체. 형식·검증 규칙은 `기획/데이터-테이블-규칙.md` |
| 빌드 | Android IL2CPP, ARM64. 제품명/회사명/패키지 ID는 아직 임시(DefaultCompany) — 스토어 등록 전 변경 |
| 제외 패키지 | Visual Scripting, Timeline, Multiplayer Center, SpriteShape, Aseprite, PSD Importer, 2D Animation, Tilemap Extras (필요해지면 다시 추가) |
| 유지 패키지 | `com.unity.pipeline`은 Unity CLI가 에디터에 연결할 때 쓰므로 지우지 않는다 |
| 추가 패키지 | `com.unity.nuget.newtonsoft-json` 3.2.2 (Data 계층 JSON 파싱). TMP Essential Resources 임포트됨(`Assets/TextMesh Pro`) |

## 코드 규칙

- 식별자는 영어, 주석·문서·커밋 메시지는 한국어.
- 표기는 Unity 6판 C# 스타일 가이드 + `기획/프로그래밍-규약.md`: 4칸, Allman 중괄호, private `m_camelCase`, static `s_`, 상수 `k_PascalCase`, 이벤트 발생 `On...`, 핸들러 `Subject_EventName`, 접근 제한자 항상 명시, `var`는 타입이 보일 때만. 예상된 실패는 `Result`/`TryXxx`, 버그는 예외. 주석·로그는 최소한으로, 요청되지 않은 방어 장치·옵션은 넣지 않는다.
- 네임스페이스 = 폴더 = 어셈블리 (`ZooTycoon.Core / .Data / .Game / .UI / .Editor`).
- 게임 규칙은 Core 서비스에만. MonoBehaviour는 생명주기 훅에서 서비스를 호출하는 얇은 어댑터. `static` 가변 상태·`Find...` 금지. 싱글턴은 GameKit Manager와 `GameManager`만. 의존은 생성자로, 서비스 조립은 `GameManager.Init`, Presenter 조립은 씬 스크립트(`MainScene`)에서만.
- UI는 MVP. View는 표시 메서드와 입력 이벤트만, Presenter가 모델 이벤트를 구독해 View를 갱신한다. UI는 모델을 직접 바꾸지 않는다.
- 기능 하나 = 세션 하나. ① 설계(`기획/설계/NN-*.md`: 코드 설계 + 검증 항목)를 사용자와 확정 → ② Unity CLI를 최대한 써서 한 번에 구현(스크립트·씬·프리팹·게임오브젝트·더미 리소스까지) → ③ **에이전트가 1회 실행해 검증**: 컴파일 0·콘솔 0, EditMode 테스트(`run_tests`는 플레이 모드가 아닐 때만), 플레이 모드 진입 후 캡처로 결과 확인. 이것이 "개발 완성"이다 → ④ 짧게 보고(만든 것·검증 결과·달라진 점)하고 `기획/진행상황.md`와 `기획/포트폴리오.md`(4장에 기능 한 절: 기술 포인트·AI가 한 일, 5장 숫자) 갱신, **보고 끝에 커밋을 제안**한다(미커밋·피드백 대기 상태를 세션 너머로 남기지 않는다). 테스트 방법 문서는 쓰지 않고, 문서화는 최소한만. 구현 중에는 사용자를 기다리지 않는다. 전체 규칙은 `기획/코드-규칙.md`.
- 프리팹을 통째로 만드는 에디터 스크립트는 `Scripts/Editor`에 `[MenuItem("ZooTycoon/Bake/...")]`로 남긴다(현재 UI·Shop·Fonts·Import UI Sprites·Import Visitor Sheets). 일회성 조립·조회 스크립트만 scratchpad. 규칙 문서 개정은 기능 구현과 커밋을 나눈다.
- 기획 회차는 같은 주제로 2회를 넘기지 않는다. 2회차 뒤에는 후보 하나를 별도 씬 1주 프로토타입으로 검증한다.
- 아트는 `기획/아트-프롬프트.md` 0장 스타일 가이드를 모든 프롬프트 앞에 붙이고(동물은 웜뱃이 아니라 사물 참조 + 문장으로 종 지정, 후처리 `make_pixel.py`로 격자 강제, 한 화면 한 칸 크기: 지상 4px), **시안 3장 → 캡처 위 합성 비교 → 사용자 1회 선택 → 굽기 1회** 순서로 한다. 굽기 전에는 프리팹을 건드리지 않는다.
- 기획서의 숫자는 코드에 하드코딩하지 않고 `ConfigTable.json` 등 JSON 테이블에 둔다. 행 클래스(`XXTable`)에는 기획서 표 번호(예: 6.3)를 주석에 남긴다.
- JSON 테이블을 고칠 때는 `기획/데이터-테이블-규칙.md` 6장 절차를 따른다(기획서 먼저 → JSON → 검증 테스트). 파일 전체를 유효한 JSON으로 다시 쓴다.

## 작업 방식

- 개발 순서: 메인 루프(가차 → 배치 → 초당 수입 → 재투자/홍보 → 오프라인 수익 → 저장)를 먼저 완성하고 재미를 확인한 뒤 컨텐츠를 붙인다. 기획서 5장·9장 우선순위를 따른다.
- 기획을 바꾸는 결정은 먼저 사용자에게 객관식으로 하나씩 묻고, 확정되면 기획서 「확정 결정」표와 변경 이력에 반영한다.
- Unity 에디터가 열려 있으면 Unity CLI(unity-cli 스킬)로 씬·프리팹을 조작할 수 있다. 닫혀 있을 때는 텍스트 자산(.asset/.unity/.meta) 직접 편집이 가능하나 새 폴더/파일에는 .meta를 함께 만든다.
- Unity CLI 요령: 연결 확인은 `unity pipeline list`. 여러 줄 C#은 `run_script --file --entry`(eval은 using 불가). Git Bash에서 계층 경로를 쓰기 전에 `export MSYS_NO_PATHCONV=1`. UI 확인은 플레이 모드에서 `capture_game_view --source screen`. 스크립트 작성 후 `recompile` → `editor_status.compiling=false` 대기 → `console_status` 오류 0 확인.
- 커밋은 사용자가 요청할 때만(에이전트는 검증이 끝난 보고 끝에 제안만 한다). 메시지는 한국어 한 줄 요약 + 필요 시 본문.

## 유용한 명령

```bash
# 에디터 열기 (Unity CLI)
unity projects open ProjectTycoon
```
