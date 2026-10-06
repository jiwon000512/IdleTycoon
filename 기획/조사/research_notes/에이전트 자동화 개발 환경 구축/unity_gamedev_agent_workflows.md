# Unity 게임 개발용 AI 에이전트 작업 흐름과 자동 리소스 생성 파이프라인 (2025~2026)

조사일 2026-10-06. Reddit은 조사 도구가 막혀 있어(접근 불가 도메인) 커뮤니티 경험은 HN·개발 블로그·GitHub 문서에서만 모았다.

## 1. Unity MCP 서버와 에디터 브리지: 무엇이 있고, 무엇을 할 수 있고, 어디서 걸리나

### Takeaway
2026년에는 세 갈래가 있다. (a) 커뮤니티 MCP(CoplayDev `unity-mcp`가 가장 크고, IvanMurzak `Unity-MCP`, CoderGamester `mcp-unity`), (b) Unity 공식 MCP(AI Assistant 패키지에 들어 있고 Unity Cloud 연결과 AI 베타 구독이 필요), (c) Unity CLI + `com.unity.pipeline`(2026-07, 실행 중인 에디터에 C#을 eval하며 도메인 리로드가 없다)와 이를 묶은 공식 Claude Code 플러그인(2026-09-09, 스킬 29개). 공통으로 걸리는 곳은 도메인 리로드 때 연결 끊김, 플레이 모드 테스트, 한 에디터를 여러 에이전트가 나눠 쓸 때 직렬 처리와 타임아웃 뒤 재시도로 같은 일이 두 번 되는 문제, 에디터가 열린 프로젝트에는 batchmode를 못 띄우는 것이다.

### Cited Findings
**CoplayDev/unity-mcp (MCP for Unity)**
- MCP 도구 진입점 48개: 씬·GameObject 생성과 관리, C# 스크립트 편집, 에셋 관리, 테스트 실행, 프로파일링, 빌드. 지원 범위 Unity 2021.3 LTS~6.x, Python 3.10+(`uv`). 최신 v10.0.0(2026-06-30), 별 약 14.7k, MIT. Unity 본사가 아니라 Aura(Coplay)가 관리한다. — [GitHub CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- 다중 인스턴스 라우팅: 에디터마다 `Name@hash`로 구분하고, `set_active_instance`로 세션의 대상을 고정하거나 호출마다 `unity_instance`를 준다. 에디터가 여럿 연결됐는데 대상을 고르지 않으면 오류가 난다. — [MCP for Unity Multi-Instance 가이드](https://coplaydev.github.io/unity-mcp/guides/multi-instance)
- 한 에디터는 명령을 **차례로** 처리한다. 동시에 들어온 호출은 줄을 서고, 문서에는 "에이전트 4개가 붙자 보통 ~5초 걸리던 읽기가 ~17초"가 됐다고 적혀 있다. — [같은 문서](https://coplaydev.github.io/unity-mcp/guides/multi-instance)
- 도메인 리로드 중에는 인스턴스 목록이 잠깐 비어, 에디터가 곧 돌아오는데도 "instance not found" 오류가 가짜로 난다. — [같은 문서](https://coplaydev.github.io/unity-mcp/guides/multi-instance)
- **재시도 위험**: Unity가 실행을 시작한 뒤 30초 타임아웃을 넘긴 명령은 실패로 보고되지만 효과는 적용된다. 그래서 `execute_code`·테스트 실행 같은 긴 작업을 무작정 재시도하면 같은 일이 두 번 된다. 재시도 전에 결과를 확인해야 한다. — [같은 문서](https://coplaydev.github.io/unity-mcp/guides/multi-instance)
- 알려진 설치 문제: WSL2의 Claude가 Windows의 Unity에 닿지 않는 문제(포트 포워딩·방화벽·HTTP 전송으로 해결), Unity 6.3+에서 Unity AI Assistant 패키지와 `System.Collections.Immutable` DLL 버전이 충돌하는 문제(AI Assistant를 지우거나 맞는 DLL을 넣는다), Unity가 `claude` 경로를 못 찾는 문제. — [CoplayDev Wiki: Common Setup Problems](https://github.com/CoplayDev/unity-mcp/wiki/3.-Common-Setup-Problems)

**IvanMurzak/Unity-MCP**
- 내장 도구 70개 이상: 에셋·프리팹·머티리얼, 씬·하이어라키, **스크린샷**, Roslyn으로 C# 즉시 컴파일·실행, 리플렉션으로 메서드 호출, 테스트 실행, 콘솔 로그, 프로파일러. 빌드된 게임 안(런타임)에서도 쓸 수 있다(`UnityMcpPluginRuntime.Initialize()`). 로컬은 stdio, 원격은 streamableHttp(기본 포트 8080). 별 약 4.4k. — [GitHub IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)
- 제약: 프로젝트 경로에 **공백이 있으면 안 된다**. Unity API는 메인 스레드에서 돌아야 한다. 600초 동안 쓰지 않으면 서버가 꺼진다(바꿀 수 있음). — [GitHub IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)

**CoderGamester/mcp-unity**
- 도구 30개 이상: `run_tests`, `set_play_mode_status`, `batch_execute`, GameObject·컴포넌트·머티리얼 조작, 패키지 추가. Unity 6 이상. 에디터(포트 8090)와 Node.js 브리지가 WebSocket으로 통신한다. 별 약 1.9k. — [GitHub CoderGamester/mcp-unity](https://github.com/codergamester/mcp-unity)
- **플레이 모드 테스트를 돌리면 도메인 리로드로 연결이 끊긴다.** 공식 해결책은 Project Settings > Editor에서 "Reload Domain"을 끄는 것. — [GitHub CoderGamester/mcp-unity](https://github.com/codergamester/mcp-unity); 원인 설명(리로드가 C# 상태를 통째로 내려 MCP 서버·WebSocket도 내려가는데, 클라이언트는 죽은 소켓만 계속 붙잡고 있다) — [mcp-unity PR #21 검색 요약](https://github.com/CoderGamester/mcp-unity/pull/21)

**Unity 공식 MCP (AI Assistant 패키지)**
- 요건: Unity 6(6000.0)+, `com.unity.ai.assistant` 패키지, Unity Cloud에 연결된 프로젝트, Unity AI 베타의 체험판이나 유료 구독. 씬 관리, GameObject·컴포넌트 값 읽고 쓰기, 스크립트 편집, 콘솔 읽기, 빌드 설정 조회를 하고, C#으로 사용자 도구를 등록할 수 있다. 베타라 언제든 바뀔 수 있다고 적혀 있다. 블로그 날짜 2026-05-11. — [Unity 블로그: Unity MCP 시작하기](https://unity.com/blog/unity-ai-mcp-how-to-get-started)
- 구조: Unity가 시작될 때 릴레이 바이너리를 `%USERPROFILE%\.unity\relay\`에 깔고, 클라이언트는 이것을 `--mcp`로 실행한다. 처음 연결할 때 에디터의 Pending Connections에서 승인해야 한다. 에디터가 여럿이면 `--project-path`/`UNITY_PROJECT_PATH`나 `--instance-id`(PID)로 대상을 고른다. 도구 이름 예: `Unity_ManageScene`, `Unity_ManageGameObject`, `Unity_ReadConsole`. — [Unity 문서: Get started with Unity MCP (AI Assistant 2.7)](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.7/manual/integration/unity-mcp-get-started.html)
- 공식 문서에는 플레이 모드·테스트 실행·스크린샷 도구가 나오지 않는다(위 두 출처에서 확인되지 않음).

**Unity CLI + `com.unity.pipeline` (2026-07-20)**
- 독립 실행 바이너리. 에디터 설치(`unity install 6000.2.10f1 -m android ios webgl`), `unity editors`, `unity open`, `unity pipeline install/list`, `unity auth login`, `unity doctor`, **`unity command eval "code"` / `eval_file`로 실행 중인 에디터에서 C#을 실행**하며 이때 프로젝트 재컴파일이나 도메인 리로드가 없다. `[CliCommand]` 속성으로 사용자 명령을 만들 수 있다. 출력은 JSON/TSV, 종료 코드는 0/1/130. CI용 서비스 계정을 지원한다. Unity 6.0 LTS 이상. 빌드된 플레이어에도 `--runtime`으로 붙는데, 런타임 쪽은 localhost 전용이고 개발 빌드에서 기본으로 꺼져 있다. eval은 보안 토큰으로 막혀 있다. — [Unity 블로그: Meet the Unity CLI](https://unity.com/blog/meet-the-unity-cli)
- HN 반응: CI와 병렬 세션에 쓰기 좋고 빌드를 끝까지 자동화할 수 있다는 칭찬. 걱정은 **에디터가 프로젝트를 독점으로 잠가서** 프로젝트를 임시 폴더에 복사하는 우회가 필요하다는 점, 헤드리스 시작 속도. MCP 서버와 기능이 겹친다는 지적. — [HN: The Unity CLI](https://news.ycombinator.com/item?id=48995712)

**공식 Unity Plugin for Claude Code (2026-09-09)**
- 명령 하나로 설치되며 Unity 엔지니어링 스킬 29개 + Unity CLI + 라이브 에디터 제어용 Unity MCP 서버를 함께 깐다. 스킬 분야: 프로젝트 설정·CLI·패키지 관리, UI(uGUI/UITK/IMGUI·TMP), 2D(pixel-perfect·sprite-editor·sprite atlas·tilemap), URP 렌더링, 오디오, 내비·물리, IAP·LevelPlay·라이브 서비스, 멀티플레이, 웹 최적화, 현지화. Claude Code가 처음 지원 대상이고 다른 에이전트는 2026년 안에 지원할 예정. — [Unity 블로그: Official Unity Plugin for Claude Code](https://unity.com/blog/unity-plugin-for-claude-code); [PocketGamer.biz](https://www.pocketgamer.biz/unity-launches-official-claude-code-plugin-with-29-built-in-engine-skills/)
- 같은 블로그에는 구독 요건, 테스트·플레이 모드·스크린샷 기능이 적혀 있지 않다. — [Unity 블로그](https://unity.com/blog/unity-plugin-for-claude-code)

**batchmode / -executeMethod**
- 에디터가 열어 둔 프로젝트를 batchmode로 또 열 수는 없다. 한 프로젝트에는 Unity 인스턴스 하나만 붙는다. 그래서 batchmode 테스트는 에디터를 닫고 돌리거나, 에디터 안의 Test Framework로 돌려야 한다. — [Unity 문서: Command line arguments (구버전 매뉴얼)](https://docs.unity3d.com/550/Documentation/Manual/CommandLineArguments.html)

**기타**
- OpenClaw의 Unity·Godot·Unreal 플러그인(Apache 2.0): 에디터 안에 HTTP 서버를 띄워 오브젝트를 조작하고, **플레이 중 키보드·마우스 입력을 흉내 내고**, 스크린샷을 찍는다. Unity용 도구는 ~100개. HN 반응은 거의 없었다(댓글 1개). — [HN Show HN](https://news.ycombinator.com/item?id=47121900)

### Inferences
- "한 에디터 + 여러 에이전트" 구조는 MCP를 쓰더라도 처리가 직렬이라 느려지고, 타임아웃 뒤 재시도로 같은 일이 두 번 될 수 있다. 에디터를 쓰기 전에 서로 알리는 운영 규칙(이 프로젝트의 아트방/프로그래밍방 규칙)은 도구가 풀어 주지 않는 문제를 대신 막는 셈이라 계속 필요해 보인다.
- 도메인 리로드는 세 MCP 모두의 공통 약점이다. 리로드가 없는 Unity CLI `eval`은 상태를 읽거나 단발 조작을 하는 데 구조적으로 유리하다. 다만 스크립트를 고친 뒤 컴파일·리로드는 어차피 일어나므로, 리로드가 끝났는지 확인하는 단계는 그대로 필요하다.
- 공식 MCP는 Unity Cloud 연결과 AI 구독이 필요하고 6.3+에서 커뮤니티 MCP와 DLL이 충돌한 보고도 있다. 이미 CLI(`com.unity.pipeline`) 경로로 돌아가는 프로젝트라면 AI Assistant 패키지를 더할 이유가 약하다.
- IvanMurzak 판은 프로젝트 경로에 공백이 있으면 안 되고, 한글 경로에 대해서는 근거가 없다. 이 프로젝트는 Unity 경로가 `C:\project\Tycoon\ProjectTycoon`이라 공백 문제는 없다.

### Gaps
- CoplayDev 도구 카탈로그 페이지(`/reference/tools/`)가 404여서 도구 이름(예: 스크린샷·플레이 모드·`run_tests`가 비동기 작업인지)은 하나하나 확인하지 못했다.
- 공식 Unity MCP가 플레이 모드 제어·테스트 실행·스크린샷을 지원하는지, 공식 Claude Code 플러그인에 구독이 필요한지는 공식 출처에서 확인되지 않았다.
- 세 MCP의 토큰 소모량(도구 스키마 크기)을 비교한 자료는 찾지 못했다.

## 2. Unity 자동 플레이 테스트: CLI 테스트, 입력 흉내, 결정론적 시뮬, 스크린샷 검사

### Takeaway
기본 재료는 Unity Test Framework(EditMode/PlayMode, CLI는 NUnit XML), Input System의 `InputTestFixture`(가상 장치로 입력 주입), Graphics Test Framework의 `ImageAssert`(기준 이미지와 비교)다. 에이전트용 MCP는 이것들을 에디터 안에서 실행해 주는 얇은 층이다. 밸런스 시뮬은 엔진 밖에서 헤드리스로 돌리거나 Machinations 같은 도구를 쓰는 것이 일반적이다.

### Cited Findings
- CLI 테스트: `Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults artifacts/playmode-results.xml -testCategory "Smoke" -logFile artifacts/unity.log`. `-testPlatform`은 EditMode·PlayMode·빌드 타깃(실제 플레이어 빌드에서 실행) 중 하나. 결과는 NUnit XML. — [Unity Test Framework 문서: Running tests from the command line](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html); [tessl 레퍼런스 요약](https://tessl.io/registry/testland/unity-test-framework)
- 에디터가 열려 있으면 batchmode를 같은 프로젝트에 띄울 수 없다(1장 참조). — [Unity 문서: Command line arguments](https://docs.unity3d.com/550/Documentation/Manual/CommandLineArguments.html)
- MCP로 플레이 모드 테스트를 돌리면 도메인 리로드로 연결이 끊긴다. 해결책은 "Reload Domain"을 끄는 것(Enter Play Mode Options). — [CoderGamester/mcp-unity](https://github.com/codergamester/mcp-unity)
- Unity 이슈 트래커에는 플레이 모드를 짧은 간격으로 여러 번 들어갔다 나오면 "Reloading Domain" 창에서 에디터가 멈추는 버그 보고가 있다. — [Unity Issue Tracker #20062](https://issuetracker.unity.com/issues/20062/editor-gets-stuck-on-the-reloading-domain-window-when-the-play-mode-is-entered-and-exited-a-few-times-in-a-short-period)
- 입력 흉내: `InputTestFixture`는 테스트마다 비어 있는 Input System 인스턴스를 세우고 끝나면 되돌린다. `InputSystem.AddDevice<T>()`로 가상 장치를 붙여 입력을 주입하고, 이 입력은 실제 플랫폼 입력과 똑같이 처리된다. `manifest.json`의 `testables`에 `com.unity.inputsystem`을 넣고 `Unity.InputSystem.TestFramework` asmdef를 참조해야 한다. `[InitializeOnLoad]`·`[RuntimeInitializeOnLoadMethod]`로 등록한 사용자 레이아웃은 테스트 안에서 다시 등록해야 한다. — [Input System 1.20 문서: Testing](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/Testing.html)
- 스크린샷 검사: Graphics Test Framework의 `ImageAssert.AreEqual()`은 이미지나 카메라 출력을 기준 이미지와 비교한다. 픽셀별 허용치와 전체 허용치를 줄 수 있고, 실행하면 `Assets/ActualImages`가 생기며 이것을 기준 이미지 폴더로 바꿔 쓴다. — [Graphics Test Framework 문서](https://docs.unity3d.com/Packages/com.unity.testframework.graphics@8.9/manual/index.html)
- 에이전트용 시각 확인: IvanMurzak MCP와 OpenClaw 플러그인은 스크린샷 도구를 준다. OpenClaw는 플레이 중 입력 흉내도 지원한다. — [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP); [HN Show HN OpenClaw](https://news.ycombinator.com/item?id=47121900)
- 사례: Unity 6 + Claude Code(에이전트 8개 병렬) + Unity MCP로 10일 만에 2D 로그라이트(스크립트 173개, ~29,000줄)를 내면서 **테스트 파일 88개**를 "빨리 움직여도 깨지지 않게 하는 안전망"으로 썼다. — [BigDevSoon: Shipped a 2D Roguelite in 10 Days](https://bigdevsoon.me/blog/building-games-with-ai-indie-game-dev-workflow/)
- 에이전트가 쓴 테스트는 **지금 동작을 그대로 통과시키는 쪽**으로 쓰이기 쉬워 버그를 못 잡는다는 지적이 있다. — [Althera Games 블로그 (2026-06-18)](https://altheragames.com/en/blog/ai-agents-game-development-2026)
- 밸런스: Machinations.io는 게임 시스템을 다이어그램으로 모델링하고 몬테카를로로 예측한다. 방치형 경제는 스프레드시트·CSV로 튜닝하는 것이 흔하다. 경제가 복잡한 게임에서는 엔진을 처음부터 헤드리스로 만들어 게임 상태를 모듈로 내보낸 사례가 있다. — [Machinations 사례 모음](https://www.casestudies.com/company/machinationsio); [itch.io 데브로그(Ledger Guild Simulator)](https://itch.io/devlog/1630696/ledger-guild-simulator-devlog.amp); [Idle Economy Builder](https://mtw1man2.itch.io/idle-formula-builder)

### Inferences
- 시뮬이 `Mall.Tick`처럼 순수 C#이고 숫자가 JSON 표에 있는 구조라면, 결정론적 밸런스 시뮬은 Unity 밖 EditMode 테스트(시간을 고정 스텝으로 몇 시간치 돌려 수입 곡선 단언)로 하는 것이 가장 싸다. PlayMode·도메인 리로드 문제를 피한다.
- "플레이에서 확인"은 (1) 상태 값 단언(CLI eval·MCP로 읽기)과 (2) 캡처를 기준 이미지와 비교(`ImageAssert`)로 나누면, 사람이 눈으로 보는 단계를 줄일 수 있다. 다만 연출·애니메이션처럼 시간에 따라 변하는 화면은 허용치를 튜닝하는 비용이 든다.

### Gaps
- Unity 6.3에서 `-runTests`와 Enter Play Mode Options(리로드 끔)를 같이 쓸 때의 함정은 공식 자료를 찾지 못했다.
- 에이전트가 플레이 중 입력을 넣고 결과를 판정하는 "자율 플레이테스트"를 상용 수준으로 쓴 인디 사례(정량 결과 포함)는 찾지 못했다.

## 3. 커뮤니티 경험: Claude Code·Cursor + Unity로 1인 개발할 때 되는 것과 안 되는 것

### Takeaway
되는 것: 로직 위주 코드, 뼈대 작성, 테스트·현지화·리소스 처리 스크립트, MCP를 통한 씬 조사·디버깅. 안 되는 것: 손맛(game feel) 튜닝, 시각적 UI·애니메이션을 처음부터 만드는 일, 저장 시스템·게임 고유 로직, 성능 병목 진단. 한 번에 고치는 양을 작게 하고 사람이 검토하는 것이 생산성의 조건이라는 데 의견이 모인다. Reddit 원문은 확인하지 못했다.

### Cited Findings
- 에이전트가 잘하는 것: 뼈대(UE5 클래스 세팅 2~3시간 → ~25분), 테스트 생성(~5분), 현지화(10~15개 언어), 리소스 처리 스크립트(일괄 변환·검증). 못하는 것: 게임 고유 로직, 저장 시스템, 성능 디버깅(GPU/CPU 병목 구분 못 함), "손맛" 튜닝과 설계 판단. 체감 이득은 주당 6~10시간. 권고: PR은 200줄 미만, 병합 전 사람 검토 필수, 자율 에이전트(Devin)는 게임 코드에서 위험이 더 크다. — [Althera Games (2026-06-18)](https://altheragames.com/en/blog/ai-agents-game-development-2026) (UE5 중심 글이라 Unity에 그대로 옮기려면 주의)
- 10일 로그라이트 사례: Claude Code가 "운영 전체의 중심", 설계·구현·밸런스·테스트를 나눈 에이전트 8개, Unity MCP로 씬 조사·런타임 디버깅, 월 AI 비용 ~$50, "AI는 비전을 키워 줄 뿐 대신하지 않는다". — [BigDevSoon](https://bigdevsoon.me/blog/building-games-with-ai-indie-game-dev-workflow/)
- Claude는 슬라이더·애니메이션 같은 시각 작업을 처음부터 만들 때 막히고, 로직이 많은 구현이나 데이터 파일 연결은 훨씬 잘한다. — [WebSearch 요약, 원문은 itch.io 데브로그 계열로 추정되어 출처 불분명](https://quetzakol.itch.io/lantre-du-protecteur/devlog/184718/post-mortem-partie-2-un-jeu-en-2d-avec-unity3d) (신뢰도 낮음)
- 스크립트만으로는 끝나지 않는다. GameObject 계층, SerializeField 연결, 레이어·태그 지정이 따로 남는다. — [claudelab.net: Claude Code for Unity](https://claudelab.net/en/articles/claude-code/claude-code-unity-game-development-complete) (검색 요약 기준)
- HN: "Unity MCP 하나면 바깥에서 거의 뭐든 할 수 있다"는 경험담, 그리고 Unity가 MCP 서버를 버전과 함께 갱신하면 에이전트가 학습 데이터에 없던 새 기능도 바로 본다는 장점. — [HN 검색 결과 요약](https://news.ycombinator.com/item?id=48995712)
- HN에는 "MCP was always a bad idea?" 같은 MCP 회의론 스레드도 있다(내용은 확인 안 함). — [HN](https://news.ycombinator.com/item?id=49779329)
- YouTube "12 Months of Unity Dev with Claude Code"라는 장기 사용기가 있다(내용 미확인). — [YouTube](https://www.youtube.com/watch?v=lf57tKke_XA)

### Inferences
- 공통으로 권하는 방식은 데이터 주도(JSON 표) + 테스트 안전망 + 작은 단위 작업 + 사람이 플레이로 판정하는 것이다. 에이전트가 약한 분야(손맛·연출)는 사람이 판정하는 단계로 남기는 것이 업계의 일반적인 합의로 보인다.
- 씬·프리팹 연결 작업을 코드(조립 스크립트·굽기 메뉴)로 옮겨 두면 에이전트가 약한 "에디터 수작업" 영역이 줄어든다.

### Gaps
- r/Unity3D·r/gamedev·r/IndieDev 원문은 조사 도구가 막혀 있어 읽지 못했다. Unity 포럼·X 글도 이번에 직접 확인하지 못했다.
- 커뮤니티 MCP별 만족도·불만을 정량 비교한 자료는 없다.

## 4. 2D 픽셀아트·오디오 AI 리소스 파이프라인과 "시안 → 비교 → 선택 → 적용" 반복

### Takeaway
픽셀아트는 전용 도구(PixelLab·Retro Diffusion, 둘 다 MCP 있음)가 격자·투명도·방향별 캐릭터·타일셋을 직접 다루고, 범용 생성기(gpt-image-2 등)는 생성 → 배경 제거 → 격자·팔레트 정리 후처리가 필요하다. 스타일 일관성은 승인된 그림을 참조 이미지로 넘기고 팔레트를 제한하는 방식이 가장 많이 쓰인다. 오디오는 로컬 ACE-Step 1.5(음악), Stable Audio Open(효과음), 클라우드 ElevenLabs SFX v2(루프 지원)가 주 선택지다.

### Cited Findings
**픽셀아트 전용**
- PixelLab MCP: 4·8방향 캐릭터, 캐릭터 애니메이션(걷기·달리기·대기), 탑다운 Wang 타일셋, 횡스크롤 타일셋, 아이소 타일, 투명 배경 맵 오브젝트(스타일 맞춤). 이미지 도구는 작업 ID를 돌려주고 `get_image`로 폴링하는 비동기 방식. 참조 이미지로 스타일을 맞춘다. 맵 오브젝트는 8시간 뒤 자동 삭제된다. Claude Code·Cursor 등을 지원한다. — [PixelLab MCP](https://www.pixellab.ai/mcp)
- PixelLab 요금: Apprentice $10/월(생성 1,000회), Artisan $30/월(3,000회), Architect $50/월(6,000회), 무료 체험 40회 + 하루 느린 생성 5회. MCP 도구는 28개이고 Claude Code용 픽셀아트 생성·정리 스킬이 있다. — [Knowara PixelLab 리뷰(2차 출처)](https://knowara.com/ai-tools/image/pixellab-review/) (공식 요금 페이지에서 확인하지는 못함)
- Retro Diffusion: HTTP API와 원격 MCP 서버(설치 없음). 모델 RD Fast·Plus·Pro·Mini, 스타일 90개 이상, 이미지·애니메이션(GIF/스프라이트 시트)·타일셋 생성, 커진 이미지나 흐려진 픽셀아트 복구, 비용 견적 무료. 장당 ~$0.01부터, 선불이고 구독 없음, 크레딧 만료 없음. — [Retro Diffusion MCP (mcpservers.org)](https://mcpservers.org/ko/servers/retro-diffusion/retro-diffusion-mcp); [Retro-Diffusion api-examples](https://github.com/Retro-Diffusion/api-examples)

**범용 이미지 생성기 (gpt-image-2 / Codex 계열)**
- gpt-image-2는 알파 채널을 생성 단계에서 안정적으로 주지 않는다. 생성 뒤 배경을 지우는 것이 권장된다. — [geniea: GPT Image pixel art prompts](https://www.geniea.com/prompts/gpt-image-pixel-art-game-art)
- AgentBrush 파이프라인: gpt-image-2 + 스타일 프리셋 → 마스크로 부분 수정 → 로컬 배경 제거(진짜 알파 PNG) → 승인된 스프라이트를 `reference_image_paths`로 다음 생성의 앵커로 넘긴다("다시 처음부터 프롬프트하는 것보다 눈에 띄게 안정적"). 실패하는 곳: 프레임 사이 동작 호가 일정하지 않은 애니메이션, 아이소 타일 격자 어긋남, 여러 장에 걸쳐 정확한 hex 팔레트를 유지하지 못함, 16×16 미만에서는 진짜 픽셀아트가 아니라 축소된 일러스트가 나옴. — [AgentBrush 블로그 (2026)](https://agentbrush.dev/blog/2d-game-asset-generation)
- AI 픽셀아트에서 흔히 고치는 것: 고르지 않은 픽셀 격자, 흐린 가장자리, 들쭉날쭉한 팔레트, 끊긴 외곽선, 게임 크기에서 읽히지 않는 디테일. — [Seeles 블로그](https://www.seeles.ai/resources/blogs/ai-pixel-art-generator-create-game-assets)
- 10일 로그라이트 사례: 스프라이트는 Flux 2D Game Assets, 배경은 Recraft V4 Pro, 변형은 Flux 2 Pro. **7색 팔레트를 강제해** 서로 다른 AI 출력을 하나의 스타일로 묶었다. 소리는 ElevenLabs(효과음 9개, 음악 3곡). — [BigDevSoon](https://bigdevsoon.me/blog/building-games-with-ai-indie-game-dev-workflow/)

**후처리·조립 자동화**
- Aseprite CLI: `aseprite -b file.aseprite --split-tags --sheet-type packed --sheet out.png --data out.json`, 파일명 자리표시자 `{tag}`·`{layer}`·`{tagframe}`. Lua: `aseprite.exe -b my-sprite.aseprite -script export.lua`로 `app.command.ExportSpriteSheet()`를 부른다(batch 모드에서는 스프라이트가 열려 있어야 이 명령이 활성화된다). — [Aseprite 커뮤니티: Batch scripting and ExportSpriteSheet](https://community.aseprite.org/t/batch-scripting-and-exportspritesheet/17395); [Aseprite 스프라이트 시트 문서](https://mintlify.com/aseprite/aseprite/features/sprite-sheets)
- Aseprite MCP(ext-sakamoro): CLI와 Lua로 픽셀아트 생성·편집·프레임·스프라이트 시트 내보내기를 에이전트가 하게 한다. — [AsepriteMCP (mcpservers.org)](https://mcpservers.org/en/servers/ext-sakamoro/AsepriteMCP)
- Unity 쪽 임포트 자동화에는 공식 Claude Code 플러그인의 `2d-pixel-perfect`·`sprite-editor`·`manage-sprite-atlas` 스킬이 있다. — [Unity 블로그](https://unity.com/blog/unity-plugin-for-claude-code)

**오디오**
- ACE-Step 1.5(2026-02 공개, XL은 2026-04-02): 오픈소스 음악 모델. 짧은 루프부터 10분 곡까지, VRAM 4GB 미만으로 로컬 실행, 곡 몇 개로 LoRA 개인화, RTX 3090에서 곡 하나 10초 미만. Qwen 계열 LM(0.6B/1.7B/4B)이 곡 설계를 하고 DiT가 생성한다. XL은 VRAM 12GB 이상(오프로드), 20GB 권장. ComfyUI 공식 지원. — [ACE-Step 1.5 (gitee 미러)](https://gitee.com/andangel/ACE-Step-1.5); [GIGAZINE: ACE-Step 1.5 XL](https://www.gigazine.net/gsc_news/en/20260409-ace-step-1-5-xl/); [ComfyUI Wiki](https://comfyui-wiki.com/en/news/2026-01-23-ace-step-1-5-music-generation); [AMD 블로그](https://www.amd.com/en/blogs/2026/commercial-grade-ai-music-generation-on-amd-ryzen-ai-and-radeon-ace-step-1-5.html)
- ElevenLabs Sound Effects v2: 텍스트로 효과음, 길이 지정, **루프 옵션**(시작·끝이 티 나지 않게 반복). 길이 범위는 출처마다 다르다: 0.1~30초([ElevenLabs 문서](https://elevenlabs.io/docs/overview/capabilities/sound-effects)) vs 0.5~22초([302.ai 상품 페이지](https://302.ai/product/detail/elevenlabs-sound-generation)). 요금은 길이를 지정하면 초당 40 크레딧(재판매처마다 다름). — [ElevenLabs 문서](https://elevenlabs.io/docs/overview/capabilities/sound-effects)
- Stable Audio Open 1.0: 가중치가 공개된 텍스트→오디오 모델. 앰비언스·폴리·효과음을 최대 47초까지, 로컬에서 클립당 비용 없이 만든다. 같은 출처는 SFX 전용 small 체크포인트가 있는 "Stable Audio 3"도 언급하는데, 1차 출처(Stability AI)로 확인하지는 못했다. — [ComfyUI Wiki: Stable Audio](https://comfyui-wiki.com/en/models/stable-audio)

**출시·정책**
- Steam(2026-01-16 개정): 뒤에서 쓰는 "효율 도구" AI는 신고할 필요가 없고, 플레이어가 직접 접하는 생성 AI 콘텐츠(그림·소리 포함)만 신고한다. 2026-07-28 조사에서 상점 게임의 약 5분의 1이 AI 표시를 달았고, 2026년 6월 한 주 출시작의 40%가 생성 AI 사용을 신고했다. 저작권·안전 책임은 개발자에게 있다. — [GIGAZINE (2026-01-20)](https://gigazine.net/gsc_news/en/20260120-steam-updates-ai-disclosure-guidelines); [Notebookcheck](https://www.notebookcheck.net/Steam-updates-AI-disclosure-form-requiring-developers-to-report-visible-and-in-game-AI-but-not-background-tools.1206103.0.html); [tech-insider](https://tech-insider.org/steam-ai-disclosure-2026)

### Inferences
- "시안 3개 → 비교 페이지 → 선택 → 적용" 반복은 공개 사례에서 따로 도구로 만든 것을 찾지 못했다. 다만 사례마다 들어가는 재료는 같다: (1) 승인본을 참조 이미지 앵커로 넘김, (2) 팔레트 제한(7색 등), (3) 생성 뒤 배경 제거·격자 정리, (4) Aseprite/Unity 스크립트로 시트·임포트 설정. 이 프로젝트의 Codex CLI → 칸 단위 픽셀 변환 → 게임에 끼워 캡처 → 비교 페이지 흐름은 공개 사례보다 앞서 있는 편이다.
- 정확한 hex 팔레트는 범용 생성기로 유지되지 않으므로(AgentBrush), 팔레트 고정은 생성 단계가 아니라 후처리 단계(양자화)에서 강제하는 것이 맞다.
- 방향별 캐릭터·걷기 애니메이션·타일셋이 필요해지면 PixelLab/Retro Diffusion이 범용 생성기보다 손이 덜 갈 수 있다. 단, 이 프로젝트의 그림으로 시험한 적은 없다.
- 모바일(Google Play·App Store) 출시라면 Steam 공개 규칙은 직접 해당되지 않는다. 다만 앞으로 PC판을 내면 그림·소리 모두 신고 대상이다.

### Gaps
- PixelLab·Retro Diffusion으로 기존 손그림·Codex 스타일에 맞춰 새 그림을 만들 때의 일관성을 객관적으로 비교한 자료는 없다.
- ACE-Step 1.5로 마디 단위로 끊김 없이 루프되는 BGM을 만드는 공식 기능(루프 모드)이 있는지는 확인하지 못했다. 공개 자료는 "짧은 루프"를 만들 수 있다는 정도만 말한다.
- ElevenLabs SFX의 길이 범위는 출처끼리 다르다(위 참조). 공식 API 문서로 확정하지 못했다.
- Google Play·App Store의 AI 생성 콘텐츠 공개 의무는 이번 범위에서 조사하지 않았다.
