---
name: plan-doc
description: 기획서 「사업가 웜뱃」을 고치거나 올릴 때 읽는다 — 원본은 저장소 기획/기획서.html, 아티팩트는 보기용.
---

# 기획서 고치기

- 원본: `기획/기획서.html`(아티팩트 https://claude.ai/artifact/TQaPKFEcLafuVm4WNitbGv 의 본문, 문서 뼈대 없음). 결정 · 숫자 · 범위의 단일 출처다.
- 통째로 읽지 않는다(128KB). `grep -n`으로 자리를 찾아 그 줄만 Read · Edit 한다.
  - 「확정 결정」 새 행: 파일의 첫 `</table></div>`(확정 결정 표 끝) 바로 앞.
  - 버전 이력: 표 아래 `<p class="meta">v0.NN …` 맨 앞에 새 판을 붙인다. 머리말 `eyebrow`의 버전 · 날짜도.
- 올리기: Artifact publish `file_path=기획/기획서.html` + `url=위 주소`.
  - 이 세션에서 처음이면 거절되고 지금 판이 파일로 온다 → `python3 Tools/plan_sync.py <그 파일>`.
  - 「같음」이면 그대로 다시 올린다. 「다름」이면 사용자가 웹에서 고친 것이다: 저장소 원본이 웹 판으로 바뀌었으니 `git diff`로 확인하고 내 수정을 다시 얹어 올린다.
- 늘 읽는 문서에 `@`로 넣지 않는다(import는 맥락을 줄이지 않는다). 커밋은 문서 커밋에.
