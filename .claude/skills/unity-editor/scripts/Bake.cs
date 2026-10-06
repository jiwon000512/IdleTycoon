// bake.sh가 run_script로 부른다(굽기 메뉴는 eval의 5초 한도보다 길다). 다음 틱으로 미루지 않는다:
// 쉬는 에디터에서는 미룬 일이 다른 방의 다음 명령 때(플레이 중일 수도) 깨어난다(2026-10-06)
public static class Bake
{
    public static string Bakery() { return Run("ZooTycoon/Bake/Bakery"); }
    public static string Fonts() { return Run("ZooTycoon/Bake/Fonts"); }
    public static string UiSprites() { return Run("ZooTycoon/Bake/Import UI Sprites"); }
    public static string VisitorSheets() { return Run("ZooTycoon/Bake/Import Visitor Sheets"); }

    private static string Run(string menu)
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return "playing, skipped " + menu;
        }

        return (UnityEditor.EditorApplication.ExecuteMenuItem(menu) ? "ok " : "no menu ") + menu;
    }
}
