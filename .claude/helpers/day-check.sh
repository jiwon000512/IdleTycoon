#!/usr/bin/env bash
# 아침 정리 알림(UserPromptSubmit): 그 세션에서 날짜가 바뀐 첫 명령이면 workflow 아티팩트 절의 정리를 먼저 하라고 맥락에 한 줄 넣는다.
# 새벽 0~5시는 전날로 본다(자정을 넘겨 이어 하는 작업). 넘김 목록에 남은 것도 함께 알린다
sid=$(cat | grep -o '"session_id":"[^"]*"' | cut -d'"' -f4)
STATE="$(dirname "${BASH_SOURCE[0]}")/../state"
mkdir -p "$STATE"
today=$(date -d '-6 hours' +%F)
last=$(cat "$STATE/day-$sid" 2>/dev/null || cat "$STATE/last-day" 2>/dev/null)
echo "$today" > "$STATE/day-$sid"
echo "$today" > "$STATE/last-day"
[ -n "$last" ] && [ "$last" != "$today" ] || exit 0
echo "날짜가 바뀌었다($last → $today): 이 명령을 하기 전에 workflow.md 아티팩트 절의 아침 정리(어제 「(완료)」 아티팩트 지우기 · 그 크롬 탭 닫기)를 한다. 지울 목록은 AskUserQuestion으로 한 번만 묻고, 승인된 주소를 .claude/state/artifact-delete-ok.txt에 한 줄씩 적은 뒤 지운다(훅이 그 주소만 창 없이 허용, 끝나면 파일 삭제)."
pending=$(python3 "$STATE/../../Tools/handoff.py" list 2>/dev/null)
[ -n "$pending" ] && echo "넘김 목록에 남은 것: $pending"
exit 0
