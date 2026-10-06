#!/usr/bin/env bash
# 플레이를 끄고 임시 캡처 폴더(Assets/Temp)를 지우고 에디터 잠금을 놓는다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_use || exit 1
stop_play
unity cmd eval --code 'UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp"); return "ok";' >/dev/null 2>&1
status | grep -o '"playMode":"[a-z]*"'
lock_free
