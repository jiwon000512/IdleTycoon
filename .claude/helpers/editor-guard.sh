#!/usr/bin/env bash
# 에디터 차례 잠금 지킴이(PreToolUse: Bash · PowerShell · Edit · Write). 잠금은 unity-editor/scripts/common.sh
# - 날 unity 명령: 다른 방이 잡고 있으면 막는다(읽기만 하는 것은 통과). 비었으면 짧은 cmd 잠금을 잡고, 이 방 것이면 손댄 시각만 고친다
# - .cs 고치기: 누구든 플레이 잠금을 잡고 있으면 막는다(플레이 중 고치면 다음 명령 때 도메인이 다시 읽혀 플레이가 깨진다)
input=$(cat)
case "$input" in *unity*|*.cs*) ;; *) exit 0 ;; esac
eval "$(printf '%s' "$input" | python3 -c '
import sys, json, shlex
sys.stdout.reconfigure(encoding="utf-8")
d = json.loads(sys.stdin.buffer.read().decode("utf-8")); t = d.get("tool_input") or {}
for k, v in (("sid", d.get("session_id")), ("agent", d.get("agent_type")), ("tool", d.get("tool_name")), ("cmd", t.get("command")), ("path", t.get("file_path"))):
    print(k + "=" + shlex.quote(v or ""))')"
# 한 탭 팀(리드 = 프로그래밍방, 팀원 = 아트방): 팀원 · 서브에이전트의 도구 호출은 session_id가 리드와 같고 agent_type이 붙는다 → 방 이름은 agent_type
ME="${agent:-$sid}"
source "$(dirname "${BASH_SOURCE[0]}")/../skills/unity-editor/scripts/common.sh"
# 에디터 스크립트는 셸 환경의 세션 ID로 잠금을 잡아 리드와 구분이 안 된다 → 팀원은 앞에 ME=<자기 agent_type>을 붙여 부른다
if [ -n "$agent" ] && [ "$tool" = Bash ]; then
  case "$cmd" in *unity-editor/scripts/*|*Tools/bake.sh*)
    case "$cmd" in *"ME=$agent "*) ;; *)
      echo "에디터 잠금: 팀원은 스크립트 앞에 ME=$agent 를 붙여 부른다(예: ME=$agent bash .claude/skills/unity-editor/scripts/play.sh)" >&2
      exit 2 ;;
    esac ;;
  esac
fi

case "$tool" in
  Edit|Write|MultiEdit)
    case "$path" in *ProjectTycoon*Assets*.cs|*UnityGameKit*.cs) ;; *) exit 0 ;; esac
    lock_get && [ "$what" = play ] || exit 0
    lock_stale && { rm -rf "$LOCK"; exit 0; }
    if [ "$owner" = "$ME" ]; then
      echo "에디터 잠금: 이 방이 플레이 중이다. .cs는 stop.sh로 플레이를 끈 뒤 고친다" >&2
    else
      echo "에디터 잠금: $(lock_say). 지금 .cs를 고치면 그쪽 플레이가 깨진다. 다른 일을 먼저 하거나 lock.sh wait 뒤에 고친다" >&2
    fi
    exit 2 ;;
esac

case "$cmd" in *"unity cmd"*|*"unity command"*|*"unity projects"*) ;; *) exit 0 ;; esac
# 스크립트는 스스로 잡고 기다린다
case "$cmd" in *unity-editor/scripts/*|*Tools/bake.sh*) exit 0 ;; esac
# 읽기만 하는 명령은 통과
writes=$(printf '%s' "$cmd" | grep -oE 'unity (cmd|command) [a-z_]+' | awk '{print $3}' | grep -vxE 'editor_status|recompile_status|console_status|console|list')
[ -z "$writes" ] && ! printf '%s' "$cmd" | grep -q 'unity projects' && exit 0
LOCK_WAIT=0 lock_use 2>/dev/null && exit 0
echo "에디터 잠금: $(lock_say). 비면 다시 한다(bash .claude/skills/unity-editor/scripts/lock.sh wait)" >&2
exit 2
