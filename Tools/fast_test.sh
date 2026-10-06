#!/usr/bin/env bash
# 자동화 C: Unity 밖 빠른 테스트(Core · Data · GameKit.Core + EditMode 테스트 대부분). 에디터를 잡지 않는다.
# 빠지는 것: FontAssetTests · BigNumberFormatterTests(Unity · UI 필요), Settle_MatchesSimulation(씨앗 여섯 평균 ±10% 통계 대조 —
# 런타임마다 객체 해시 순서가 달라 dotnet에서는 14% 벗어난다. Unity 테스트에서는 그대로 돈다)
# 사용: bash Tools/fast_test.sh [추가 dotnet test 인자(예: --filter Name~Restaurant)]
cd "$(dirname "${BASH_SOURCE[0]}")/FastTests" || exit 1
filter="FullyQualifiedName!~Settle_MatchesSimulation"
for arg in "$@"; do
  if [ "$prev" = "--filter" ]; then filter="($filter)&($arg)"; fi
  prev="$arg"
done
args=()
skip=0
for arg in "$@"; do
  if [ $skip = 1 ]; then skip=0; continue; fi
  if [ "$arg" = "--filter" ]; then skip=1; continue; fi
  args+=("$arg")
done
dotnet test --nologo -v q --filter "$filter" "${args[@]}" 2>&1 | grep -E "통과|실패|Passed|Failed|error|오류|Expected|But was|at ZooTycoon" | grep -vE "^\s+통과 " | tail -40
