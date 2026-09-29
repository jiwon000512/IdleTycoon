using UnityEngine;
using ZooTycoon.Core;

namespace ZooTycoon.World
{
    // 설계 25: 곳 화면(빵집·광장·농장) 공통. WorldManager가 웜뱃이 있는 곳의 화면에 카메라·편집 모드를 넘긴다
    public interface IAreaView
    {
        // 곳 원점(월드). 곳 좌표 = 월드 − 원점
        Vector3 Origin { get; }
        Transform Wombat { get; }
        // 카메라 경계
        Rect Bounds { get; }
        void SetEditing(bool editing);
        // 잡은 사물(null이면 없음)의 외곽선을 노랗게
        void SetHeld(IPlaced held);
        // 끌고 있는 사물의 그림자(놓을 수 있으면 초록, 아니면 빨강)
        void ShowGhost(IPlacedKind kind, System.Numerics.Vector2 at, bool ok);
        void HideGhost();
    }
}
