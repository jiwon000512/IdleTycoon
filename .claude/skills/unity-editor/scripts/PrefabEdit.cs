using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 틀: scratchpad에 복사해 k_Path와 Edit 안만 고쳐 run_script로 돌린다(--entry PrefabEdit.Run)
public static class PrefabEdit
{
    const string k_Path = "Assets/Resources/UI/ClerkPopupView.prefab";

    static void Edit(GameObject root)
    {
        RectTransform target = (RectTransform)root.transform.Find("Popup/Panel");
        target.sizeDelta = new Vector2(target.sizeDelta.x, 1200f);

        // 직렬화 필드 연결 예:
        // SerializedObject so = new SerializedObject(root.GetComponent<ZooTycoon.UI.ClerkPopupView>());
        // so.FindProperty("m_field").objectReferenceValue = something;
        // so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static string Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(k_Path);
        Edit(root);
        PrefabUtility.SaveAsPrefabAsset(root, k_Path);
        PrefabUtility.UnloadPrefabContents(root);
        return "ok";
    }
}
