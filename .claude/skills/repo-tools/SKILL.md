---
name: repo-tools
description: 커밋 묶기 · 표 버전 올리기 · 포트폴리오 숫자 · 아트 넘김 목록 · 훅(잠금 · 빠른 테스트 · 아침 정리) — 저장소 Tools/와 .claude/helpers 도구를 쓸 때 읽는다.
---

# 저장소 도구

셸은 Git Bash, `python3`(MSYS)로 돈다. 한글 입출력은 도구가 UTF-8로 맞춘다.

| 하려는 일 | 명령 |
|---|---|
| 커밋 묶음 보기(기능 · 아트 넘김 · 도구 · 문서) | `python3 Tools/commit_groups.py` |
| 표 버전 올리기(JSON · 테스트 TestCase · 규칙 문서 이력 뼈대) | `python3 Tools/table_bump.py "<까닭(설계 N)>" <Table> [...]` |
| 포트폴리오 5장 숫자 | `python3 Tools/stats.py` |
| 아트 넘김 등록(아트방) · 보기 · 붙임 · 커밋 뒤 비우기 | `python3 Tools/handoff.py add <id> "<메모>" <파일 · glob…>` · `list` · `done <id>` · `clear` |
| 기획서 웹 판 맞추기 | 스킬 `plan-doc` |
| Unity 밖 빠른 테스트 | `bash Tools/fast_test.sh` |

## 훅(`.claude/settings.json`, 두 방 모두)

- `editor-guard.sh`(PreToolUse): 에디터 차례 잠금. unity-editor 스킬 「에디터 차례 잠금」.
- `fast-gate.sh`(PostToolUse 표시 · Stop 검사): Core · Data · 테스트 .cs를 고친 턴은 끝날 때 빠른 테스트.
- `day-check.sh`(UserPromptSubmit): 날짜가 바뀐 첫 명령에 아침 정리 · 넘김 남은 것을 알린다(새벽 0~5시는 전날).
- `Tools/githooks/pre-commit`(`git config core.hooksPath Tools/githooks`): 늘 읽는 문서 12KB 넘으면 막고, 테스트 단언이 빠지면 경고.

## 커밋 순서

1. `commit_groups.py`로 묶음을 본다. 아트 넘김(「붙임」)은 그 기능 커밋에 같이, 도구 · 문서는 따로.
2. 커밋 뒤 `python3 Tools/handoff.py clear`.
