#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;

public sealed class SpriteVisualKeyingWindow : EditorWindow
{
    private const string SpriteHashProperty = "m_SpriteHash";
    private const string SortingOrderProperty = "m_SortingOrder";

    private Animator _animationRoot;
    private SpriteResolver[] _parts = Array.Empty<SpriteResolver>();
    private string[] _partNames = Array.Empty<string>();
    private int _partIndex = -1;
    private SpriteResolver _target;
    private SpriteRenderer _renderer;
    private int _originalSortingOrder;

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

        DrawClipContext(animationWindow, clip);
        EditorGUILayout.Space(8);
        DrawRigSelection();

        if (_animationRoot == null ||
            _target == null ||
            _renderer == null)
        {
            return;
        }

        EditorGUILayout.Space(8);
        DrawSelectionContext();

        SpriteLibrary library = _target.spriteLibrary;
        SpriteLibraryAsset libraryAsset =
            library != null ? library.spriteLibraryAsset : null;

        if (libraryAsset == null)
        {
            EditorGUILayout.HelpBox(
                "선택한 파츠에서 Sprite Library Asset을 찾을 수 없습니다.",
                MessageType.Warning);
            return;
        }

        string category = _target.GetCategory();

        if (string.IsNullOrEmpty(category))
        {
            EditorGUILayout.HelpBox(
                "선택한 파츠의 Category가 설정되지 않았습니다.",
                MessageType.Warning);
            return;
        }

        DrawVisualKeys(
            animationWindow,
            clip,
            libraryAsset,
            category);
    }

    private static void DrawClipContext(
        AnimationWindow animationWindow,
        AnimationClip clip)
    {
        EditorGUILayout.LabelField("현재 클립", clip.name);
        EditorGUILayout.LabelField(
            "현재 프레임",
            animationWindow.frame.ToString());
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
                RefreshParts();
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
                "애니메이션 기준 아래에서 SpriteResolver를 찾지 못했습니다.",
                MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup(
            "대상 파츠",
            Mathf.Max(0, _partIndex),
            _partNames);

        if (EditorGUI.EndChangeCheck() || _target == null)
            SelectPart(newIndex);
    }

    private void DrawSelectionContext()
    {
        string targetPath = AnimationUtility.CalculateTransformPath(
            _target.transform,
            _animationRoot.transform);

        EditorGUILayout.LabelField("대상 경로", targetPath);

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
            _renderer.sortingLayerName);
        EditorGUILayout.LabelField(
            "원래 Order",
            _originalSortingOrder.ToString());
    }

    private void DrawVisualKeys(
        AnimationWindow animationWindow,
        AnimationClip clip,
        SpriteLibraryAsset libraryAsset,
        string category)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Category", category);

        string[] labels = libraryAsset
            .GetCategoryLabelNames(category)
            .OrderBy(label => label)
            .ToArray();

        if (labels.Length == 0)
        {
            EditorGUILayout.HelpBox(
                $"Category '{category}'에 Label이 없습니다.",
                MessageType.Warning);
            return;
        }

        string currentLabel = GetLabelAtCurrentTime(
            animationWindow,
            clip,
            category,
            labels);

        int currentLabelIndex = Mathf.Max(
            0,
            Array.IndexOf(labels, currentLabel));

        EditorGUI.BeginChangeCheck();
        int newLabelIndex = EditorGUILayout.Popup(
            "Label",
            currentLabelIndex,
            labels);

        if (EditorGUI.EndChangeCheck())
        {
            AddDiscreteKey(
                animationWindow,
                clip,
                CreateBinding(
                    _target.transform,
                    typeof(SpriteResolver),
                    SpriteHashProperty),
                GetSpriteHash(category, labels[newLabelIndex]),
                "Set Sprite Label");

            _target.SetCategoryAndLabel(
                category,
                labels[newLabelIndex]);
        }

        EditorCurveBinding orderBinding = CreateBinding(
            _renderer.transform,
            typeof(SpriteRenderer),
            SortingOrderProperty);

        int currentOrder = GetIntAtCurrentTime(
            animationWindow,
            clip,
            orderBinding,
            _renderer.sortingOrder);

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
                    _originalSortingOrder - 1);
            }

            if (GUILayout.Button("원래"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _originalSortingOrder);
            }

            if (GUILayout.Button("앞"))
            {
                SetSortingOrderKey(
                    animationWindow,
                    clip,
                    _originalSortingOrder + 1);
            }
        }
    }

    private void SetSortingOrderKey(
        AnimationWindow animationWindow,
        AnimationClip clip,
        int order)
    {
        AddDiscreteKey(
            animationWindow,
            clip,
            CreateBinding(
                _renderer.transform,
                typeof(SpriteRenderer),
                SortingOrderProperty),
            order,
            "Set Sprite Sorting Order");

        _renderer.sortingOrder = order;
    }

    private string GetLabelAtCurrentTime(
        AnimationWindow animationWindow,
        AnimationClip clip,
        string category,
        string[] labels)
    {
        EditorCurveBinding binding = CreateBinding(
            _target.transform,
            typeof(SpriteResolver),
            SpriteHashProperty);

        AnimationCurve curve = AnimationUtility.GetEditorCurve(
            clip,
            binding);

        if (curve == null || curve.length == 0)
            return _target.GetLabel();

        int currentHash = CurveValueToInt(
            curve.Evaluate(animationWindow.time));

        foreach (string label in labels)
        {
            if (GetSpriteHash(category, label) == currentHash)
                return label;
        }

        return _target.GetLabel();
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

        return CurveValueToInt(
            curve.Evaluate(animationWindow.time));
    }

    private void AddDiscreteKey(
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
        Keyframe key = new(
            time,
            IntToCurveValue(value),
            float.PositiveInfinity,
            float.PositiveInfinity);

        Undo.RegisterCompleteObjectUndo(clip, undoName);

        int existingIndex = FindKeyAtTime(curve, time);

        if (existingIndex >= 0)
            curve.MoveKey(existingIndex, key);
        else
            curve.AddKey(key);

        AnimationUtility.SetEditorCurve(clip, binding, curve);
        EditorUtility.SetDirty(clip);
        animationWindow.Repaint();
        SceneView.RepaintAll();
    }

    private EditorCurveBinding CreateBinding(
        Transform target,
        Type componentType,
        string propertyName)
    {
        string path = AnimationUtility.CalculateTransformPath(
            target,
            _animationRoot.transform);

        return EditorCurveBinding.DiscreteCurve(
            path,
            componentType,
            propertyName);
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

        SpriteResolver selectedResolver =
            selected.GetComponent<SpriteResolver>();

        if (selectedResolver == null || _parts.Length == 0)
            return;

        int index = Array.IndexOf(_parts, selectedResolver);

        if (index >= 0)
            SelectPart(index);
    }

    private void SetAnimationRoot(Animator animator)
    {
        _animationRoot = animator;
        RefreshParts();
    }

    private void RefreshParts()
    {
        if (_animationRoot == null)
        {
            _parts = Array.Empty<SpriteResolver>();
            _partNames = Array.Empty<string>();
            SelectPart(-1);
            return;
        }

        _parts = _animationRoot
            .GetComponentsInChildren<SpriteResolver>(true)
            .OrderBy(
                resolver => AnimationUtility.CalculateTransformPath(
                    resolver.transform,
                    _animationRoot.transform))
            .ToArray();

        _partNames = _parts
            .Select(
                resolver =>
                {
                    string path = AnimationUtility.CalculateTransformPath(
                        resolver.transform,
                        _animationRoot.transform);
                    string category = resolver.GetCategory();

                    return string.IsNullOrEmpty(category)
                        ? path
                        : $"{path}  [{category}]";
                })
            .ToArray();

        SelectPart(
            _parts.Length > 0
                ? Mathf.Clamp(_partIndex, 0, _parts.Length - 1)
                : -1);
    }

    private void SelectPart(int index)
    {
        _partIndex = index;

        if (index < 0 || index >= _parts.Length)
        {
            _target = null;
            _renderer = null;
            _originalSortingOrder = 0;
            return;
        }

        _target = _parts[index];
        _renderer = _target.GetComponent<SpriteRenderer>();

        if (_renderer == null)
        {
            _originalSortingOrder = 0;
            return;
        }

        SpriteRenderer prefabRenderer =
            PrefabUtility.GetCorrespondingObjectFromSource(_renderer);

        _originalSortingOrder = prefabRenderer != null
            ? prefabRenderer.sortingOrder
            : _renderer.sortingOrder;
    }

    private static AnimationWindow GetAnimationWindow()
    {
        return Resources
            .FindObjectsOfTypeAll<AnimationWindow>()
            .FirstOrDefault();
    }

    private static int GetSpriteHash(
        string category,
        string label)
    {
        const int thirtyBitMask = 0x3FFFFFFF;

        return Animator.StringToHash($"{category}_{label}") &
               thirtyBitMask;
    }

    private static float IntToCurveValue(int value)
    {
        return BitConverter.Int32BitsToSingle(value);
    }

    private static int CurveValueToInt(float value)
    {
        return BitConverter.SingleToInt32Bits(value);
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

#endif
