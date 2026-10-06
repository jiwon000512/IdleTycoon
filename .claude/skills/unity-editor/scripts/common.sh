# 공통: 저장소 루트·프로젝트 폴더·상태 읽기
ROOT="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
cd "$ROOT/ProjectTycoon" || exit 1
status() { unity cmd editor_status 2>&1 | tail -1; }
stop_play() {
  if status | grep -q '"playMode":"playing"'; then
    unity cmd editor_stop >/dev/null 2>&1
    for i in $(seq 1 20); do status | grep -q '"playMode":"stopped"' && break; sleep 1; done
    sleep 2
  fi
}
wait_idle() {
  for i in $(seq 1 80); do
    s=$(status)
    echo "$s" | grep -q '"compiling":false' && echo "$s" | grep -q '"domainReloadInProgress":false' && return 0
    sleep 3
  done
  return 1
}
# 에디터 차례 잠금: 두 방이 에디터 하나를 나눠 쓴다. .claude/editor.lock 폴더(mkdir이 원자적) + info(owner · what · since).
# info의 수정 시각이 마지막 손댄 때다. 오래 손대지 않은 잠금은 버려진 것으로 본다(cmd 2분 · play 20분 · 그 밖 15분).
LOCK="$ROOT/.claude/editor.lock"
ME="${ME:-${CLAUDE_CODE_SESSION_ID:-user}}"
lock_get() {
  owner=; what=; since=$(date +%s); age=0
  [ -d "$LOCK" ] || return 1
  [ -f "$LOCK/info" ] && . "$LOCK/info"
  age=$(( $(date +%s) - $(stat -c %Y "$LOCK/info" 2>/dev/null || stat -c %Y "$LOCK") ))
}
lock_say() { echo "$([ "$owner" = "$ME" ] && echo 이 방 || echo 다른 방)이 $what 중($(( ($(date +%s) - since) / 60 ))분째)"; }
lock_stale() {
  local limit=900
  case "$what" in cmd) limit=120 ;; play) limit=1200 ;; esac
  [ $age -gt $limit ] && return 0
  # 플레이 잠금인데 에디터가 멈춰 있으면(정지 버튼 · stop.sh 잊음) 1분 뒤 버린다
  [ "$what" = play ] && [ $age -gt 60 ] && status | grep -q '"playMode":"stopped"'
}
# lock_take <하는 일>: 잡힐 때까지 LOCK_WAIT초(기본 300) 기다린다. 이미 이 방 것이면 하는 일만 바꾼다
lock_take() {
  local waited=0
  while :; do
    if mkdir "$LOCK" 2>/dev/null || { lock_get && [ "$owner" = "$ME" ]; }; then
      owner="$ME"; what="$1"; since=$(date +%s); age=0
      printf 'owner=%s\nwhat=%s\nsince=%s\n' "$owner" "$what" "$since" > "$LOCK/info"
      return 0
    fi
    if lock_stale; then echo "에디터 잠금: 버려진 잠금($(lock_say))을 푼다" >&2; rm -rf "$LOCK"; continue; fi
    if [ $waited -ge "${LOCK_WAIT:-300}" ]; then
      echo "에디터 사용 중: $(lock_say). 기다리려면 bash .claude/skills/unity-editor/scripts/lock.sh wait" >&2
      return 1
    fi
    [ $waited = 0 ] && echo "에디터 사용 중: $(lock_say) — 비기를 기다린다(최대 ${LOCK_WAIT:-300}초)" >&2
    sleep 5; waited=$((waited + 5))
  done
}
# 이 방 잠금이면 손댄 시각만 고치고(플레이 잠금 유지), 아니면 짧은 cmd 잠금을 잡는다
lock_use() {
  if lock_get && [ "$owner" = "$ME" ]; then touch "$LOCK/info"; else lock_take cmd; fi
}
lock_free() { lock_get && [ "$owner" = "$ME" ] && rm -rf "$LOCK"; return 0; }
console_report() {
  unity cmd console_status 2>&1 | tail -1 | grep -o '"compilationFailed":[a-z]*\|"consoleErrors":[0-9]*,"consoleWarnings":[0-9]*'
  unity cmd console --json 2>&1 | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin); r = d['data']['result'] if 'data' in d else d
    for e in r.get('entries', [])[:10]:
        if e['level'] == 'log': continue
        tool = 'Failed to handle /api/' in e['message']
        print('(tool, not game)' if tool else e['level'], e['message'][:300].encode('ascii', 'replace').decode())
except Exception as ex:
    print('console read failed', ex)"
}
