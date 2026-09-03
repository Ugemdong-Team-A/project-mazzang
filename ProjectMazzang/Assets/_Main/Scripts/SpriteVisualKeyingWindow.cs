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

    private Animator _animationRoot;
    private SpriteVisualAnimationDriver[] _parts =
        Array.Empty<SpriteVisualAnimationDriver>();
    private string[] _partNames = Array.Empty<string>();
    private int _partIndex = -1;
    private SpriteVisualAnimationDriver _target;

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
        Selection.selectionChanged += OnSelectionChanged;
        RefreshFromSelection();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
        Selection.selectionChanged -= OnSelectionChanged;
        SpriteVisualKeyingWindowCompanion.SuppressUntilAnimationLosesFocus();
    }

    private void OnSelectionChanged()
    {
        RefreshFromSelection();
        Repaint();
    }

    private void OnGUI()
    {
        AnimationWindow animationWindow = GetAnimationWindow();

        if (animationWindow == null)
        {
            EditorGUILayout.HelpBox(
                "Animation Window(Ctrl+6)을 열어주세요.",
                MessageType.Info);
            return;
        }

        AnimationClip clip = animationWindow.animationClip;

        if (clip == null)
        {
            EditorGUILayout.HelpBox(
                "Animation Clip을 선택해주세요.",
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

        EditorGUILayout.Space(8);
        DrawInitialIKKeys(
            animationWindow,
            clip);

        if (_target == null)
            return;

        if (_target.Renderer == null || _target.Resolver == null)
        {
            EditorGUILayout.HelpBox(
                "선택한 Driver의 SpriteResolver 또는 SpriteRenderer가 없습니다.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.Space(8);
        DrawTargetContext();
        EditorGUILayout.Space(8);
        DrawVisualKeys(animationWindow, clip);
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

    private void DrawInitialIKKeys(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        List<Transform> targets =
            FindLimbTargets(
                out List<string> missingTargets);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "IK 시작 포즈",
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

            if (animationWindow.frame != 0)
            {
                EditorGUILayout.HelpBox(
                    "현재 포즈를 정확히 저장하려면 먼저 0프레임으로 이동하세요.",
                    MessageType.Info);

                if (GUILayout.Button("0프레임으로 이동"))
                {
                    animationWindow.frame = 0;
                    animationWindow.Repaint();
                }

                return;
            }

            using (new EditorGUI.DisabledScope(
                       targets.Count !=
                       Standard2DRigDefinition.LimbSpecs.Count))
            {
                if (GUILayout.Button(
                        "현재 IK 포즈를 첫 키로 저장",
                        GUILayout.Height(30)))
                {
                    SaveInitialIKKeys(
                        animationWindow,
                        clip,
                        targets);
                }
            }
        }
    }

    private void DrawRigSelection()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            Animator newRoot =
                (Animator)EditorGUILayout.ObjectField(
                    "애니메이션 기준",
                    _animationRoot,
                    typeof(Animator),
                    true);

            if (EditorGUI.EndChangeCheck())
                SetAnimationRoot(newRoot);

            if (GUILayout.Button("새로고침", GUILayout.Width(72)))
                RefreshParts(true);
        }

        if (_animationRoot == null)
        {
            EditorGUILayout.HelpBox(
                "캐릭터 또는 캐릭터의 Solver/파츠를 선택해주세요.",
                MessageType.Warning);
            return;
        }

        if (_parts.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "애니메이션 기준 아래에서 Sprite Visual Driver를 찾지 못했습니다.",
                MessageType.Warning);
            DrawAddDriverButton();
            return;
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup(
            "대상 파츠",
            Mathf.Max(0, _partIndex),
            _partNames);

        if (EditorGUI.EndChangeCheck() || _target == null)
            SelectPart(newIndex, true);

        DrawAddDriverButton();
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

        if (!GUILayout.Button("선택한 파츠에 Visual Driver 추가"))
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
        EditorGUILayout.LabelField("Category", _target.Category);

        GameObject selected = Selection.activeGameObject;
        Solver2D selectedSolver = selected != null
            ? selected.GetComponentInParent<Solver2D>()
            : null;

        EditorGUILayout.LabelField(
            "현재 Solver",
            selectedSolver != null
                ? $"{selectedSolver.name} ({selectedSolver.GetType().Name})"
                : "선택 없음");

        EditorGUILayout.LabelField(
            "Sorting Layer",
            _target.Renderer.sortingLayerName);
        EditorGUILayout.LabelField(
            "원래 Order",
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
                "Driver에 사용할 SLA Label이 없습니다. 새로고침을 눌러주세요.",
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

        EditorGUI.BeginChangeCheck();
        int newLabelIndex = EditorGUILayout.Popup(
            "Label",
            currentLabelIndex,
            labels);

        if (EditorGUI.EndChangeCheck())
        {
            AddConstantKey(
                animationWindow,
                clip,
                labelBinding,
                newLabelIndex,
                "Set Sprite Label");
            _target.PreviewLabel(newLabelIndex);
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
            "Order",
            currentOrder);

        if (EditorGUI.EndChangeCheck())
            SetSortingOrderKey(animationWindow, clip, newOrder);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(
                "빠른 Order",
                GUILayout.Width(EditorGUIUtility.labelWidth - 4));

            if (GUILayout.Button("뒤"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _target.OriginalSortingOrder - 1);
            }

            if (GUILayout.Button("원래"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _target.OriginalSortingOrder);
            }

            if (GUILayout.Button("앞"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _target.OriginalSortingOrder + 1);
            }
        }
    }

    private void SetSortingOrderKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        int order)
    {
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

    private EditorCurveBinding CreateBinding(
        Type componentType,
        string propertyName)
    {
        string path = AnimationUtility.CalculateTransformPath(
            _target.transform,
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
        AnimationCurve curve = AnimationUtility.GetEditorCurve(
                                  clip,
                                  binding) ??
                              new AnimationCurve();
        float time = animationWindow.time;

        Undo.RegisterCompleteObjectUndo(clip, undoName);

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

        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();
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

                    return string.IsNullOrEmpty(driver.Category)
                        ? path
                        : $"{path}  [{driver.Category}]";
                })
            .ToArray();

        SelectPart(
            _parts.Length > 0
                ? Mathf.Clamp(_partIndex, 0, _parts.Length - 1)
                : -1,
            false);
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
        bool synchronize)
    {
        _partIndex = index;
        _target = index >= 0 && index < _parts.Length
            ? _parts[index]
            : null;

        if (_target == null || !synchronize)
            return;

        if (_target.SynchronizeDefinition())
            EditorUtility.SetDirty(_target);
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

    private void SaveInitialIKKeys(
        AnimationWindow animationWindow,
        AnimationClip clip,
        IReadOnlyList<Transform> targets)
    {
        const float firstFrameTime = 0f;

        Undo.RegisterCompleteObjectUndo(
            clip,
            "Save Initial Limb IK Keys");

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
                firstFrameTime);
            SetTransformKey(
                clip,
                path,
                "m_LocalPosition.y",
                position.y,
                firstFrameTime);
            SetTransformKey(
                clip,
                path,
                "m_LocalPosition.z",
                position.z,
                firstFrameTime);

            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.x",
                rotation.x,
                firstFrameTime);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.y",
                rotation.y,
                firstFrameTime);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.z",
                rotation.z,
                firstFrameTime);
            SetTransformKey(
                clip,
                path,
                "m_LocalRotation.w",
                rotation.w,
                firstFrameTime);
        }

        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();

        Debug.Log(
            $"[{nameof(SpriteVisualKeyingWindow)}] " +
            $"'{clip.name}' 0프레임에 Limb IK Target {targets.Count}개의 " +
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

        EditorGUILayout.LabelField("Category", driver.Category);
        EditorGUILayout.LabelField(
            "Labels",
            driver.Labels.Count.ToString());
        EditorGUILayout.LabelField(
            "원래 Order",
            driver.OriginalSortingOrder.ToString());

        if (!GUILayout.Button("SLA 정보 새로고침"))
            return;

        Undo.RecordObject(driver, "Refresh Sprite Visual Driver");

        if (driver.SynchronizeDefinition())
            EditorUtility.SetDirty(driver);
    }
}

#endif
