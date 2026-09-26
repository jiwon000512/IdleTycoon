using UnityEditor;
using UnityEngine;

namespace ZooTycoon.Editor
{
    // 레이어 규칙(2026-09-25): 정렬축 Y는 피벗(발끝·밑변)으로 잰다. 기본값 Center(몸통 가운데)면 진열대(SortingGroup = 밑변) 바로 아래 선 웜뱃·손님이 뒤로 들어간다.
    // 프리팹을 임포트할 때마다 모든 SpriteRenderer에 강제하므로 굽기·손 편집 어느 쪽으로 만든 프리팹이든 예외가 없다(프리팹마다 검사·테스트할 필요 없음).
    // 실행 중 코드로 만드는 렌더러(PlazaView 장식)만 그 자리에서 Pivot을 준다
    public sealed class WorldSpritePostprocessor : AssetPostprocessor
    {
        private void OnPostprocessPrefab(GameObject root)
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }
}
