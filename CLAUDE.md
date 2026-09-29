# 사업가 웜뱃 (Project Tycoon)

1인 개발 모바일 방치형 경영 타이쿤. 웜뱃이 굴을 파고 가게를 연다. 굴 하나 = 업종 하나, 지금은 빵집 굴 · 농장 굴 · 굴 속 광장. Unity로 만들어 Android(우선)·iOS 출시가 목표.
저장소 루트 `C:\project\Tycoon`, 원격 https://github.com/jiwon000512/IdleTycoon (main). 코드의 이름(`ZooTycoon`, `ProjectTycoon`)은 옛 제목 「동물원 타이쿤」에서 온 것으로 그대로 쓴다.

## 지금 상태

@기획/진행상황.md

위 내용이 보이지 않으면 `기획/진행상황.md`를 먼저 읽는다. 기능이 끝나면 고친다.

## 문서 지도

| 무엇 | 어디 | 언제 읽히나 |
|---|---|---|
| 작업 절차(설계·검증·커밋·아티팩트·문서) | `.claude/rules/workflow.md` | 늘 |
| 코드 규칙 요약 | `.claude/rules/code.md` | `Scripts/`·`Tests/`를 읽을 때 |
| 데이터 테이블 규칙 요약 | `.claude/rules/data-tables.md` | `Resources/Data/`·표 클래스를 읽을 때 |
| UI 규칙(원칙·공용 조각·팔레트) | `.claude/rules/ui.md` | UI 스크립트·프리팹·조각을 읽을 때 |
| 아트 규칙(스타일·검수) | `.claude/rules/art.md` | 스프라이트를 읽을 때 |
| Unity 에디터 조작·함정 | 스킬 `unity-editor` | Unity를 건드리기 전 |
| 그림 리소스 만들기 | 스킬 `asset-codex` | 그림을 만들 때 |
| BGM·효과음 만들기 | 스킬 `bgm` | 소리를 만들 때 |
| 연출·완성도 점검 | 스킬 `polish-review` | 사용자가 부를 때 |
| 기획서(결정·숫자·범위의 단일 출처) | 아티팩트 「사업가 웜뱃」 https://claude.ai/artifact/TQaPKFEcLafuVm4WNitbGv | 기획을 다룰 때 |
| 상세 참조 | `기획/코드-규칙.md` · `프로그래밍-규약.md` · `데이터-테이블-규칙.md` | 요약으로 모자랄 때 |
| 기록 | `기획/포트폴리오.md` · `재미-프로필.md` · `기획/보관/`(폐기 문서) | 필요할 때만 |

경로 규칙은 Read 도구로 해당 파일을 읽을 때 자동으로 들어온다. 셸로만 작업해 규칙이 보이지 않으면 위 표의 파일을 직접 읽는다.

## 폴더

```
Tycoon/
├─ .claude/rules · skills   에이전트 규칙과 절차
├─ 기획/                    진행상황 · 상세 규칙 · 포트폴리오 · 보관
└─ ProjectTycoon/Assets/    Unity 프로젝트
   ├─ Scenes/Main.unity     단일 씬(조립 지점만)
   ├─ Scripts/  Core · Data · Game · UI · World · Editor
   ├─ Tests/EditMode/       Core·Data 유닛 테스트
   ├─ Resources/  Data(JSON 표 19개) · UI(UI 프리팹) · Sprites · Audio · Shaders
   ├─ Prefabs/Bakery/       월드 프리팹(메뉴 Bake/Bakery가 굽는다)
   ├─ Sprites/  UI · World  조각 원본과 만드는 스크립트는 각 Source~/
   └─ Audio/Source~/        효과음·BGM 원본 스크립트
```

## 기술 결정

| 항목 | 결정 |
|---|---|
| 엔진 | Unity 6000.3.24f1, URP 2D Renderer |
| 화면 | 세로 고정 1080×1920. 월드는 직교 카메라가 보는 2D, 깊이는 발끝(Pivot) 기준 정렬. 카메라는 웜뱃을 따라간다 |
| UI | uGUI + TextMeshPro(갈무리 비트맵 폰트). UI Toolkit 사용 안 함. MVP |
| 입력 | Input System. 플로팅 조이스틱 + 상호작용 버튼 |
| 구조 | asmdef 7개(`ZooTycoon.Core/.Data/.Game/.UI/.World/.Editor/.Tests.EditMode`). 상태·서비스는 `GameManager.Init`, 씬 조립은 `MainScene` |
| 공통 기반 | GameKit UPM 패키지 `com.jiwon.gamekit`(저장소 `C:\project\UnityGameKit`, GitHub jiwon000512/UnityGameKit): MonoSingleton·EventBus·TableSet·UI·Pool·Sound |
| 데이터 | JSON 단일 원본 + Newtonsoft.Json. ScriptableObject 사용 안 함 |
| 숫자 | `double` + K/M/B 표기. 시뮬은 매 프레임(`Mall.Tick`) |
| 저장 | 아직 없음. 계획은 로컬 JSON 1파일, 마지막 저장 시각 UTC |
| 빌드 | 목표 Android IL2CPP ARM64(지금 타깃은 Standalone). 제품명·회사명·패키지 ID는 임시 |
| 패키지 | `com.unity.pipeline`은 Unity CLI 연결용이라 지우지 않는다. `com.unity.nuget.newtonsoft-json` 3.2.2 |
| 리소스 생성 | 그림은 Codex CLI, 소리는 로컬 ACE-Step 1.5(`C:\project\ACE-Step-1.5`) |

## 개발 순서

빵집 굴의 메인 루프(굽기 → 진열 → 계산 → 재투자·점원)를 다듬은 뒤 벽 → 오프라인·저장 → 두 번째 가게 순서. 우선순위는 기획서를 따른다.
