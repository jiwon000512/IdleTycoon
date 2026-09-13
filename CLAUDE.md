# 동물원 타이쿤 (Project Tycoon)

1인 개발 모바일 방치형 경영 타이쿤. Unity로 만들어 Android(우선)·iOS 출시가 목표.
기획서: `기획/동물원-타이쿤-기획서.md` (현재 v0.5). 데이터 테이블 규칙: `기획/데이터-테이블-규칙.md`.
초기 세팅 결정과 절차(다음 프로젝트 재사용용): `기획/유니티-프로젝트-초기-세팅-가이드.md`. 실행본 스킬: `~/.claude/skills/unity-project-setup/`.
원격 저장소: https://github.com/jiwon000512/IdleTycoon (main). 저장소 루트는 `C:\project\Tycoon`.
기획과 개발을 병행하며 기획서는 결정이 바뀔 때마다 버전을 올린다.

## 폴더

```
Tycoon/
├─ 기획/                 기획서(.md). 결정·숫자·범위는 여기가 단일 출처
└─ ProjectTycoon/        Unity 프로젝트 (Unity 6000.3.24f1, URP 2D)
   └─ Assets/
      ├─ Scenes/Main.unity   단일 씬 (세로 고정 1080×1920, 카메라 이동 없음)
      ├─ Scripts/
      │  ├─ Core/    순수 C# 게임 로직 (UnityEngine 의존 금지: 수입 계산·가차 확률·비용 곡선·오프라인 계산·세이브 모델)
      │  ├─ Data/    JSON 테이블에 대응하는 C# 레코드·로더·검증기 (UnityEngine 비의존)
      │  ├─ Game/    MonoBehaviour 계층 (GameManager, 틱, 저장/로드, 씬 부트스트랩)
      │  ├─ UI/      uGUI 뷰·팝업 (상단 바, 우리 격자, 가차 버튼, 홍보/도감 팝업, 오프라인 팝업)
      │  └─ Editor/  에디터 전용 도구 (Editor 폴더라 빌드에서 제외됨)
      ├─ Resources/Data/     JSON 테이블 4개 (animals, grades, zoo_levels, game_config)
      ├─ Resources/Sprites/Animals/  동물 스프라이트 — 파일명 = 동물 ID (Resources.Load)
      ├─ Prefabs/, Prefabs/UI/
      ├─ Sprites/UI/, Fonts/, Audio/
      └─ Tests/EditMode/     Core 로직 유닛 테스트 (Unity Test Framework)
```

Assets 바로 아래에 종류별 폴더를 둔다. 어셈블리 정의(asmdef)는 쓰지 않는다(기본 Assembly-CSharp 하나).
`Core/`는 UnityEngine을 참조하지 않는다는 규칙만 지키면 된다. 빈 폴더는 `.gitkeep`으로 유지(Unity는 점으로 시작하는 파일을 무시).

## 기술 결정

| 항목 | 결정 |
|---|---|
| 엔진 | Unity 6000.3.24f1, URP 2D Renderer |
| UI | uGUI(Canvas) + TextMeshPro. UI Toolkit 사용 안 함 |
| 입력 | Input System 패키지 (`Assets/Settings/InputSystem_Actions.inputactions`). 첫 버전은 uGUI 버튼 탭만 |
| 화면 | 세로 고정(Portrait only), 기준 해상도 1080×1920, CanvasScaler Scale With Screen Size |
| 숫자 | `double` + K/M/B 표기. 수입 틱 0.1~0.25초, 표시 숫자만 보간 |
| 저장 | 로컬 JSON 1파일 (`Application.persistentDataPath`), 마지막 저장 시각 UTC. 시간 조작 방어는 첫 버전에 없음 |
| 데이터 | JSON 단일 원본(`Assets/Resources/Data/*.json`, 테이블당 1파일) + Newtonsoft.Json. ScriptableObject 사용 안 함. 에셋은 ID 규칙 경로로 참조. 형식·검증 규칙은 `기획/데이터-테이블-규칙.md` |
| 빌드 | Android IL2CPP, ARM64. 제품명/회사명/패키지 ID는 아직 임시(DefaultCompany) — 스토어 등록 전 변경 |
| 제외 패키지 | Visual Scripting, Timeline, Multiplayer Center, SpriteShape, Aseprite, PSD Importer, 2D Animation, Tilemap Extras (필요해지면 다시 추가) |
| 유지 패키지 | `com.unity.pipeline`은 Unity CLI가 에디터에 연결할 때 쓰므로 지우지 않는다 |

## 코드 규칙

- 식별자는 영어, 주석·문서·커밋 메시지는 한국어.
- C#: 4칸 들여쓰기, 여는 중괄호 새 줄, private 필드 `_camelCase`, 상수 `PascalCase`. `.editorconfig` 참고.
- 네임스페이스 `ZooTycoon.Core / .Data / .Game / .UI`.
- MonoBehaviour는 얇게. 규칙과 수식은 Core에 두고 Tests/EditMode에서 검증한다.
- 기획서의 숫자는 코드에 하드코딩하지 않고 `game_config.json` 등 JSON 테이블에 둔다. 레코드 클래스에는 기획서 표 번호(예: 6.3)를 주석에 남긴다.
- JSON 테이블을 고칠 때는 `기획/데이터-테이블-규칙.md` 6장 절차를 따른다(기획서 먼저 → JSON → 검증 테스트). 파일 전체를 유효한 JSON으로 다시 쓴다.

## 작업 방식

- 개발 순서: 메인 루프(가차 → 배치 → 초당 수입 → 재투자/홍보 → 오프라인 수익 → 저장)를 먼저 완성하고 재미를 확인한 뒤 컨텐츠를 붙인다. 기획서 5장·9장 우선순위를 따른다.
- 기획을 바꾸는 결정은 먼저 사용자에게 객관식으로 하나씩 묻고, 확정되면 기획서 「확정 결정」표와 변경 이력에 반영한다.
- Unity 에디터가 열려 있으면 Unity CLI(unity-cli 스킬)로 씬·프리팹을 조작할 수 있다. 닫혀 있을 때는 텍스트 자산(.asset/.unity/.meta) 직접 편집이 가능하나 새 폴더/파일에는 .meta를 함께 만든다.
- 커밋은 사용자가 요청할 때만. 메시지는 한국어 한 줄 요약 + 필요 시 본문.

## 유용한 명령

```bash
# 에디터 열기 (Unity CLI)
unity project open ProjectTycoon
```
