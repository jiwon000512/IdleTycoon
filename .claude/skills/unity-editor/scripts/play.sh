#!/usr/bin/env bash
# 플레이를 새로 시작하고 캔버스를 카메라로 돌려 세로 캡처를 준비한다(배경 실행은 프로젝트 설정으로 늘 켜져 있다)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
stop_play
unity cmd clear_console >/dev/null 2>&1
unity cmd editor_play >/dev/null 2>&1
for i in $(seq 1 30); do status | grep -q '"playMode":"playing"' && break; sleep 1; done
sleep 3
unity cmd run_script --file "$ROOT/.claude/skills/unity-editor/scripts/UiCamera.cs" --entry UiCamera.Run --mode ephemeral 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|error[^}]*'
