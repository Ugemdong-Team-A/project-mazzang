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
        foreach (SerializedProperty child in VisibleChildren(property))
            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        if (GetContextHelpText(property) != null)
            height += ContextHelpHeight + EditorGUIUtility.standardVerticalSpacing;
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
                foreach (SerializedProperty child in VisibleChildren(property))
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

                    using (new EditorGUI.DisabledScope(fromBehavior))
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

                string contextHelp = GetContextHelpText(property);
                if (contextHelp != null)
                {
                    EditorGUI.HelpBox(
                        new Rect(position.x, y, position.width, ContextHelpHeight),
                        contextHelp,
                        MessageType.Info);
                }
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private const float ContextHelpHeight = 58f;

    private static string GetContextHelpText(SerializedProperty property)
    {
        SerializedProperty enabled = property.FindPropertyRelative("enabled");
        if (property.name != "meter" ||
            enabled == null ||
            !enabled.boolValue ||
            enabled.hasMultipleDifferentValues)
        {
            return null;
        }

        SerializedProperty charge = FindSibling(property, "charge");
        SerializedProperty chargeEnabled = charge?.FindPropertyRelative("enabled");
        bool usesCharge = chargeEnabled != null && chargeEnabled.boolValue &&
            !chargeEnabled.hasMultipleDifferentValues;

        if (!usesCharge)
        {
            return "설정한 자동 충전과 준 피해로 게이지가 차며, 사용 필요량에 도달하면 스킬을 사용할 수 있습니다.";
        }

        if (UsesMeterForChargeRecharge(property))
            return "게이지가 가득 차면 사용 횟수를 회복합니다.";

        SerializedProperty windowMode =
            charge.FindPropertyRelative("chargeWindowMode");
        bool usesWindow = windowMode != null &&
            !windowMode.hasMultipleDifferentValues &&
            windowMode.intValue == (int)SkillChargeWindowMode.Timed;

        return usesWindow
            ? "횟수와 게이지가 모두 있어야 처음 사용할 수 있습니다. 제한 시간 동안의 추가 사용은 횟수만 차감합니다."
            : "사용 횟수와 게이지가 모두 있어야 스킬을 사용할 수 있습니다.";
    }

    private static bool UsesMeterForChargeRecharge(SerializedProperty property)
    {
        SerializedProperty charge = FindSibling(property, "charge");
        SerializedProperty enabled = charge?.FindPropertyRelative("enabled");
        SerializedProperty rechargeMode = charge?.FindPropertyRelative("rechargeMode");

        return enabled != null && enabled.boolValue &&
               !enabled.hasMultipleDifferentValues &&
               rechargeMode != null && !rechargeMode.hasMultipleDifferentValues &&
               rechargeMode.intValue == (int)SkillChargeRechargeMode.Meter;
    }

    private static System.Collections.Generic.IEnumerable<SerializedProperty>
        VisibleChildren(SerializedProperty property)
    {
        foreach (SerializedProperty child in Children(property))
        {
            if (ShouldShowChild(property, child))
                yield return child;
        }
    }

    private static bool ShouldShowChild(
        SerializedProperty property,
        SerializedProperty child)
    {
        if (property.name == "charge")
        {
            SerializedProperty rechargeMode =
                property.FindPropertyRelative("rechargeMode");
            if (rechargeMode != null &&
                !rechargeMode.hasMultipleDifferentValues)
            {
                bool meterRecharge = rechargeMode.intValue ==
                    (int)SkillChargeRechargeMode.Meter;
                if (meterRecharge &&
                    (child.name == "initialCharges" ||
                     child.name == "rechargeDuration"))
                {
                    return false;
                }

                if (!meterRecharge && child.name == "meterRefillMode")
                    return false;
            }

            SerializedProperty windowMode =
                property.FindPropertyRelative("chargeWindowMode");
            if (windowMode != null &&
                !windowMode.hasMultipleDifferentValues &&
                windowMode.intValue == (int)SkillChargeWindowMode.Persistent &&
                (child.name == "chargeWindowDuration" ||
                 child.name == "chargeWindowRefreshMode"))
            {
                return false;
            }
        }

        if (property.name == "meter")
        {
            if (UsesMeterForChargeRecharge(property) &&
                (child.name == "requiredMeter" ||
                 child.name == "consumeMode" ||
                 child.name == "cost"))
            {
                return false;
            }

            SerializedProperty consumeMode =
                property.FindPropertyRelative("consumeMode");
            if (child.name == "cost" &&
                consumeMode != null &&
                !consumeMode.hasMultipleDifferentValues &&
                consumeMode.intValue != (int)SkillMeterConsumeMode.Cost)
            {
                return false;
            }
        }

        return true;
    }

    private static SerializedProperty FindSibling(
        SerializedProperty property,
        string siblingName)
    {
        int separator = property.propertyPath.LastIndexOf('.');
        if (separator < 0)
            return null;

        string parentPath = property.propertyPath.Substring(0, separator);
        return property.serializedObject.FindProperty(
            parentPath + "." + siblingName);
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
