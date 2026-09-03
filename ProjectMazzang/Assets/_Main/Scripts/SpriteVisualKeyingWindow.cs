#if UNITY_EDITOR

using System;
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

    [MenuItem("Tools/Animation/Sprite Visual Keyer")]
    private static void Open()
    {
        GetWindow<SpriteVisualKeyingWindow>("Sprite Visual");
    }

    private void OnEnable()
    {
        EditorApplication.update += Repaint;
        Selection.selectionChanged += OnSelectionChanged;
        RefreshFromSelection();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Repaint;
        Selection.selectionChanged -= OnSelectionChanged;
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

        EditorGUILayout.LabelField("현재 클립", clip.name);
        EditorGUILayout.LabelField(
            "현재 프레임",
            animationWindow.frame.ToString());

        EditorGUILayout.Space(8);
        DrawRigSelection();

        if (_animationRoot == null || _target == null)
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
