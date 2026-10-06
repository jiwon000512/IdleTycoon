#!/usr/bin/env bash
# 아티팩트 삭제를 한 번만 묻기(PreToolUse: Artifact, 2026-10-06 사용자 「한번에 물어보면 안되나」).
# 리드가 지울 목록을 AskUserQuestion으로 한 번 묻고, 승인된 주소를 .claude/state/artifact-delete-ok.txt에 한 줄씩 적는다(다 지우면 파일을 지운다).
# 그 목록에 든 주소의 삭제만 확인 창 없이 허용한다. 그 밖의 삭제 · 다른 동작은 건드리지 않는다(앱이 평소대로 묻는다)
input=$(cat)
case "$input" in *'"delete"'*) ;; *) exit 0 ;; esac
printf '%s' "$input" | python3 -c '
import sys, json, os
d = json.loads(sys.stdin.buffer.read().decode("utf-8")); t = d.get("tool_input") or {}
ok = os.path.join(os.environ.get("CLAUDE_PROJECT_DIR") or ".", ".claude", "state", "artifact-delete-ok.txt")
if d.get("tool_name") == "Artifact" and t.get("action") == "delete" and not t.get("path") and os.path.exists(ok) and t.get("url") in open(ok, encoding="utf-8").read().split():
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "allow", "permissionDecisionReason": "user approved this delete list once"}}))'
exit 0
