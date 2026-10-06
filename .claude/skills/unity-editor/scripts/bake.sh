#!/usr/bin/env bash
# 굽기 한 방: 잠금 → 플레이 끄기 → 메뉴(기본 Bakery) → 알려진 부산물 되돌리기 → 새로고침 → 바뀐 프리팹 · 그림 요약
# 사용: bake.sh [Bakery] [Fonts] [UiSprites] [VisitorSheets]
# 부산물: 다시 구우면 fileID만 바뀌는 파일(CoinPopup.prefab · 가게 손님 시트 meta). 굽기 전에 깨끗했던 것만 되돌린다
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
lock_take bake || exit 1
stop_play
wait_idle
cd "$ROOT"
side='ProjectTycoon/Assets/Prefabs/CoinPopup.prefab ProjectTycoon/Assets/Sprites/World/Shop/*.meta'
clean=$(for f in $side; do git diff --quiet -- "$f" 2>/dev/null && echo "$f"; done)
for m in "${@:-Bakery}"; do
  r=$(unity cmd run_script --file "$ROOT/.claude/skills/unity-editor/scripts/Bake.cs" --entry "Bake.$m" --mode ephemeral 2>&1 | tail -1 | grep -o '"result":"[^"]*"')
  # 30초 한도를 넘기면 도구만 먼저 돌아오고 굽기는 메인 스레드에서 계속된다 → 에디터가 다시 답할 때까지 기다린다
  if [ -z "$r" ]; then
    for i in $(seq 1 20); do unity cmd eval --code 'return "ok";' 2>&1 | tail -1 | grep -q '"result":"ok"' && break; sleep 3; done
    r="(한도 넘김, 끝까지 기다림) $m"
  fi
  echo "$r"
done
reverted=0
for f in $clean; do git diff --quiet -- "$f" || { git checkout -q -- "$f"; reverted=$((reverted + 1)); }; done
cd "$ROOT/ProjectTycoon"
unity cmd eval --code 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
wait_idle
echo "부산물 되돌림 $reverted개. 바뀐 것:"
git -C "$ROOT" status --short -- ProjectTycoon/Assets/Prefabs ProjectTycoon/Assets/Sprites ProjectTycoon/Assets/Resources/UI | head -20
console_report
lock_free
