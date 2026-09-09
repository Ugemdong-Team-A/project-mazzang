#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionAnimationData), true)]
[CanEditMultipleObjects]
public sealed class ActionAnimationDataEditor : Editor
{
    private static readonly string[] BodyMaskNames =
    {
        "전신",
        "상체",
        "팔만"
    };

    private static readonly string[] AimCompositionNames =
    {
        "조준으로 애니메이션 덮어쓰기",
        "애니메이션 그대로",
        "애니메이션과 허리 조준 섞기"
    };

    private static readonly string[] HandIkPolicyNames =
    {
        "현재 방식 유지",
        "애니메이션 손 위치",
        "무기 손잡이"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "공격·무기·스킬이 공유하는 행동 애니메이션입니다. " +
            "클립이 비어 있는 단계는 재생하지 않습니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(4);
        DrawPhase(
            "cast",
            "준비 동작",
            "공격이나 스킬이 실행되기 전의 동작입니다.");
        DrawPhase(
            "release",
            "실행 동작",
            "실제 공격이나 효과가 실행되는 동작입니다.");
        DrawPhase(
            "recovery",
            "마무리 동작",
            "실행 후 기본 자세로 돌아가는 동작입니다.");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPhase(
        string propertyName,
        string displayName,
        string description)
    {
        SerializedProperty phase =
            serializedObject.FindProperty(propertyName);

        if (phase == null)
            return;

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            GUIContent heading = new(
                displayName,
                EditorGUIUtility.IconContent(
                    "AnimationClip Icon").image);

            EditorGUILayout.LabelField(
                heading,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                description,
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            DrawChildProperty(
                phase,
                "clip",
                "애니메이션 클립");
            DrawEnumProperty(
                phase,
                "bodyMask",
                "적용 부위",
                "이 클립이 영향을 주는 캐릭터 부위입니다.",
                BodyMaskNames);
            DrawEnumProperty(
                phase,
                "aimComposition",
                "조준 애니메이션 혼합 모드",
                "클립 포즈와 마우스 조준을 함께 적용하는 방식입니다.",
                AimCompositionNames);
            DrawEnumProperty(
                phase,
                "handIkPolicy",
                "손 위치 기준",
                "손 IK가 따라갈 위치를 정합니다.",
                HandIkPolicyNames);

            SerializedProperty clip =
                phase.FindPropertyRelative("clip");

            if (clip != null &&
                !clip.hasMultipleDifferentValues &&
                clip.objectReferenceValue == null)
            {
                EditorGUILayout.LabelField(
                    "클립 없음: 이 단계는 재생하지 않습니다.",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(3);
    }

    private static void DrawChildProperty(
        SerializedProperty parent,
        string propertyName,
        string displayName)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
            return;

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(displayName));
    }

    private static void DrawEnumProperty(
        SerializedProperty parent,
        string propertyName,
        string displayName,
        string tooltip,
        string[] optionNames)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(propertyName);

        if (property == null)
            return;

        EditorGUI.showMixedValue =
            property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();

        int currentIndex = Mathf.Clamp(
            property.enumValueIndex,
            0,
            optionNames.Length - 1);
        int selectedIndex = EditorGUILayout.Popup(
            new GUIContent(
                displayName,
                tooltip),
            currentIndex,
            optionNames);

        if (EditorGUI.EndChangeCheck())
            property.enumValueIndex = selectedIndex;

        EditorGUI.showMixedValue = false;
    }
}
#endif
