#!/usr/bin/env bash
# 검증 플레이 한 줄 명령(플레이 중에만, play.sh 뒤). Editor/DevPlay.cs를 eval로 부른다
#   dev.sh status                     곳 · 코인 · 웜뱃 자리 · 손 · 대상 한 줄
#   dev.sh cheats | cheat <줄> <버튼>  치트 창 목록 · 실행(예: cheat "곳 이동" "횟집")
#   dev.sh near <사물 id> [n]          지금 곳의 그 사물 n번째 일하는 자리에 웜뱃을 세운다
#   dev.sh rect <사물 id> [n]          그 사물 둘레를 캡처 픽셀 "x y w h"로(shots.py crop에 넘긴다)
#   dev.sh scale <배속>                Time.timeScale
#   dev.sh wait '<C# bool 식>' [초]    참이 될 때까지(기본 30초). @는 ZooTycoon.Editor.DevPlay.(@M = Mall, @G = GameManager)
#   dev.sh '<C# 문장들; return ...;>'  그 밖은 그대로 eval(@ 줄임 같음)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_use || exit 1
D=ZooTycoon.Editor.DevPlay
ev() { unity cmd eval --code "${1//@/$D.}" --json 2>&1 | python3 -c "
import sys, json
sys.stdout.reconfigure(encoding='utf-8')
try:
    d = json.loads(sys.stdin.buffer.read().decode('utf-8'))
    while isinstance(d, dict) and ('data' in d or 'result' in d): d = d.get('data', d.get('result'))
    print(d)
except Exception:
    print('eval failed (playing?)')"; }
q() { printf '"%s"' "${1//\"/\\\"}"; }
case "$1" in
  status) ev "return @Status();" ;;
  cheats) ev "return @Cheats();" ;;
  cheat) ev "return @Cheat($(q "$2"), $(q "$3"));" ;;
  near) ev "return @Near($(q "$2"), ${3:-0});" ;;
  rect) ev "return @Rect($(q "$2"), ${3:-0});" ;;
  scale) ev "UnityEngine.Time.timeScale = ${2}f; return \"x$2\";" ;;
  wait)
    for i in $(seq 1 "${3:-30}"); do
      [ "$(ev "return ($2) ? \"yes\" : \"no\";")" = yes ] && { ev "return @Status();"; exit 0; }
      sleep 1
    done
    echo "시간 넘김: $2"; ev "return @Status();"; exit 1 ;;
  *) ev "$1" ;;
esac
