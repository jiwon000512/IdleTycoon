---
paths:
  - "ProjectTycoon/Assets/Resources/Data/**"
  - "ProjectTycoon/Assets/Scripts/Core/Tables/**"
  - "ProjectTycoon/Assets/Scripts/Data/**"
  - "ProjectTycoon/Assets/Tests/EditMode/TableValidatorTests.cs"
---

# 데이터 테이블 규칙 (요약)

상세(형식·검증·표별 칸)는 `기획/데이터-테이블-규칙.md`. 코드 주석이 그 문서의 절 번호(8.x)를 가리키므로 번호는 바꾸지 않는다.

- JSON이 단일 원본이다(`Assets/Resources/Data/XXTable.json`, 파일 = `table` 값 = 행 클래스 이름). ScriptableObject를 쓰지 않는다.
- 조회는 GameKit `TableSet.Get<T>(id)` · `GetAll<T>()`만. 문구는 `tables.Text/Format`.
- 에셋은 Resources 경로 칸으로 가리킨다(확장자 없음).

## 표를 고칠 때

1. 기획 숫자가 바뀌면 기획서 아티팩트를 먼저 고친다.
2. JSON: 값만 바꾸면 version 그대로. **칸 구조나 코드가 부르는 id가 바뀌면 `version` +1**과 `notes`에 한 줄(날짜·까닭). 파일 전체가 유효한 JSON이어야 하고 줄바꿈(CRLF)을 유지한다. 행을 지울 때는 `notes`에 까닭을 남긴다.
3. 행 클래스(`Core/Tables/XXTable.cs`): setter 있는 속성, 코드가 부르는 id는 `k_*` 상수.
4. `Data/TableValidator.cs`에 칸 규칙, 코드가 id로 부르는 행은 `CheckRequired`.
5. `Tests/EditMode/TableValidatorTests.cs`: 표 버전 `TestCase`를 맞추고, 새 규칙마다 실패 사례 하나.
6. `기획/데이터-테이블-규칙.md` 8장의 그 표 절과 변경 이력.
7. 문구에 새 글자가 생기면 `FontAssetTests`가 실패한다 → 에디터 메뉴 `ZooTycoon/Bake/Fonts`.

새 표는 1~6을 모두 하고 8장에 절을 추가한다.
