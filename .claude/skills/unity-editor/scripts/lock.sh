#!/usr/bin/env bash
# 에디터 차례 잠금 보기 · 다루기. 사용: lock.sh [show | wait | take <하는 일> | free | force]
#   show  누가 무엇을 하는 중인지   wait  다른 방 잠금이 풀릴 때까지(LOCK_WAIT초, 기본 600) 기다린다(잡지는 않는다)
#   take  스크립트 밖에서 오래 쓸 때(예: 굽기 메뉴) 잡는다   free  이 방 잠금을 놓는다   force  누구 것이든 지운다(멈춘 걸 확인했을 때만)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
case "${1:-show}" in
  show) if lock_get; then lock_say; else echo "비어 있음"; fi ;;
  wait)
    waited=0
    while lock_get && [ "$owner" != "$ME" ] && ! lock_stale; do
      [ $waited -ge "${LOCK_WAIT:-600}" ] && { echo "아직 사용 중: $(lock_say)"; exit 1; }
      sleep 5; waited=$((waited + 5))
    done
    echo "비었음" ;;
  take) lock_take "${2:?하는 일}" && lock_say ;;
  free) lock_free; echo "놓음" ;;
  force) rm -rf "$LOCK"; echo "지움" ;;
  *) sed -n '2,4p' "$0"; exit 1 ;;
esac
