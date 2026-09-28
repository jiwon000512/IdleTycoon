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
