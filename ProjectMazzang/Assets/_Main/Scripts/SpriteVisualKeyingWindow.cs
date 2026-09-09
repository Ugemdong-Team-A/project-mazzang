#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;

public sealed class SpriteVisualKeyingWindow : EditorWindow
{
    private const string LabelIndexProperty = "pose";
    private const string SortingOrderProperty = "_sortingOrder";
    private const float OrderCardWidth = 76f;
    private const float OrderCardStep = 80f;
    private const string FkBakeIconPath =
        "Assets/_Main/Art/Editor/Icons/Standard2DAnimationBake.png";

    private Animator _animationRoot;
    private SpriteVisualAnimationDriver[] _parts =
        Array.Empty<SpriteVisualAnimationDriver>();
    private string[] _partNames = Array.Empty<string>();
    private int _partIndex = -1;
    private SpriteVisualAnimationDriver _target;
    private Vector2 _orderStripScroll;
    private SpriteVisualAnimationDriver _lastOrderStripTarget;
    private int _lastOrderStripTargetIndex = -1;
    private AnimationClip _lastBakedClip;
    private Vector2 _windowScroll;
    private AnimationClip _workingClip;
    private AnimationClip _observedAnimationWindowClip;
    private Animator _clipEditAnimator;
    private RuntimeAnimatorController _originalController;
    private AnimatorOverrideController _clipEditController;
    private Texture2D _fkBakeIcon;
    [SerializeField]
    private bool _showBulkKeying;
    [SerializeField]
    private bool _showPartDetails;

    [MenuItem("Tools/2D Animation/Sprite Visual Keyer")]
    private static void Open()
    {
        ShowWindow();
    }

    internal static void ShowWindow()
    {
        SpriteVisualKeyingWindow window =
            GetWindow<SpriteVisualKeyingWindow>();

        window.titleContent = CreateTitleContent();
        window.minSize = new Vector2(340f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = CreateTitleContent();
        minSize = new Vector2(340f, 320f);
        EditorApplication.update += Repaint;
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        RefreshFromSelection();
    }

    private void OnDisable()
    {
        RestoreClipEditController();
        EditorApplication.update -= Repaint;
        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        SpriteVisualKeyingWindowCompanion.SuppressUntilAnimationLosesFocus();
    }

    private void OnSelectionChanged()
    {
        RefreshFromSelection();
        Repaint();
    }

    private void OnUndoRedoPerformed()
    {
        ApplyCurrentVisualPreview();
        Repaint();
    }

    private void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            RestoreClipEditController();
    }

    private void OnGUI()
    {
        using EditorGUILayout.ScrollViewScope scroll =
            new(_windowScroll);
        _windowScroll = scroll.scrollPosition;

        DrawWindowContent();
    }

    private void DrawWindowContent()
    {
        AnimationWindow animationWindow = GetAnimationWindow();

        if (animationWindow == null)
        {
            EditorGUILayout.HelpBox(
                "Animation Window(Ctrl+6)을 열어주세요.",
                MessageType.Info);
            return;
        }

        SyncWorkingClip(animationWindow);
        DrawClipSelection(animationWindow);

        AnimationClip clip = _workingClip;

        if (clip == null)
        {
            EditorGUILayout.HelpBox(
                "작업할 Animation Clip을 선택해주세요.",
                MessageType.Info);
            return;
        }

        DrawHeader(
            clip,
            animationWindow.frame);

        EditorGUILayout.Space(8);
        DrawRigSelection();

        if (_animationRoot == null)
            return;

        bool isShownInAnimationWindow =
            animationWindow.animationClip == clip;

        if (!isShownInAnimationWindow)
        {
            EditorGUILayout.Space(8);
            DrawCharacterRefresh(animationWindow);
            DrawAddDriverButton();

            EditorGUILayout.HelpBox(
                "FK 굽기는 가능하지만 현재 프레임 키 편집은 Animation 창에 표시된 " +
                "클립에서만 가능합니다.",
                MessageType.Warning);

            EditorGUILayout.Space(8);
            DrawFkBake(clip);
            return;
        }

        if (_target != null &&
            (_target.Renderer == null ||
             _target.Resolver == null))
        {
            EditorGUILayout.HelpBox(
                "선택한 Driver의 SpriteResolver 또는 SpriteRenderer가 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.Space(8);
        DrawCharacterRefresh(animationWindow);
        DrawAddDriverButton();

        if (_target != null)
        {
            EditorGUILayout.Space(8);
            DrawSelectedPartKeying(
                animationWindow,
                clip);

            EditorGUILayout.Space(8);
            DrawPartDetails();
        }

        EditorGUILayout.Space(8);
        DrawBulkKeying(
            animationWindow,
            clip);

        EditorGUILayout.Space(8);
        DrawFkBake(clip);
    }

    private void DrawClipSelection(
        AnimationWindow animationWindow)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "작업 애니메이션",
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            AnimationClip selectedClip =
                (AnimationClip)EditorGUILayout.ObjectField(
                    "편집할 클립",
                    _workingClip,
                    typeof(AnimationClip),
                    false);

            if (EditorGUI.EndChangeCheck())
            {
                _workingClip = selectedClip;

                if (selectedClip != null)
                {
                    TryShowWorkingClip(
                        animationWindow,
                        false);
                }
            }

            if (_workingClip == null ||
                animationWindow.animationClip == _workingClip)
            {
                return;
            }

            if (GUILayout.Button(
                    "Animation 창에 표시",
                    GUILayout.Height(24)))
            {
                TryShowWorkingClip(
                    animationWindow,
                    true);
            }

            EditorGUILayout.HelpBox(
                "Controller 에셋과 Transition은 바꾸지 않고 임시 Override로 표시합니다.",
                MessageType.Info);
        }
    }

    private static void DrawHeader(
        AnimationClip clip,
        int frame)
    {
        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.65f, 0.95f, 1f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = previousColor;

            GUIContent heading = new(
                " Mazzang Sprite Visual Keyer",
                EditorGUIUtility.IconContent("AnimationClip Icon").image);

            EditorGUILayout.LabelField(
                heading,
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField("현재 클립", clip.name);
            EditorGUILayout.LabelField(
                "현재 프레임",
                frame.ToString());
        }

        GUI.backgroundColor = previousColor;
    }

    private void DrawIKKeys(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        List<Transform> targets =
            FindLimbTargets(
                out List<string> missingTargets);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "IK 포즈 저장",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "팔·다리·발 Target",
                $"{targets.Count}/{Standard2DRigDefinition.LimbSpecs.Count}");

            if (missingTargets.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "찾지 못한 Target: " +
                    string.Join(", ", missingTargets),
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(
                       targets.Count !=
                       Standard2DRigDefinition.LimbSpecs.Count))
            {
                if (GUILayout.Button(
                        "현재 프레임에 IK 포즈 저장",
                        GUILayout.Height(30)))
                {
                    SaveIKKeys(
                        animationWindow,
                        clip,
                        targets);
                }
            }
        }
    }

    private void DrawAllPartKeying(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "현재 스프라이트 상태 저장",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "모든 부위",
                $"{_parts.Length}개");

            using (new EditorGUI.DisabledScope(_parts.Length == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("현재 모습 저장"))
                    {
                        SaveAllPartKeys(
                            animationWindow,
                            clip,
                            true,
                            false);
                    }

                    if (GUILayout.Button("현재 순서 저장"))
                    {
                        SaveAllPartKeys(
                            animationWindow,
                            clip,
                            false,
                            true);
                    }
                }

                if (GUILayout.Button(
                        "현재 모습 + 순서 저장",
                        GUILayout.Height(26)))
                {
                    SaveAllPartKeys(
                        animationWindow,
                        clip,
                        true,
                        true);
                }
            }
        }
    }

    private void DrawFkBake(AnimationClip sourceClip)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "FK 애니메이션 굽기",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "현재 IK Target과 표준 Skeleton의 자세를 모든 프레임에 기록한 " +
                "별도 Animation Clip을 만듭니다. 원본 클립은 변경하지 않습니다.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(
                       _animationRoot == null))
            {
                GUIContent bakeContent = new(
                    "현재 클립 FK로 굽기...",
                    GetFkBakeIcon(),
                    "선택한 캐릭터의 6개 Limb IK를 프레임마다 계산하고, " +
                    "완성된 본 자세와 IK Target을 새 Animation Clip에 저장합니다.");

                Vector2 previousIconSize =
                    EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(
                    new Vector2(24f, 24f));
                bool bakeRequested = GUILayout.Button(
                    bakeContent,
                    GUILayout.Height(32));
                EditorGUIUtility.SetIconSize(
                    previousIconSize);

                if (bakeRequested)
                {
                    BakeCurrentClip(sourceClip);
                }
            }

            if (_lastBakedClip == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "마지막 결과",
                        _lastBakedClip,
                        typeof(AnimationClip),
                        false);
                }

                if (GUILayout.Button(
                        "찾기",
                        GUILayout.Width(44)))
                {
                    EditorGUIUtility.PingObject(
                        _lastBakedClip);
                }
            }
        }
    }

    private Texture2D GetFkBakeIcon()
    {
        if (_fkBakeIcon == null)
        {
            _fkBakeIcon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    FkBakeIconPath);
        }

        return _fkBakeIcon;
    }

    private void BakeCurrentClip(AnimationClip sourceClip)
    {
        if (!Standard2DAnimationBaker.TryBake(
                _animationRoot,
                sourceClip,
                out Standard2DAnimationBaker.Result result,
                out string bakeError))
        {
            EditorUtility.DisplayDialog(
                "FK 애니메이션 굽기 실패",
                bakeError,
                "확인");
            return;
        }

        string assetPath =
            EditorUtility.SaveFilePanelInProject(
                "FK 애니메이션 저장",
                sourceClip.name + "_FK",
                "anim",
                "원본과 분리된 FK Animation Clip의 저장 위치를 선택해주세요.",
                Standard2DAnimationBakeAssets.GetDefaultDirectory(
                    sourceClip));

        if (string.IsNullOrEmpty(assetPath))
        {
            DestroyImmediate(result.Clip);
            return;
        }

        if (!Standard2DAnimationBakeAssets.TrySave(
                sourceClip,
                result,
                assetPath,
                out AnimationClip savedClip,
                out string saveError))
        {
            if (result.Clip != null &&
                !AssetDatabase.Contains(result.Clip))
            {
                DestroyImmediate(result.Clip);
            }

            EditorUtility.DisplayDialog(
                "FK 애니메이션 저장 실패",
                saveError,
                "확인");
            return;
        }

        _lastBakedClip = savedClip;
        EditorGUIUtility.PingObject(savedClip);
        ShowNotification(
            new GUIContent(
                $"FK 굽기 완료 · 본 {result.BoneCount} · " +
                $"Target {result.TargetCount} · 프레임 {result.FrameCount}"));

        Debug.Log(
            $"[{nameof(SpriteVisualKeyingWindow)}] " +
            $"'{sourceClip.name}'을 '{assetPath}'에 FK로 구웠습니다. " +
            $"본 {result.BoneCount}개, Target {result.TargetCount}개, " +
            $"프레임 {result.FrameCount}개.",
            savedClip);
    }

    private void DrawRigSelection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            Animator newRoot =
                (Animator)EditorGUILayout.ObjectField(
                    "캐릭터 기준",
                    _animationRoot,
                    typeof(Animator),
                    true);

            if (EditorGUI.EndChangeCheck())
                SetAnimationRoot(newRoot);

            if (GUILayout.Button("목록", GUILayout.Width(52)))
                RefreshParts(true);
        }

        if (_animationRoot == null)
        {
            EditorGUILayout.HelpBox(
                "캐릭터 또는 캐릭터의 IK 제어기/부위를 선택해주세요.",
                MessageType.Warning);
            return;
        }

        if (_parts.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "캐릭터 기준 아래에서 Sprite Visual Driver를 찾지 못했습니다.",
                MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup(
            "편집할 부위",
            Mathf.Max(0, _partIndex),
            _partNames);

        if (EditorGUI.EndChangeCheck())
            SelectPart(newIndex, true, true);
        else if (_target == null)
            SelectPart(newIndex, true);
    }

    private void DrawSelectedPartKeying(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox))
        {
            GUIContent heading = new(
                "선택 부위 편집",
                EditorGUIUtility.IconContent(
                    "SpriteRenderer Icon").image);

            EditorGUILayout.LabelField(
                heading,
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "모습이나 순서를 바꾸면 현재 프레임에 즉시 키가 저장됩니다. " +
                "잘못 바꾼 값은 Ctrl+Z로 되돌릴 수 있습니다.",
                MessageType.Info);

            DrawVisualKeys(
                animationWindow,
                clip);
        }
    }

    private void DrawBulkKeying(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        _showBulkKeying =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                _showBulkKeying,
                "여러 부위 한 번에 저장");

        if (_showBulkKeying)
        {
            DrawIKKeys(
                animationWindow,
                clip);

            EditorGUILayout.Space(4);

            DrawAllPartKeying(
                animationWindow,
                clip);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawCharacterRefresh(
        AnimationWindow animationWindow)
    {
        Standard2DCharacterSetup characterSetup =
            _animationRoot != null
                ? _animationRoot.GetComponent<
                    Standard2DCharacterSetup>()
                : null;

        using (new EditorGUI.DisabledScope(
                   characterSetup == null))
        {
            GUIContent refreshContent = new(
                "편집 정보 새로고침",
                "현재 Animator·Sprite Resolver·Visual Driver 정보와 Animation 창 표시를 " +
                "다시 읽습니다. 기존 IK는 다시 만들지 않습니다.");

            if (GUILayout.Button(
                    refreshContent,
                    GUILayout.Height(24)))
            {
                RefreshCharacterSetup(
                    characterSetup,
                    animationWindow);
            }
        }

        if (characterSetup == null)
        {
            EditorGUILayout.HelpBox(
                "캐릭터 기준에 Standard 2D Character Setup이 없어 " +
                "편집 정보를 새로고침할 수 없습니다.",
                MessageType.Info);
        }
    }

    private void DrawPartDetails()
    {
        _showPartDetails =
            EditorGUILayout.BeginFoldoutHeaderGroup(
                _showPartDetails,
                "선택 부위 상세 정보");

        if (_showPartDetails)
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox))
            {
                DrawTargetContext();
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void RefreshCharacterSetup(
        Standard2DCharacterSetup setup,
        AnimationWindow animationWindow)
    {
        if (setup == null)
            return;

        Transform selectedPart = _target != null
            ? _target.transform
            : null;

        if (!Standard2DCharacterBuilder.RefreshExisting(setup))
            return;

        _animationRoot = setup.Animator != null
            ? setup.Animator
            : setup.GetComponent<Animator>();

        RefreshParts(false);

        if (selectedPart != null)
        {
            SpriteVisualAnimationDriver selectedDriver =
                selectedPart.GetComponent<SpriteVisualAnimationDriver>();

            if (selectedDriver != null)
                SelectDriver(selectedDriver, true);
        }

        ApplyCurrentVisualPreview();
        animationWindow.Repaint();
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawAddDriverButton()
    {
        GameObject selected = Selection.activeGameObject;
        SpriteResolver resolver =
            selected != null
                ? selected.GetComponent<SpriteResolver>()
                : null;

        if (resolver == null ||
            resolver.GetComponent<SpriteVisualAnimationDriver>() != null)
        {
            return;
        }

        if (!GUILayout.Button("선택한 부위에 Visual Driver 추가"))
            return;

        SpriteVisualAnimationDriver driver =
            Undo.AddComponent<SpriteVisualAnimationDriver>(
                resolver.gameObject);

        driver.SynchronizeDefinition();
        EditorUtility.SetDirty(driver);
        RefreshParts(false);
        SelectDriver(driver, false);
    }

    private void DrawTargetContext()
    {
        string targetPath = AnimationUtility.CalculateTransformPath(
            _target.transform,
            _animationRoot.transform);

        EditorGUILayout.LabelField("대상 경로", targetPath);
        EditorGUILayout.LabelField(
            "부위 분류",
            GetPartDisplayName(_target.Category));

        GameObject selected = Selection.activeGameObject;
        Solver2D selectedSolver = selected != null
            ? selected.GetComponentInParent<Solver2D>()
            : null;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.HorizontalScope(
                       GUILayout.Width(EditorGUIUtility.labelWidth)))
            {
                GUIContent ikLabel = new("연결된 IK");
                GUILayout.Label(
                    ikLabel,
                    GUILayout.Width(
                        EditorStyles.label.CalcSize(ikLabel).x));

                GUIContent helpIcon = EditorGUIUtility.IconContent("_Help");
                helpIcon.tooltip =
                    "IK Target을 따라 팔·다리 등의 뼈를 움직이는 제어기입니다.";
                GUILayout.Label(
                    helpIcon,
                    GUILayout.Width(18),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            GUILayout.Label(
                selectedSolver != null
                    ? $"{selectedSolver.name} ({selectedSolver.GetType().Name})"
                    : "해당 없음");
        }

        EditorGUILayout.LabelField(
            "그리기 레이어",
            _target.Renderer.sortingLayerName);
        EditorGUILayout.LabelField(
            "기본 스프라이트 순서",
            _target.OriginalSortingOrder.ToString());
    }

    private void DrawVisualKeys(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        string[] labels = _target.Labels.ToArray();

        if (labels.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "사용할 스프라이트 모습(Sprite Library Asset Label)이 없습니다. " +
                "새로고침을 눌러주세요.",
                MessageType.Warning);
            return;
        }

        EditorCurveBinding labelBinding = CreateBinding(
            typeof(SpriteVisualAnimationDriver),
            LabelIndexProperty);

        int currentLabelIndex = Mathf.Clamp(
            GetIntAtCurrentTime(
                animationWindow,
                clip,
                labelBinding,
                _target.LabelIndex),
            0,
            labels.Length - 1);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            int newLabelIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "스프라이트 모습",
                    "Sprite Library Asset의 Label을 선택합니다."),
                currentLabelIndex,
                labels);

            if (EditorGUI.EndChangeCheck())
            {
                SetLabelKey(
                    animationWindow,
                    clip,
                    labelBinding,
                    newLabelIndex);
            }

            int defaultLabelIndex = Array.IndexOf(
                labels,
                _target.DefaultLabel);

            using (new EditorGUI.DisabledScope(defaultLabelIndex < 0))
            {
                if (GUILayout.Button("기본", GUILayout.Width(52)))
                {
                    SetLabelKey(
                        animationWindow,
                        clip,
                        labelBinding,
                        defaultLabelIndex);
                }
            }
        }

        EditorCurveBinding orderBinding = CreateBinding(
            typeof(SpriteVisualAnimationDriver),
            SortingOrderProperty);

        int currentOrder = GetIntAtCurrentTime(
            animationWindow,
            clip,
            orderBinding,
            _target.SortingOrder);

        EditorGUI.BeginChangeCheck();
        int newOrder = EditorGUILayout.IntField(
            "스프라이트 순서",
            currentOrder);

        if (EditorGUI.EndChangeCheck())
            SetSortingOrderKey(animationWindow, clip, newOrder);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(
                "빠른 변경",
                GUILayout.Width(EditorGUIUtility.labelWidth - 4));

            if (GUILayout.Button("-10"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    currentOrder - 10);
            }

            if (GUILayout.Button("-1"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    currentOrder - 1);
            }

            if (GUILayout.Button("기본"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _target.OriginalSortingOrder);
            }

            if (GUILayout.Button("+1"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    currentOrder + 1);
            }

            if (GUILayout.Button("+10"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    currentOrder + 10);
            }
        }

        DrawAppliedResult(
            labels[currentLabelIndex],
            currentOrder);

        DrawNearbySpriteOrders();

        // TODO: 모든 Driver의 기본 모습/순서 키 기능은
        // 선택 부위의 기존 기본 복귀 기능과 별도로 안정화한 뒤 추가한다.
        // DrawAllPartsDefaults(animationWindow, clip);
    }

    private void DrawNearbySpriteOrders()
    {
        int sortingLayerId = _target.Renderer.sortingLayerID;
        SpriteVisualAnimationDriver[] orderedParts = _parts
            .Where(
                driver => driver != null &&
                          driver.Renderer != null &&
                          driver.Renderer.sortingLayerID == sortingLayerId)
            .OrderBy(driver => driver.Renderer.sortingOrder)
            .ThenBy(
                driver => AnimationUtility.CalculateTransformPath(
                    driver.transform,
                    _animationRoot.transform),
                StringComparer.Ordinal)
            .ToArray();

        int targetIndex = Array.IndexOf(orderedParts, _target);

        if (targetIndex < 0)
            return;

        HashSet<int> duplicateOrders = orderedParts
            .GroupBy(driver => driver.Renderer.sortingOrder)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        if (_lastOrderStripTarget != _target ||
            _lastOrderStripTargetIndex != targetIndex)
        {
            float viewportWidth = Mathf.Max(0f, position.width - 36f);
            _orderStripScroll.x = Mathf.Max(
                0f,
                targetIndex * OrderCardStep -
                (viewportWidth - OrderCardWidth) * 0.5f);
            _lastOrderStripTarget = _target;
            _lastOrderStripTargetIndex = targetIndex;
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "전체 스프라이트 순서  (뒤 → 앞)",
                EditorStyles.boldLabel);

            _orderStripScroll = EditorGUILayout.BeginScrollView(
                _orderStripScroll,
                true,
                false,
                GUILayout.Height(118));

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (SpriteVisualAnimationDriver driver in orderedParts)
                {
                    DrawSpriteOrderCard(
                        driver,
                        driver == _target,
                        duplicateOrders.Contains(
                            driver.Renderer.sortingOrder));
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSpriteOrderCard(
        SpriteVisualAnimationDriver driver,
        bool selected,
        bool hasSameOrder)
    {
        Color previousBackgroundColor = GUI.backgroundColor;

        if (selected)
            GUI.backgroundColor = new Color(0.35f, 0.7f, 1f, 1f);

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox,
                   GUILayout.Width(OrderCardWidth)))
        {
            Sprite sprite = driver.Renderer.sprite;
            Texture preview = sprite != null
                ? AssetPreview.GetAssetPreview(sprite)
                : null;

            if (preview == null && sprite != null)
                preview = AssetPreview.GetMiniThumbnail(sprite);

            string partName = string.IsNullOrEmpty(driver.Category)
                ? driver.gameObject.name
                : driver.Category;
            string displayName = GetPartDisplayName(partName);
            GUIContent previewContent = preview != null
                ? new GUIContent(preview, displayName)
                : new GUIContent("모습 없음", displayName);

            if (GUILayout.Button(
                    previewContent,
                    GUILayout.Width(OrderCardWidth - 10f),
                    GUILayout.Height(52f)))
            {
                SelectPart(
                    Array.IndexOf(_parts, driver),
                    true,
                    true);
            }

            if (GUILayout.Button(
                    new GUIContent(partName, displayName),
                    EditorStyles.miniButton,
                    GUILayout.Width(OrderCardWidth - 10f)))
            {
                SelectPart(
                    Array.IndexOf(_parts, driver),
                    true,
                    true);
            }

            string orderText = hasSameOrder
                ? $"{driver.Renderer.sortingOrder} · 같음"
                : driver.Renderer.sortingOrder.ToString();

            GUILayout.Label(
                orderText,
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(OrderCardWidth - 10f));
        }

        GUI.backgroundColor = previousBackgroundColor;
    }

    private void DrawAppliedResult(
        string label,
        int order)
    {
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "현재 모습",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "스프라이트",
                $"{GetPartDisplayName(_target.Category)} / {label}");
            EditorGUILayout.LabelField(
                "스프라이트 순서",
                _target.OriginalSortingOrder == order
                    ? order.ToString()
                    : $"{_target.OriginalSortingOrder} → {order}");
        }
    }

    private void SetLabelKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        EditorCurveBinding labelBinding,
        int labelIndex)
    {
        RecordPreviewUndo("Set Sprite Label");
        AddConstantKey(
            animationWindow,
            clip,
            labelBinding,
            labelIndex,
            "Set Sprite Label");
        _target.PreviewLabel(labelIndex);
    }

    private void SetSortingOrderKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        int order)
    {
        RecordPreviewUndo("Set Sprite Sorting Order");
        AddConstantKey(
            animationWindow,
            clip,
            CreateBinding(
                typeof(SpriteVisualAnimationDriver),
                SortingOrderProperty),
            order,
            "Set Sprite Sorting Order");
        _target.PreviewSortingOrder(order);
    }

    private void RecordPreviewUndo(string undoName)
    {
        Undo.RecordObjects(
            new UnityEngine.Object[]
            {
                _target,
                _target.Resolver,
                _target.Renderer
            },
            undoName);
    }

    private EditorCurveBinding CreateBinding(
        Type componentType,
        string propertyName)
    {
        return CreateBinding(
            _target,
            componentType,
            propertyName);
    }

    private EditorCurveBinding CreateBinding(
        SpriteVisualAnimationDriver driver,
        Type componentType,
        string propertyName)
    {
        string path = AnimationUtility.CalculateTransformPath(
            driver.transform,
            _animationRoot.transform);

        return EditorCurveBinding.FloatCurve(
            path,
            componentType,
            propertyName);
    }

    private static int GetIntAtCurrentTime(
        AnimationWindow animationWindow,
        AnimationClip clip,
        EditorCurveBinding binding,
        int fallback)
    {
        AnimationCurve curve = AnimationUtility.GetEditorCurve(
            clip,
            binding);

        if (curve == null || curve.length == 0)
            return fallback;

        return Mathf.RoundToInt(
            curve.Evaluate(animationWindow.time));
    }

    private static void AddConstantKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        EditorCurveBinding binding,
        int value,
        string undoName)
    {
        float time = animationWindow.time;

        Undo.RegisterCompleteObjectUndo(clip, undoName);

        SetConstantKey(
            clip,
            binding,
            value,
            time);

        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();
    }

    private static void SetConstantKey(
        AnimationClip clip,
        EditorCurveBinding binding,
        int value,
        float time)
    {
        AnimationCurve curve = AnimationUtility.GetEditorCurve(
                                  clip,
                                  binding) ??
                              new AnimationCurve();

        int keyIndex;
        int existingIndex = FindKeyAtTime(curve, time);

        if (existingIndex >= 0)
        {
            keyIndex = curve.MoveKey(
                existingIndex,
                new Keyframe(time, value));
        }
        else
        {
            keyIndex = curve.AddKey(
                new Keyframe(time, value));
        }

        AnimationUtility.SetKeyLeftTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetKeyRightTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.Constant);
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private void SaveAllPartKeys(
        AnimationWindow animationWindow,
        AnimationClip clip,
        bool saveLabels,
        bool saveOrders)
    {
        SpriteVisualAnimationDriver[] validParts = _parts
            .Where(
                driver => driver != null &&
                          driver.Renderer != null &&
                          driver.Resolver != null)
            .ToArray();

        int labelKeyCount = saveLabels
            ? validParts.Count(driver => driver.Labels.Count > 0)
            : 0;
        int orderKeyCount = saveOrders
            ? validParts.Length
            : 0;

        if (labelKeyCount == 0 && orderKeyCount == 0)
        {
            Debug.LogWarning(
                $"[{nameof(SpriteVisualKeyingWindow)}] " +
                "저장할 스프라이트 상태가 없습니다.",
                clip);
            return;
        }

        string undoName = saveLabels && saveOrders
            ? "Save Current Sprite Visual Keys"
            : saveLabels
                ? "Save Current Sprite Label Keys"
                : "Save Current Sprite Order Keys";

        Undo.RegisterCompleteObjectUndo(
            clip,
            undoName);

        float time = animationWindow.time;

        foreach (SpriteVisualAnimationDriver driver in validParts)
        {
            if (saveLabels && driver.Labels.Count > 0)
            {
                SetConstantKey(
                    clip,
                    CreateBinding(
                        driver,
                        typeof(SpriteVisualAnimationDriver),
                        LabelIndexProperty),
                    GetCurrentLabelIndex(driver),
                    time);
            }

            if (saveOrders)
            {
                SetConstantKey(
                    clip,
                    CreateBinding(
                        driver,
                        typeof(SpriteVisualAnimationDriver),
                        SortingOrderProperty),
                    driver.Renderer.sortingOrder,
                    time);
            }
        }

        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();

        Debug.Log(
            $"[{nameof(SpriteVisualKeyingWindow)}] " +
            $"'{clip.name}' {animationWindow.frame}프레임에 " +
            $"모습 {labelKeyCount}개, 순서 {orderKeyCount}개 키를 저장했습니다.",
            clip);
    }

    private static int GetCurrentLabelIndex(
        SpriteVisualAnimationDriver driver)
    {
        string[] labels = driver.Labels.ToArray();
        string currentLabel = driver.Resolver.GetLabel();
        int currentIndex = Array.IndexOf(
            labels,
            currentLabel);

        return currentIndex >= 0
            ? currentIndex
            : Mathf.Clamp(
                driver.LabelIndex,
                0,
                labels.Length - 1);
    }

    private void SyncWorkingClip(
        AnimationWindow animationWindow)
    {
        AnimationClip animationWindowClip =
            animationWindow.animationClip;

        if (animationWindowClip ==
            _observedAnimationWindowClip)
        {
            return;
        }

        _observedAnimationWindowClip =
            animationWindowClip;

        if (animationWindowClip == null)
            return;

        if (_clipEditController != null &&
            animationWindowClip != _workingClip)
        {
            RestoreClipEditController();
        }

        _workingClip = animationWindowClip;
    }

    private bool TryShowWorkingClip(
        AnimationWindow animationWindow,
        bool showError)
    {
        if (_workingClip == null)
            return false;

        if (_animationRoot == null)
        {
            if (showError)
            {
                EditorUtility.DisplayDialog(
                    "Animation Clip 표시 실패",
                    "먼저 캐릭터 기준 Animator를 선택해주세요.",
                    "확인");
            }

            return false;
        }

        RestoreClipEditController();

        GameObject selected =
            Selection.activeGameObject;
        Animator selectedAnimator =
            selected != null
                ? selected.GetComponentInParent<Animator>()
                : null;

        if (selectedAnimator != _animationRoot)
        {
            Selection.activeGameObject =
                _animationRoot.gameObject;
        }

        RuntimeAnimatorController controller =
            _animationRoot.runtimeAnimatorController;

        if (controller == null)
        {
            if (showError)
            {
                EditorUtility.DisplayDialog(
                    "Animation Clip 표시 실패",
                    "선택한 Animator에 Controller가 없습니다.",
                    "확인");
            }

            return false;
        }

        if (!controller.animationClips.Contains(
                _workingClip) &&
            !TryCreateClipEditController(
                controller,
                out string controllerError))
        {
            if (showError)
            {
                EditorUtility.DisplayDialog(
                    "Animation Clip 표시 실패",
                    controllerError,
                    "확인");
            }

            return false;
        }

        animationWindow.animationClip =
            _workingClip;
        _observedAnimationWindowClip =
            animationWindow.animationClip;
        animationWindow.Repaint();

        bool displayed =
            animationWindow.animationClip ==
            _workingClip;

        if (!displayed)
        {
            RestoreClipEditController();

            if (showError)
            {
                EditorUtility.DisplayDialog(
                    "Animation Clip 표시 실패",
                    "Animation 창에서 클립을 선택하지 못했습니다. " +
                    "캐릭터 Root를 선택한 뒤 다시 시도해주세요.",
                    "확인");
            }
        }

        return displayed;
    }

    private bool TryCreateClipEditController(
        RuntimeAnimatorController sourceController,
        out string error)
    {
        error = null;

        AnimatorOverrideController overrideController =
            new(sourceController)
            {
                name =
                    sourceController.name +
                    " (Clip Edit Session)",
                hideFlags = HideFlags.HideAndDontSave
            };
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides =
            new(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);

        AnimationClip placeholder =
            overrides
                .Select(pair => pair.Key)
                .FirstOrDefault(
                    clip => clip != null &&
                            clip.name == "ActionReleasePlaceholder") ??
            overrides
                .Select(pair => pair.Key)
                .FirstOrDefault(clip => clip != null);

        if (placeholder == null)
        {
            DestroyImmediate(overrideController);
            error =
                "Controller에서 임시로 교체할 Animation Clip을 찾지 못했습니다.";
            return false;
        }

        overrideController[placeholder] =
            _workingClip;

        _clipEditAnimator = _animationRoot;
        _originalController = sourceController;
        _clipEditController = overrideController;
        _clipEditAnimator.runtimeAnimatorController =
            _clipEditController;

        return true;
    }

    private void RestoreClipEditController()
    {
        if (_clipEditAnimator != null &&
            _clipEditController != null &&
            _clipEditAnimator.runtimeAnimatorController ==
            _clipEditController)
        {
            _clipEditAnimator.runtimeAnimatorController =
                _originalController;
        }

        if (_clipEditController != null)
            DestroyImmediate(_clipEditController);

        _clipEditAnimator = null;
        _originalController = null;
        _clipEditController = null;
    }

    private void RefreshFromSelection()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
            return;

        Animator animator = selected.GetComponentInParent<Animator>();

        if (animator == null)
            animator = selected.GetComponentInChildren<Animator>(true);

        if (animator != null && animator != _animationRoot)
            SetAnimationRoot(animator);

        SpriteVisualAnimationDriver selectedDriver =
            selected.GetComponent<SpriteVisualAnimationDriver>();

        if (selectedDriver != null)
            SelectDriver(selectedDriver, true);
    }

    private void SetAnimationRoot(Animator animator)
    {
        if (_animationRoot != animator)
            RestoreClipEditController();

        _animationRoot = animator;
        RefreshParts(false);
    }

    private void RefreshParts(bool recordUndo)
    {
        if (_animationRoot == null)
        {
            _parts = Array.Empty<SpriteVisualAnimationDriver>();
            _partNames = Array.Empty<string>();
            SelectPart(-1, false);
            return;
        }

        _parts = _animationRoot
            .GetComponentsInChildren<SpriteVisualAnimationDriver>(true)
            .OrderBy(
                driver => AnimationUtility.CalculateTransformPath(
                    driver.transform,
                    _animationRoot.transform))
            .ToArray();

        foreach (SpriteVisualAnimationDriver driver in _parts)
        {
            if (recordUndo)
                Undo.RecordObject(driver, "Refresh Sprite Visual Driver");

            if (driver.SynchronizeDefinition())
                EditorUtility.SetDirty(driver);
        }

        _partNames = _parts
            .Select(
                driver =>
                {
                    string path = AnimationUtility.CalculateTransformPath(
                        driver.transform,
                        _animationRoot.transform);

                    string koreanName = GetKoreanPartName(
                        string.IsNullOrEmpty(driver.Category)
                            ? driver.gameObject.name
                            : driver.Category);

                    string categoryLabel = string.IsNullOrEmpty(koreanName)
                        ? driver.Category
                        : $"{driver.Category} · {koreanName}";

                    return string.IsNullOrEmpty(driver.Category)
                        ? string.IsNullOrEmpty(koreanName)
                            ? path
                            : $"{path}  [{koreanName}]"
                        : $"{path}  [{categoryLabel}]";
                })
            .ToArray();

        SelectPart(
            _parts.Length > 0
                ? Mathf.Clamp(_partIndex, 0, _parts.Length - 1)
                : -1,
            false);
    }

    private static string GetKoreanPartName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        string normalized = source
            .Trim()
            .ToLowerInvariant()
            .Replace('-', '_')
            .Replace(' ', '_');

        // 이목구비는 세부 종류가 많아 원문 Label을 그대로 보여준다.
        if (normalized.Contains("eye") ||
            normalized.Contains("mouth") ||
            normalized.Contains("nose") ||
            normalized.Contains("brow"))
        {
            return string.Empty;
        }

        bool isLeft = normalized.EndsWith("_l") ||
                      normalized.Contains("_l_") ||
                      normalized.Contains("left");
        bool isRight = normalized.EndsWith("_r") ||
                       normalized.Contains("_r_") ||
                       normalized.Contains("right");
        string side = isLeft
            ? "왼쪽 "
            : isRight
                ? "오른쪽 "
                : string.Empty;

        if (normalized.Contains("hair"))
            return normalized.Contains("back") ? "뒷머리" : "머리카락";
        if (normalized.Contains("coat") ||
            normalized.Contains("cloth") ||
            normalized.Contains("clothes"))
        {
            return normalized.Contains("back") ? "뒤쪽 옷" : "앞쪽 옷";
        }
        if (normalized.Contains("shoulder"))
            return side + "어깨";
        if (normalized.Contains("forearm"))
            return side + "아래팔";
        if (normalized.Contains("arm"))
            return side + "팔";
        if (normalized.Contains("hand"))
            return side + "손";
        if (normalized.Contains("thigh"))
            return side + "허벅지";
        if (normalized.Contains("calf"))
            return side + "종아리";
        if (normalized.Contains("leg"))
            return side + "다리";
        if (normalized.Contains("foot"))
            return side + "발";
        if (normalized.Contains("torso") || normalized.Contains("chest"))
            return "몸통";
        if (normalized.Contains("neck"))
            return "목";
        if (normalized.Contains("head"))
            return "머리";

        return string.Empty;
    }

    private static string GetPartDisplayName(string source)
    {
        string koreanName = GetKoreanPartName(source);

        return string.IsNullOrEmpty(koreanName)
            ? source
            : $"{source} · {koreanName}";
    }

    private void SelectDriver(
        SpriteVisualAnimationDriver driver,
        bool synchronize)
    {
        int index = Array.IndexOf(_parts, driver);

        if (index < 0)
        {
            RefreshParts(false);
            index = Array.IndexOf(_parts, driver);
        }

        SelectPart(index, synchronize);
    }

    private void SelectPart(
        int index,
        bool synchronize,
        bool selectInScene = false)
    {
        _partIndex = index;
        _target = index >= 0 && index < _parts.Length
            ? _parts[index]
            : null;

        if (_target == null || !synchronize)
            return;

        if (_target.SynchronizeDefinition())
            EditorUtility.SetDirty(_target);

        if (selectInScene)
        {
            Selection.activeGameObject = _target.gameObject;
            EditorGUIUtility.PingObject(_target.gameObject);
            SceneView.RepaintAll();
        }
    }

    private void ApplyCurrentVisualPreview()
    {
        AnimationWindow animationWindow = GetAnimationWindow();
        AnimationClip clip = animationWindow != null
            ? animationWindow.animationClip
            : null;

        if (animationWindow == null || clip == null || _target == null)
            return;

        string[] labels = _target.Labels.ToArray();

        if (labels.Length == 0)
            return;

        int labelIndex = Mathf.Clamp(
            GetIntAtCurrentTime(
                animationWindow,
                clip,
                CreateBinding(
                    typeof(SpriteVisualAnimationDriver),
                    LabelIndexProperty),
                _target.LabelIndex),
            0,
            labels.Length - 1);

        int order = GetIntAtCurrentTime(
            animationWindow,
            clip,
            CreateBinding(
                typeof(SpriteVisualAnimationDriver),
                SortingOrderProperty),
            _target.SortingOrder);

        _target.PreviewLabel(labelIndex);
        _target.PreviewSortingOrder(order);
        animationWindow.Repaint();
        SceneView.RepaintAll();
    }

    private List<Transform> FindLimbTargets(
        out List<string> missingTargets)
    {
        List<Transform> targets = new();
        missingTargets = new List<string>();

        LimbSolver2D[] solvers =
            _animationRoot.GetComponentsInChildren<LimbSolver2D>(
                true);

        foreach (Standard2DRigDefinition.LimbSpec spec
                 in Standard2DRigDefinition.LimbSpecs)
        {
            LimbSolver2D solver =
                solvers.FirstOrDefault(
                    item => item.name == spec.SolverName);

            IKChain2D chain =
                solver != null
                    ? solver.GetChain(0)
                    : null;

            Transform target =
                chain != null
                    ? chain.target
                    : null;

            if (target == null)
            {
                missingTargets.Add(spec.TargetName);
                continue;
            }

            targets.Add(target);
        }

        return targets;
    }

    private void SaveIKKeys(
        AnimationWindow animationWindow,
        AnimationClip clip,
        IReadOnlyList<Transform> targets)
    {
        float time = animationWindow.time;

        Undo.RegisterCompleteObjectUndo(
            clip,
            "Save Current Limb IK Keys");

        foreach (Transform target in targets)
        {
            string path =
                AnimationUtility.CalculateTransformPath(
                    target,
                    _animationRoot.transform);

            Vector3 position =
                target.localPosition;

            Quaternion rotation =
                target.localRotation;

            SetTransformKey(
                clip,
                path,
                "m_LocalPosition.x",
                position.x,
                time);
            SetTransformKey(
                clip,
                path,
                "m_LocalPosition.y",
                position.y,
                time);
            SetTransformKey(
                clip,
                path,
                "m_LocalPosition.z",
                position.z,
                time);

            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.x",
                rotation.x,
                time);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.y",
                rotation.y,
                time);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.z",
                rotation.z,
                time);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.w",
                rotation.w,
                time);
        }

        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();

        Debug.Log(
            $"[{nameof(SpriteVisualKeyingWindow)}] " +
            $"'{clip.name}' {animationWindow.frame}프레임에 Limb IK Target {targets.Count}개의 " +
            "Position/Rotation 키를 저장했습니다.",
            clip);
    }

    private static void SetTransformKey(
        AnimationClip clip,
        string path,
        string propertyName,
        float value,
        float time)
    {
        EditorCurveBinding binding =
            EditorCurveBinding.FloatCurve(
                path,
                typeof(Transform),
                propertyName);

        AnimationCurve curve =
            AnimationUtility.GetEditorCurve(
                clip,
                binding) ??
            new AnimationCurve();

        int existingIndex =
            FindKeyAtTime(
                curve,
                time);

        int keyIndex =
            existingIndex >= 0
                ? curve.MoveKey(
                    existingIndex,
                    new Keyframe(time, value))
                : curve.AddKey(
                    new Keyframe(time, value));

        AnimationUtility.SetKeyLeftTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.ClampedAuto);
        AnimationUtility.SetKeyRightTangentMode(
            curve,
            keyIndex,
            AnimationUtility.TangentMode.ClampedAuto);

        AnimationUtility.SetEditorCurve(
            clip,
            binding,
            curve);
    }

    private static AnimationWindow GetAnimationWindow()
    {
        return Resources
            .FindObjectsOfTypeAll<AnimationWindow>()
            .FirstOrDefault();
    }

    private static int FindKeyAtTime(
        AnimationCurve curve,
        float time)
    {
        const float tolerance = 0.0001f;

        for (int i = 0; i < curve.length; i++)
        {
            if (Mathf.Abs(curve.keys[i].time - time) < tolerance)
                return i;
        }

        return -1;
    }

    private static GUIContent CreateTitleContent()
    {
        return new GUIContent(
            "Sprite Visual",
            EditorGUIUtility.IconContent("AnimationClip Icon").image,
            "Mazzang Sprite Visual Keyer");
    }
}

[CustomEditor(typeof(SpriteVisualAnimationDriver))]
public sealed class SpriteVisualAnimationDriverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SpriteVisualAnimationDriver driver =
            (SpriteVisualAnimationDriver)target;

        EditorGUILayout.HelpBox(
            "Sprite Visual Keyer가 사용하는 연결 컴포넌트입니다. " +
            "일반적으로 직접 설정할 필요가 없습니다.",
            MessageType.Info);

        EditorGUILayout.LabelField("부위 분류", driver.Category);
        EditorGUILayout.LabelField(
            "스프라이트 모습 수",
            driver.Labels.Count.ToString());
        EditorGUILayout.LabelField(
            "기본 스프라이트 모습",
            string.IsNullOrEmpty(driver.DefaultLabel)
                ? "설정 없음"
                : driver.DefaultLabel);
        EditorGUILayout.LabelField(
            "기본 스프라이트 순서",
            driver.OriginalSortingOrder.ToString());

        if (!GUILayout.Button("Sprite Library 정보 새로고침"))
            return;

        Undo.RecordObject(driver, "Refresh Sprite Visual Driver");

        if (driver.SynchronizeDefinition())
            EditorUtility.SetDirty(driver);
    }
}

#endif
