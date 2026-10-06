#!/usr/bin/env bash
# 사용: capture.sh <이름> [출력 폴더]   세로 1080×1920 캡처를 찍어 파일 경로를 출력한다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_use || exit 1
name="${1:-shot}"; out="${2:-$ROOT/ProjectTycoon/Temp}"
unity cmd capture_game_view --source camera --width 1080 --height 1920 --save_path "Temp/$name.png" >/dev/null 2>&1
mkdir -p "$out"
cp "Assets/Temp/$name.png" "$out/$name.png" && echo "$out/$name.png"
unity cmd console_status 2>&1 | tail -1 | grep -o '"consoleErrors":[0-9]*,"consoleWarnings":[0-9]*'
