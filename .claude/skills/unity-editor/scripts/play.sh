#!/usr/bin/env bash
# 에디터 잠금을 잡고(stop.sh가 놓는다) 플레이를 새로 시작해 저장을 끄고 팝업을 닫고, 캔버스를 카메라로 돌려 세로 캡처를 준비한다(배경 실행은 프로젝트 설정으로 늘 켜져 있다)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_take play || exit 1
stop_play
unity cmd clear_console >/dev/null 2>&1
unity cmd editor_play >/dev/null 2>&1
for i in $(seq 1 30); do status | grep -q '"playMode":"playing"' && break; sleep 1; done
sleep 3
unity cmd run_script --file "$ROOT/.claude/skills/unity-editor/scripts/UiCamera.cs" --entry UiCamera.Run --mode ephemeral 2>&1 | tail -1 | grep -o '"result":"[^"]*"\|error[^}]*'
# 검증 플레이는 저장하지 않는다(저장 파일 오염 방지) · 돌아왔을 때 · 소식지 팝업 닫기
unity cmd eval --code 'return ZooTycoon.Editor.DevPlay.Begin();' 2>&1 | tail -1 | grep -o '"result":"[^"]*"'
