#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

[Serializable]
internal sealed class WeaponAnimationAuthoringPanel : IDisposable
{
    private static readonly string[] HandModes =
    {
        "기존 애니메이션 손 위치",
        "무기 손잡이"
    };

    [SerializeField]
    private Weapon weapon;

    [SerializeField]
    private AnimationClip weaponClip;

    [SerializeField]
    private bool useWeaponGrips;

    [NonSerialized]
    private WeaponAnimationAuthoringSession _session;

    [NonSerialized]
    private string _error;

    [NonSerialized]
    private string _lastEndReason;

    public bool IsActive =>
        _session != null &&
        _session.IsActive;

    public void Draw(
        Animator character,
        AnimationClip characterClip,
        AnimationWindow animationWindow)
    {
        if (_session != null &&
            !_session.IsActive)
        {
            _lastEndReason = _session.EndReason;
            _session.Dispose();
            _session = null;
        }

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            GUIContent heading = new(
                "무기 애니메이션 편집",
                EditorGUIUtility.IconContent("Prefab Icon").image);

            EditorGUILayout.LabelField(
                heading,
                EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "비교할 캐릭터 동작",
                    characterClip,
                    typeof(AnimationClip),
                    false);
            }

            using (new EditorGUI.DisabledScope(IsActive))
            {
                EditorGUI.BeginChangeCheck();

                weapon =
                    (Weapon)EditorGUILayout.ObjectField(
                        "무기 프리팹",
                        weapon,
                        typeof(Weapon),
                        false);
                weaponClip =
                    (AnimationClip)EditorGUILayout.ObjectField(
                        "녹화할 무기 클립",
                        weaponClip,
                        typeof(AnimationClip),
                        false);
                useWeaponGrips =
                    EditorGUILayout.Popup(
                        new GUIContent(
                            "손 위치 미리보기",
                            "무기 손잡이를 선택하면 양손 IK가 무기의 Grip을 따라갑니다. " +
                            "이 설정은 제작 미리보기에만 사용하며 AAD를 변경하지 않습니다."),
                        useWeaponGrips ? 1 : 0,
                        HandModes) == 1;

                if (EditorGUI.EndChangeCheck())
                {
                    _error = null;
                    _lastEndReason = null;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("새 무기 클립 만들기"))
                    {
                        CreateWeaponClip(characterClip);
                    }

                    using (new EditorGUI.DisabledScope(
                               character == null ||
                               characterClip == null ||
                               weapon == null ||
                               weapon.PresentationTemplate == null ||
                               weaponClip == null))
                    {
                        if (GUILayout.Button("무기 편집 시작"))
                        {
                            Start(
                                character,
                                characterClip,
                                animationWindow);
                        }
                    }
                }
            }

            if (IsActive)
                DrawActiveSession(animationWindow);

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(
                    _error,
                    MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(_lastEndReason))
            {
                EditorGUILayout.HelpBox(
                    _lastEndReason,
                    MessageType.Info);
            }
            else if (!IsActive)
            {
                EditorGUILayout.HelpBox(
                    "캐릭터 동작은 비교용으로만 재생하고, Animation 창에는 무기 이동과 " +
                    "무기 내부 부품만 기록합니다.",
                    MessageType.Info);
            }
        }
    }

    public void HandleSelection(
        GameObject selected)
    {
        _session?.HandleSelection(selected);
    }

    public void Dispose()
    {
        if (_session != null)
        {
            _lastEndReason = _session.EndReason;
            _session.Dispose();
            _session = null;
        }
    }

    private void DrawActiveSession(
        AnimationWindow animationWindow)
    {
        EditorGUILayout.Space(4);

        string state = animationWindow.recording
            ? "● 무기 클립 녹화 중"
            : animationWindow.previewing
                ? "무기 클립 미리보기 중"
                : "일시정지";

        EditorGUILayout.LabelField(
            state,
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "현재 프레임",
            animationWindow.frame.ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("무기 이동 선택"))
            {
                Selection.activeGameObject =
                    _session.View.gameObject;
                EditorGUIUtility.PingObject(
                    _session.View.gameObject);
            }

            using (new EditorGUI.DisabledScope(
                       !animationWindow.canRecord))
            {
                if (GUILayout.Button(
                        animationWindow.recording
                            ? "녹화 중지"
                            : "녹화 시작"))
                {
                    if (!animationWindow.recording)
                        animationWindow.previewing = true;

                    animationWindow.recording =
                        !animationWindow.recording;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("무기 클립 저장"))
            {
                Undo.FlushUndoRecordObjects();
                AssetDatabase.SaveAssetIfDirty(weaponClip);
            }

            if (GUILayout.Button("무기 편집 종료"))
                Dispose();
        }

        if (!string.IsNullOrEmpty(
                _session?.SelectionNotice))
        {
            EditorGUILayout.HelpBox(
                _session.SelectionNotice,
                MessageType.Info);
        }
    }

    private void Start(
        Animator character,
        AnimationClip characterClip,
        AnimationWindow animationWindow)
    {
        Dispose();
        _error = null;
        _lastEndReason = null;
        _session =
            new WeaponAnimationAuthoringSession();

        ActionHandIkPolicy handPolicy =
            useWeaponGrips
                ? ActionHandIkPolicy.WeaponGrips
                : ActionHandIkPolicy.AnimatedTargets;

        if (_session.TryStart(
                character,
                weapon.PresentationTemplate,
                characterClip,
                weaponClip,
                handPolicy,
                animationWindow,
                out _error))
        {
            return;
        }

        _session.Dispose();
        _session = null;
    }

    private void CreateWeaponClip(
        AnimationClip characterClip)
    {
        string defaultName =
            characterClip != null
                ? characterClip.name + "_Weapon"
                : "WeaponAnimation";
        string path =
            EditorUtility.SaveFilePanelInProject(
                "무기 애니메이션 클립 만들기",
                defaultName,
                "anim",
                "무기 이동과 내부 부품만 기록할 Animation Clip의 위치를 선택해주세요.");

        if (string.IsNullOrEmpty(path))
            return;

        AnimationClip clip =
            new()
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                frameRate =
                    characterClip != null
                        ? characterClip.frameRate
                        : 60f
            };
        AssetDatabase.CreateAsset(
            clip,
            path);
        AssetDatabase.SaveAssets();
        weaponClip = clip;
        EditorGUIUtility.PingObject(clip);
    }
}

#endif
