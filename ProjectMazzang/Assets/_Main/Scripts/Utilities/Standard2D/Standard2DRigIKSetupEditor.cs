#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// 아티스트가 실제로 보는 Inspector.
/// 복잡한 구현은 숨기고 Build 버튼 하나를 메인 동작으로 제공한다.
/// </summary>
[CustomEditor(typeof(Standard2DRigIKSetup))]
public sealed class Standard2DRigIKSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12);

        EditorGUILayout.HelpBox(
            $"Standard 2D IK v{Standard2DRigIKSetup.ToolVersion}\n\n" +
            "메인 작업은 아래 Build / Rebuild IK 버튼 하나면 됩니다.\n\n" +
            "• 실제 Skeleton root 자동 탐색\n" +
            "• PSB _1 / _2 Bone alias 대응\n" +
            "• IKManager2D 자동 추가/설정\n" +
            "• Arm 2 / Leg 2 / Foot 2 / Head 1 = Solver 7개 생성\n" +
            "• Solver는 Player Root 바로 아래\n" +
            "• Target은 각 Solver 바로 아래\n" +
            "• Effector / Target / Solver / Manager 참조 자동 연결\n" +
            "• Effector는 각 끝 본의 Local +X 방향으로 생성\n" +
            "• Reach 기본값: Arm 1.20 / Leg·Foot 1.05 / Head 1.00",
            MessageType.Info);

        Standard2DRigIKSetup setup =
            (Standard2DRigIKSetup)target;

        EditorGUILayout.Space(6);

        GUIStyle buildStyle =
            new(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };

        if (GUILayout.Button(
                "Build / Rebuild IK",
                buildStyle,
                GUILayout.Height(40)))
        {
            Standard2DIKBuilder.BuildOrRebuild(
                setup);
        }

        EditorGUILayout.Space(3);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(
                    "Validate Rig",
                    GUILayout.Height(26)))
            {
                Standard2DIKBuilder.Validate(
                    setup);
            }

            if (GUILayout.Button(
                    "Remove Generated IK",
                    GUILayout.Height(26)))
            {
                Standard2DIKBuilder.RemoveGenerated(
                    setup);
            }
        }
    }
}

#endif
