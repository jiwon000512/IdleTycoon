---
paths:
  - "ProjectTycoon/Assets/Scripts/**/*.cs"
  - "ProjectTycoon/Assets/Tests/**/*.cs"
---

# 코드 규칙 (요약)

상세는 `기획/코드-규칙.md`(구조)와 `기획/프로그래밍-규약.md`(줄 단위). 어긋나면 상세 문서가 기준이다.

## 구조

- 어셈블리 = 폴더 = 네임스페이스: `ZooTycoon.Core / .Data / .Game / .UI / .World / .Editor / .Tests.EditMode`. 의존은 UI·World → Game → Data → Core 아래로만. Core·Data는 UnityEngine을 참조하지 않는다.
- 게임 규칙은 Core에만. MonoBehaviour는 생명주기에서 Core를 부르는 얇은 어댑터다.
- UI는 MVP: View는 표시 메서드와 입력 이벤트만, Presenter(순수 C#)가 모델을 구독해 View를 갱신한다. UI는 모델을 직접 바꾸지 않는다.
- 싱글턴은 GameKit Manager와 `GameManager`만. `static` 가변 상태·`Find...` 금지. 의존은 생성자로. 서비스 조립은 `GameManager.Init`, Presenter 조립은 `MainScene`.
- GameKit Manager(`UIManager`·`TableManager`·`DataManager`·`PoolManager`·`SoundManager`)는 Game·UI·World 계층에서만 쓴다.
- 도메인 사건은 `EventBus`(선언 `Core/Events/Events.*.cs`, 발행은 일이 일어난 객체). C# event는 사물 하나 구독과 View 입력에만.
- 씬에는 조립 지점만 둔다. 월드 오브젝트는 전부 프리팹 + 실행 중 생성.
- 월드 프리팹을 통째로 만드는 스크립트는 `Scripts/Editor`에 `[MenuItem("ZooTycoon/Bake/...")]`. UI 프리팹은 프리팹이 원본이라 직접 고친다.

## 쓰는 법

- 식별자는 영어, 주석·문서·커밋은 한국어. 4칸, Allman, private `m_camelCase`, static `s_`, 상수 `k_PascalCase`, 핸들러 `Subject_EventName`(버스는 `Bus_<사건>`), 접근 제한자 명시, `var`는 타입이 보일 때만.
- 예상된 실패는 `Result`/`TryXxx`, 버그는 예외.
- 주석·로그는 최소. 요청되지 않은 방어 장치·옵션·추상화를 넣지 않는다.
- 이름: 표 행 `XXTable`, 데이터 `XXData`, 상속은 부모의 마지막 명사로 끝낸다, `Manager`는 GameKit 파사드만.

## 사용자 리뷰 기준

- 책임은 주인 객체에 모은다. 무엇을 할 수 있나는 표에, 코드는 어떻게만.
- 파일·개념 수를 줄이고 지우는 쪽을 고른다.
- 구조를 제안할 때는 앞으로 올 업종(다른 가게)에도 맞는지 따진다. 뼈대는 곳·사물·행동이다.
- 리뷰 요청을 받으면 ① 짧게 정리 ② 예시 코드 한 조각 ③ OK 뒤 수정.

## 찾기

클래스·호출 관계는 문서가 아니라 graft로 찾는다(`graft ask`, `graft callers`).
