#!/usr/bin/env bash
# 플레이를 끄고 임시 캡처 폴더(Assets/Temp)를 지운다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
stop_play
unity cmd eval --code 'UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp"); return "ok";' >/dev/null 2>&1
status | grep -o '"playMode":"[a-z]*"'

