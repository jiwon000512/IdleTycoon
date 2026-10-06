#!/usr/bin/env bash
# 빠른 테스트 게이트: 이 세션이 Core · Data · 테스트 .cs를 고쳤으면 턴을 끝내기 전에 Tools/fast_test.sh(10초)를 돌린다.
#   mark  (PostToolUse Edit|Write|MultiEdit): 그런 .cs면 세션 표시를 남긴다
#   check (Stop): 표시가 있으면 테스트. 실패면 exit 2로 첫 실패를 돌려줘 계속 고치게 한다(세 번까지, 그 뒤는 사람에게 넘긴다).
#         통과면 표시를 지운다. 통과했는데 테스트 수가 지난번보다 줄었으면 한 번 막는다(분모 바꾸기 대비)
input=$(cat)
eval "$(printf '%s' "$input" | python3 -c '
import sys, json, shlex
sys.stdout.reconfigure(encoding="utf-8")
d = json.loads(sys.stdin.buffer.read().decode("utf-8")); t = d.get("tool_input") or {}
for k, v in (("sid", d.get("session_id")), ("path", t.get("file_path"))):
    print(k + "=" + shlex.quote(v or ""))')"
ROOT="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
STATE="$ROOT/.claude/state"
MARK="$STATE/fasttest-$sid"

case "$1" in
  mark)
    case "$path" in *Assets*Scripts*Core*.cs|*Assets*Scripts*Data*.cs|*Assets*Tests*EditMode*.cs|*UnityGameKit*Runtime*Core*.cs) ;; *) exit 0 ;; esac
    mkdir -p "$STATE"
    [ -f "$MARK" ] || echo 0 > "$MARK"
    exit 0 ;;
  check)
    [ -f "$MARK" ] || exit 0
    out=$(bash "$ROOT/Tools/fast_test.sh" 2>&1)
    line=$(printf '%s\n' "$out" | grep -E "(통과|실패|Passed|Failed)!" | tail -1)
    total=$(printf '%s' "$line" | grep -oE '(전체|Total): *[0-9]+' | grep -oE '[0-9]+')
    if printf '%s' "$line" | grep -qE "(통과|Passed)!"; then
      rm -f "$MARK"
      base=$(cat "$STATE/fasttest-total" 2>/dev/null || echo 0)
      echo "$total" > "$STATE/fasttest-total"
      if [ "$total" -lt "$base" ]; then
        echo "빠른 테스트는 통과했지만 테스트 수가 $base → $total로 줄었다. 일부러 지운 것인지 확인하고 보고에 적는다" >&2
        exit 2
      fi
      exit 0
    fi
    n=$(cat "$MARK")
    if [ "$n" -ge 3 ]; then
      rm -f "$MARK"
      echo '{"systemMessage":"빠른 테스트가 세 번 실패한 채로 턴이 끝났다(Tools/fast_test.sh)"}'
      exit 0
    fi
    echo $((n + 1)) > "$MARK"
    { echo "빠른 테스트 실패(이 세션이 Core · Data · 테스트 .cs를 고쳤다). 고친 뒤 끝낸다:"; printf '%s\n' "$out" | head -25; } >&2
    exit 2 ;;
esac
