#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// 개발자가 Character Root에서 표준 제작 단계를 한 번에 실행하는 Inspector.
/// </summary>
[CustomEditor(typeof(Standard2DCharacterSetup))]
public sealed class Standard2DCharacterSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Standard2DCharacterSetup setup =
            (Standard2DCharacterSetup)target;

        EditorGUILayout.HelpBox(
            $"Standard 2D Character Setup v{Standard2DCharacterSetup.ToolVersion}\n\n" +
            "Animator가 있는 Character Root 전용입니다.\n" +
            "현재 구성 대상:\n" +
            "• 표준 2D IK 생성 / 갱신\n" +
            "• 모든 SpriteRenderer에 SpriteResolver 추가\n" +
            "• 모든 SpriteResolver에 Visual Driver 추가 / 갱신\n\n" +
            "부위 이름과 Sprite Library Asset Category 규격: lower_snake_case\n" +
            "예: arm_l, leg_r, hair_front\n\n" +
            "SpriteLibraryAsset은 나중에 연결해도 됩니다.\n" +
            "RAP, Socket 등은 이후 독립된 제작 단계로 추가할 수 있습니다.",
            MessageType.Info);

        EditorGUILayout.Space(6);

        DrawReferenceStatus(
            "Animator",
            setup.Animator,
            setup.Animator != null &&
            setup.Animator.gameObject == setup.gameObject);

        DrawReferenceStatus(
            "IK Setup",
            setup.RigIKSetup,
            setup.RigIKSetup != null &&
            setup.RigIKSetup.gameObject == setup.gameObject);

        DrawOptionalReferenceStatus(
            "Sprite Library",
            setup.SpriteLibrary);

        EditorGUILayout.LabelField(
            setup.SpriteResolvers.Count > 0
                ? $"✓ Sprite Resolvers ({setup.SpriteResolvers.Count})"
                : "✕ Sprite Resolvers (0)");

        EditorGUILayout.Space(10);

        GUIStyle buildStyle =
            new(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

        if (GUILayout.Button(
                "Build / Refresh Character",
                buildStyle,
                GUILayout.Height(40)))
        {
            Standard2DCharacterBuilder.BuildOrRefresh(
                setup);
        }

        EditorGUILayout.Space(3);

        if (GUILayout.Button(
                "Validate Character",
                GUILayout.Height(26)))
        {
            Standard2DCharacterBuilder.Validate(
                setup);
        }
    }

    private static void DrawReferenceStatus(
        string label,
        Object reference,
        bool valid)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                valid ? $"✓ {label}" : $"✕ {label}",
                reference,
                typeof(Object),
                true);
        }
    }

    private static void DrawOptionalReferenceStatus(
        string label,
        Object reference)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                reference != null
                    ? $"✓ {label}"
                    : $"△ {label} (선택)",
                reference,
                typeof(Object),
                true);
        }
    }
}

#endif
