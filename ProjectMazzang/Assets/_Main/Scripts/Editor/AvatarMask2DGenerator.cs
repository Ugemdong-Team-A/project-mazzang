using UnityEditor;
using UnityEngine;

public static class AvatarMask2DGenerator
{
    [MenuItem("Tools/2D Animation/Create Mask From Selected")]
    private static void CreateMask()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("캐릭터 루트를 선택해주세요.");
            return;
        }

        AvatarMask mask = new AvatarMask();

        // 선택한 Root와 모든 자식 Transform을 Mask에 등록
        mask.AddTransformPath(selected.transform, true);

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Avatar Mask",
            "2D_AvatarMask",
            "mask",
            "Avatar Mask 저장 위치를 선택하세요.");

        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(mask, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = mask;
    }
}
