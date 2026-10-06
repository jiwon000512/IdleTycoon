#!/usr/bin/env bash
# 플레이를 끄고 EditMode 테스트를 한 번 돌려 요약과 실패 목록을 출력한다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_take test || exit 1
stop_play
wait_idle
unity cmd run_tests --json --timeout 300 2>&1 | python3 -c "
import sys, json
d = json.load(sys.stdin); r = d.get('data', d).get('result', d)
print(r['Summary'])
for t in r['Results']:
    if t['Status'] != 'Passed':
        print(t['FullName']); print((t['Message'] or '')[:500].encode('ascii', 'replace').decode())"
unity cmd console_status 2>&1 | tail -1 | grep -o '"consoleErrors":[0-9]*,"consoleWarnings":[0-9]*'
lock_free
