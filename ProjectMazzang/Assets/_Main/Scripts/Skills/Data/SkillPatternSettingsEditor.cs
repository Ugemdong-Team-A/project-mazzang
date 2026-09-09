#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillPatternOptions), true)]
public sealed class SkillPatternOptionsDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;
        foreach (SerializedProperty child in Children(property))
            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        return height;
    }

    public override void OnGUI(
    Rect position,
    SerializedProperty property,
    GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var enabled = property.FindPropertyRelative("enabled");

        const float HeaderHeight = 18f;
        const float FoldoutWidth = 16f;
        const float ToggleWidth = 16f;
        const float Gap = 1f;

        var header = new Rect(
            position.x,
            position.y,
            position.width,
            HeaderHeight);

        // ---------------------------------------------------------
        // Volume 느낌의 Header Background
        // ---------------------------------------------------------

        Color headerColor = EditorGUIUtility.isProSkin
            ? new Color(0.19f, 0.19f, 0.19f)
            : new Color(0.76f, 0.76f, 0.76f);

        Color borderColor = EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.12f, 0.12f)
            : new Color(0.58f, 0.58f, 0.58f);

        EditorGUI.DrawRect(header, headerColor);

        // 위/아래 1px 경계선
        EditorGUI.DrawRect(
            new Rect(header.x, header.y, header.width, 1f),
            borderColor);

        EditorGUI.DrawRect(
            new Rect(header.x, header.yMax - 1f, header.width, 1f),
            borderColor);

        // ---------------------------------------------------------
        // Foldout / Toggle / Label
        // ---------------------------------------------------------

        var foldoutRect = new Rect(
            header.x + 2f,
            header.y,
            FoldoutWidth,
            header.height);

        var toggleRect = new Rect(
            foldoutRect.xMax,
            header.y + 1f,
            ToggleWidth,
            header.height - 2f);

        var labelRect = new Rect(
            toggleRect.xMax + Gap,
            header.y,
            header.xMax - toggleRect.xMax - Gap - 2f,
            header.height);

        // 화살표
        property.isExpanded = EditorGUI.Foldout(
            foldoutRect,
            property.isExpanded,
            GUIContent.none,
            false);

        // 활성화 체크
        EditorGUI.PropertyField(
            toggleRect,
            enabled,
            GUIContent.none);

        // 이름
        EditorGUI.LabelField(
            labelRect,
            label,
            EditorStyles.label);

        // 이름 클릭 → 열고 닫기
        Event evt = Event.current;

        if (evt.type == EventType.MouseDown &&
            evt.button == 0 &&
            labelRect.Contains(evt.mousePosition))
        {
            property.isExpanded = !property.isExpanded;
            evt.Use();
        }

        // ---------------------------------------------------------
        // Children
        // ---------------------------------------------------------

        if (property.isExpanded)
        {
            float y =
                header.yMax +
                EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.indentLevel++;

            using (new EditorGUI.DisabledScope(
                       !enabled.boolValue ||
                       enabled.hasMultipleDifferentValues))
            {
                foreach (SerializedProperty child in Children(property))
                {
                    float height =
                        EditorGUI.GetPropertyHeight(child, true);

                    var source =
                        property.FindPropertyRelative("source");

                    bool fromBehavior =
                        child.name == "seconds" &&
                        source != null &&
                        !source.hasMultipleDifferentValues &&
                        source.enumValueIndex ==
                        (int)SkillDurationSource.Behavior;

                    var rechargeMode = property.FindPropertyRelative("rechargeMode");
                    bool knownRecharge = rechargeMode != null && !rechargeMode.hasMultipleDifferentValues;
                    bool meterRecharge = knownRecharge &&
                        rechargeMode.intValue == (int)SkillChargeRechargeMode.Meter;
                    bool unusedChargeSetting = knownRecharge &&
                        (meterRecharge
                            ? child.name == "initialCharges" || child.name == "rechargeDuration"
                            : child.name == "meterRechargePolicy");
                    var consumeMode = property.FindPropertyRelative("consumeMode");
                    bool unusedCost = child.name == "cost" && consumeMode != null &&
                        !consumeMode.hasMultipleDifferentValues &&
                        consumeMode.intValue != (int)SkillMeterConsumeMode.Cost;

                    using (new EditorGUI.DisabledScope(fromBehavior || unusedChargeSetting || unusedCost))
                    {
                        EditorGUI.PropertyField(
                            new Rect(
                                position.x,
                                y,
                                position.width,
                                height),
                            child,
                            true);
                    }

                    y +=
                        height +
                        EditorGUIUtility.standardVerticalSpacing;
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }
    private static System.Collections.Generic.IEnumerable<SerializedProperty> Children(SerializedProperty property)
    {
        var child = property.Copy();
        var end = property.GetEndProperty();
        if (!child.NextVisible(true)) yield break;
        do
        {
            if (SerializedProperty.EqualContents(child, end)) yield break;
            if (child.name != "enabled") yield return child.Copy();
        } while (child.NextVisible(false));
    }
}

[CustomEditor(typeof(SkillData), true), CanEditMultipleObjects]
public sealed class SkillPatternSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "스킬의 공통 정보와 필요한 기능만 켜서 사용합니다. " +
            "꺼진 기능의 값은 보존되지만 게임에는 적용되지 않습니다.",
            MessageType.Info);

        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            "기본 정보",
            EditorStyles.boldLabel);
        DrawProperty("cooldown", "재사용 대기시간");
        DrawProperty("icon", "아이콘");
        DrawProperty("animation", "행동 애니메이션");

        EditorGUILayout.Space(6);
        DrawProperty("patterns", "선택 기능", true);

        DrawSkillSpecificProperties();
        serializedObject.ApplyModifiedProperties();

        foreach (Object item in targets)
        {
            var data = (SkillData)item;
            if (!data.ValidatePatterns(out string error))
                EditorGUILayout.HelpBox(data.name + ": " + error, MessageType.Error);
        }
    }

    private void DrawProperty(
        string propertyName,
        string displayName,
        bool includeChildren = false)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
            return;

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(displayName),
            includeChildren);
    }

    private void DrawSkillSpecificProperties()
    {
        SerializedProperty iterator =
            serializedObject.GetIterator();
        bool enterChildren = true;
        bool drewHeading = false;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (IsCommonProperty(iterator.name))
                continue;

            if (!drewHeading)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(
                    "스킬 고유 설정",
                    EditorStyles.boldLabel);
                drewHeading = true;
            }

            EditorGUILayout.PropertyField(
                iterator,
                true);
        }
    }

    private static bool IsCommonProperty(
        string propertyName)
    {
        return propertyName == "m_Script" ||
               propertyName == "cooldown" ||
               propertyName == "icon" ||
               propertyName == "animation" ||
               propertyName == "patterns";
    }
}
#endif
