#!/usr/bin/env bash
# 날짜 알림(UserPromptSubmit): 그 세션에서 날짜가 바뀐 첫 명령이면 넘김 목록에 남은 것을 맥락에 넣는다.
# 새벽 0~5시는 전날로 본다(자정을 넘겨 이어 하는 작업). 「(완료)」 아티팩트는 지우지 않고 재활용한다(workflow 아티팩트 절).
sid=$(cat | grep -o '"session_id":"[^"]*"' | cut -d'"' -f4)
STATE="$(dirname "${BASH_SOURCE[0]}")/../state"
mkdir -p "$STATE"
today=$(date -d '-6 hours' +%F)
last=$(cat "$STATE/day-$sid" 2>/dev/null || cat "$STATE/last-day" 2>/dev/null)
echo "$today" > "$STATE/day-$sid"
echo "$today" > "$STATE/last-day"
[ -n "$last" ] && [ "$last" != "$today" ] || exit 0
pending=$(python3 "$STATE/../../Tools/handoff.py" list 2>/dev/null)
[ -n "$pending" ] && echo "날짜가 바뀌었다($last → $today). 넘김 목록에 남은 것: $pending"
exit 0
