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

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var enabled = property.FindPropertyRelative("enabled");
        var toggle = new Rect(header.xMax - 20f, header.y, 20f, header.height);
        header.width -= 24f;
        property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
        EditorGUI.PropertyField(toggle, enabled, GUIContent.none);

        if (property.isExpanded)
        {
            float y = position.y + header.height + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(!enabled.boolValue || enabled.hasMultipleDifferentValues))
            {
                foreach (SerializedProperty child in Children(property))
                {
                    float height = EditorGUI.GetPropertyHeight(child, true);
                    var source = property.FindPropertyRelative("source");
                    bool fromBehavior = child.name == "seconds" && source != null &&
                        !source.hasMultipleDifferentValues &&
                        source.enumValueIndex == (int)SkillDurationSource.Behavior;
                    using (new EditorGUI.DisabledScope(fromBehavior))
                        EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), child, true);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
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
            "공통 Patterns는 전환 준비용 설정입니다. 현재 실행은 기존 스킬 필드와 인터페이스를 사용합니다.",
            MessageType.Info);
        DrawDefaultInspector();
        foreach (Object item in targets)
        {
            var data = (SkillData)item;
            if (!data.ValidatePatterns(out string error))
                EditorGUILayout.HelpBox(data.name + ": " + error, MessageType.Error);
        }
    }
}
#endif
