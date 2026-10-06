#!/usr/bin/env bash
# 플레이를 끄고 → 에셋 새로고침 → 재컴파일 → 콘솔 오류·경고를 출력한다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_take compile || exit 1
stop_play
unity cmd clear_console >/dev/null 2>&1
unity cmd eval --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
wait_idle
unity cmd recompile >/dev/null 2>&1
for i in $(seq 1 80); do
  s=$(unity cmd recompile_status 2>&1 | tail -1)
  echo "$s" | grep -qE '"(completed|up_to_date)"' && break
  sleep 3
done
wait_idle; sleep 2
console_report
lock_free
