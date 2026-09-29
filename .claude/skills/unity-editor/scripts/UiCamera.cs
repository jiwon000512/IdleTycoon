using UnityEngine;

// 플레이 중: 모든 루트 캔버스를 카메라 공간으로 돌린다(--source camera 세로 캡처에 UI가 찍히게). 배경 실행은 프로젝트 설정으로 늘 켜져 있다
public static class UiCamera
{
    public static string Run()
    {
        int n = 0;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode == RenderMode.WorldSpace) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = Camera.main;
            c.planeDistance = 1f;
            c.sortingOrder = 5000;
            n++;
        }
        return "canvases " + n;
    }
}
