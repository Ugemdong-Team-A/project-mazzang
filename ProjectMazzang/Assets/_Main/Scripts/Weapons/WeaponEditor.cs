#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Weapon), true), CanEditMultipleObjects]
public sealed class WeaponEditor : Editor
{
    private static readonly IReadOnlyDictionary<string, string>
        KoreanLabels =
            new Dictionary<string, string>
            {
                ["defaultAttack"] = "단일 방향 공격",
                ["noDirectionAttack"] = "방향 입력 없을 때",
                ["sideAttack"] = "좌우 입력할 때",
                ["downAttack"] = "아래 입력할 때",
                ["primaryAction"] = "주 공격 연출",
                ["secondaryAction"] = "보조 공격 연출",
                ["attack"] = "공격 데이터",
                ["bashAttack"] = "밀치기 공격 데이터",
                ["hitboxSize"] = "판정 크기",
                ["hitboxForwardOffset"] = "판정 앞 거리",
                ["hurtboxLayer"] = "피격 대상 레이어",
                ["dash"] = "돌진 데이터",
                ["dashSpeed"] = "돌진 속도",
                ["dashControlLock"] = "돌진 조작 제한 시간",
                ["cooldown"] = "재사용 대기시간",
                ["sharedCooldown"] = "공용 재사용 대기시간",
                ["magazineSize"] = "탄창 크기",
                ["fireInterval"] = "발사 간격",
                ["pelletCount"] = "산탄 수",
                ["spreadAngle"] = "퍼짐 각도",
                ["muzzle"] = "발사 위치",
                ["projectilePrefab"] = "투사체 원본",
                ["projectileSpeed"] = "투사체 속도",
                ["projectileLifetime"] = "투사체 수명",
                ["muzzleFlash"] = "총구 섬광",
                ["audioSource"] = "소리 재생기",
                ["fireClip"] = "발사 소리",
                ["fireShakeProfile"] = "발사 화면 흔들림",
                ["parryDuration"] = "패링 유지시간",
                ["parryRadius"] = "패링 반경",
                ["parryArcAngle"] = "패링 각도",
                ["parryAimInfluence"] = "패링 조준 반영량",
                ["parrySpeedMultiplier"] = "패링 속도 배율",
                ["parryForwardOffset"] = "패링 앞 거리",
                ["bashEffectForwardOffset"] = "밀치기 효과 앞 거리",
                ["bashShakeProfile"] = "밀치기 화면 흔들림",
                ["parrySuccessShakeProfile"] = "패링 성공 화면 흔들림"
            };

    private static readonly HashSet<string> BaseProperties =
        new()
        {
            "m_Script",
            "rb",
            "worldCollider",
            "pickupTrigger",
            "stanceDirection",
            "stanceAnimation",
            "primaryDirection",
            "secondaryDirection",
            "defaultAttack",
            "noDirectionAttack",
            "sideAttack",
            "downAttack",
            "primaryAction",
            "secondaryAction",
            "sortingGroup",
            "presentationTemplate",
            "visualSizeOffset"
        };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScript();
        DrawStance();
        DrawAttackDirections();
        DrawPresentation();
        DrawWeaponSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScript()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(
                    "m_Script"));
        }
    }

    private void DrawStance()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "장착 중 기본 자세",
            EditorStyles.boldLabel);

        SerializedProperty direction =
            serializedObject.FindProperty(
                "stanceDirection");

        DrawPopup(
            direction,
            "바라보는 기준",
            "무기를 들고 공격하지 않을 때 캐릭터가 바라보는 기준입니다.",
            "마우스 조준",
            "이동 방향");

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(
                "stanceAnimation"),
            new GUIContent(
                "기본 자세 애니메이션",
                "비어 있으면 캐릭터의 일반 Idle을 그대로 사용합니다."));

        if (direction != null &&
            direction.enumValueIndex ==
                (int)WeaponStanceDirection.Facing)
        {
            EditorGUILayout.HelpBox(
                "이동 입력으로 좌우를 정하며, 평상시에는 마우스 조준과 상체 CCD를 사용하지 않습니다.",
                MessageType.Info);
        }
    }

    private void DrawAttackDirections()
    {
        DrawAttackDirection(
            "주 공격",
            "primaryDirection",
            WeaponButton.Primary);

        Weapon weapon =
            target as Weapon;

        if (weapon != null &&
            weapon.ConsumesParryInput)
        {
            DrawAttackDirection(
                "보조 공격",
                "secondaryDirection",
                WeaponButton.Secondary);
        }
    }

    private void DrawAttackDirection(
        string title,
        string propertyName,
        WeaponButton button)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            title,
            EditorStyles.boldLabel);

        SerializedProperty direction =
            serializedObject.FindProperty(
                propertyName);

        DrawPopup(
            direction,
            "입력 방식",
            "입력 순간 판정과 애니메이션에 함께 사용할 방향을 정합니다.",
            "마우스 정밀 조준",
            "바라보는 좌우",
            "브라울할라식");

        bool brawlhalla =
            direction != null &&
            direction.enumValueIndex ==
                (int)WeaponAttackDirection.Brawlhalla;

        bool hasDirectionalSlots =
            button == WeaponButton.Primary &&
            serializedObject.FindProperty(
                "defaultAttack") != null;

        if (brawlhalla &&
            hasDirectionalSlots)
        {
            EditorGUILayout.HelpBox(
                "위 입력 또는 방향 입력이 없으면 '방향 입력 없을 때', 좌우 입력은 '좌우 입력할 때', 아래 입력은 '아래 입력할 때' 슬롯을 사용합니다.",
                MessageType.Info);
        }
        else if (brawlhalla)
        {
            EditorGUILayout.HelpBox(
                "이 무기는 아직 방향별 행동 슬롯을 사용하지 않습니다. 어느 방향에서도 아래의 같은 공격 데이터를 사용합니다.",
                MessageType.Info);
        }

        DrawAttackSlots(
            button,
            direction);
    }

    private void DrawAttackSlots(
        WeaponButton button,
        SerializedProperty direction)
    {
        if (button == WeaponButton.Secondary)
        {
            DrawPropertyIfExists(
                "secondaryAction");
            return;
        }

        if (serializedObject.FindProperty(
                "defaultAttack") == null)
        {
            DrawPropertyIfExists(
                "primaryAction");
            return;
        }

        bool brawlhalla =
            direction != null &&
            direction.enumValueIndex ==
                (int)WeaponAttackDirection.Brawlhalla;

        if (!brawlhalla)
        {
            DrawPropertyIfExists(
                "defaultAttack");
            return;
        }

        DrawPropertyIfExists(
            "noDirectionAttack");
        DrawPropertyIfExists(
            "sideAttack");
        DrawPropertyIfExists(
            "downAttack");
    }

    private void DrawPresentation()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "월드와 장착 모습",
            EditorStyles.boldLabel);

        DrawProperty(
            "rb",
            "물리 본체");
        DrawProperty(
            "worldCollider",
            "바닥 충돌체");
        DrawProperty(
            "pickupTrigger",
            "획득 범위");
        DrawProperty(
            "sortingGroup",
            "스프라이트 묶음");
        DrawProperty(
            "presentationTemplate",
            "장착 모습 원본");
        DrawProperty(
            "visualSizeOffset",
            "월드 모습 크기");
    }

    private void DrawWeaponSettings()
    {
        SerializedProperty property =
            serializedObject.GetIterator();

        bool enterChildren = true;
        bool drewHeader = false;

        while (property.NextVisible(
                   enterChildren))
        {
            enterChildren = false;

            if (BaseProperties.Contains(
                    property.name) ||
                property.name.StartsWith("_"))
            {
                continue;
            }

            if (!drewHeader)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    "무기별 설정",
                    EditorStyles.boldLabel);
                drewHeader = true;
            }

            GUIContent label =
                KoreanLabels.TryGetValue(
                    property.name,
                    out string koreanLabel)
                    ? new GUIContent(
                        koreanLabel,
                        property.tooltip)
                    : new GUIContent(
                        property.displayName,
                        property.tooltip);

            EditorGUILayout.PropertyField(
                property,
                label,
                true);
        }
    }

    private void DrawPropertyIfExists(
        string propertyName)
    {
        SerializedProperty property =
            serializedObject.FindProperty(
                propertyName);

        if (property == null)
            return;

        string label =
            KoreanLabels.TryGetValue(
                propertyName,
                out string koreanLabel)
                ? koreanLabel
                : property.displayName;

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(
                label,
                property.tooltip),
            true);
    }

    private void DrawProperty(
        string propertyName,
        string label)
    {
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(
                propertyName),
            new GUIContent(label));
    }

    private static void DrawPopup(
        SerializedProperty property,
        string label,
        string tooltip,
        params string[] options)
    {
        if (property == null)
            return;

        EditorGUI.showMixedValue =
            property.hasMultipleDifferentValues;

        EditorGUI.BeginChangeCheck();

        int selected =
            EditorGUILayout.Popup(
                new GUIContent(
                    label,
                    tooltip),
                property.enumValueIndex,
                options);

        if (EditorGUI.EndChangeCheck())
        {
            property.enumValueIndex =
                selected;
        }

        EditorGUI.showMixedValue = false;
    }
}

#endif
