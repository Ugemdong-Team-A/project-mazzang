using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.U2D.Animation;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteResolver))]
[RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("Mazzang/Animation/Sprite Visual Animation Driver")]
[MovedFrom(true, null, null, "SpriteVisualAnimationTest")]
public sealed class SpriteVisualAnimationDriver : MonoBehaviour
{
    // 기존 테스트 클립의 Animation Binding을 유지하기 위해 이름을 보존한다.
    [SerializeField, HideInInspector]
    private int pose;

    [SerializeField, HideInInspector]
    private int _sortingOrder;

    [SerializeField, HideInInspector]
    private string category;

    [SerializeField, HideInInspector]
    private string[] _labels = Array.Empty<string>();

    [SerializeField, HideInInspector]
    private int _originalSortingOrder;

    [SerializeField, HideInInspector]
    private bool _initialized;

    // 이전 테스트 데이터에서 Label 순서를 이관할 때만 사용한다.
    [SerializeField, HideInInspector]
    private string defaultLabel = "Default";

    [SerializeField, HideInInspector]
    private string readyLabel = "CounterReady";

    [SerializeField, HideInInspector]
    private string attackLabel = "CounterAttack";

    [SerializeField, HideInInspector]
    private int defaultSortingOrder;

    private SpriteResolver _resolver;
    private SpriteRenderer _renderer;

    public string Category => category;
    public IReadOnlyList<string> Labels => _labels;
    public int LabelIndex => pose;
    public int SortingOrder => _sortingOrder;
    public int OriginalSortingOrder => _originalSortingOrder;
    public SpriteResolver Resolver => _resolver;
    public SpriteRenderer Renderer => _renderer;

    private void OnEnable()
    {
        CacheComponents();
        Apply();
    }

    private void OnValidate()
    {
        CacheComponents();
        Apply();
    }

    private void OnDidApplyAnimationProperties()
    {
        CacheComponents();
        Apply();
    }

    public bool SynchronizeDefinition()
    {
        CacheComponents();

        if (_resolver == null || _renderer == null)
            return false;

        SpriteLibrary library = _resolver.spriteLibrary;
        SpriteLibraryAsset libraryAsset =
            library != null ? library.spriteLibraryAsset : null;

        string resolvedCategory = _resolver.GetCategory();

        if (libraryAsset == null ||
            string.IsNullOrEmpty(resolvedCategory))
        {
            return false;
        }

        List<string> labels = new();

        AddLegacyLabelIfPresent(
            labels,
            libraryAsset,
            resolvedCategory,
            defaultLabel);
        AddLegacyLabelIfPresent(
            labels,
            libraryAsset,
            resolvedCategory,
            readyLabel);
        AddLegacyLabelIfPresent(
            labels,
            libraryAsset,
            resolvedCategory,
            attackLabel);

        foreach (string label in libraryAsset
                     .GetCategoryLabelNames(resolvedCategory)
                     .OrderBy(item => item))
        {
            if (!labels.Contains(label))
                labels.Add(label);
        }

        if (labels.Count == 0)
            return false;

        string currentLabel = _resolver.GetLabel();
        int currentIndex = labels.IndexOf(currentLabel);
        bool changed =
            !_initialized ||
            category != resolvedCategory ||
            !_labels.SequenceEqual(labels);

        if (!_initialized)
        {
            _originalSortingOrder = defaultSortingOrder;

            if (_originalSortingOrder == 0 &&
                defaultSortingOrder == 0)
            {
                _originalSortingOrder = _renderer.sortingOrder;
            }

            _sortingOrder = _renderer.sortingOrder;
            pose = currentIndex >= 0 ? currentIndex : 0;
        }
        else if (pose < 0 || pose >= labels.Count)
        {
            pose = currentIndex >= 0 ? currentIndex : 0;
            changed = true;
        }

        category = resolvedCategory;
        _labels = labels.ToArray();
        _initialized = true;

        Apply();
        return changed;
    }

    public void PreviewLabel(int labelIndex)
    {
        pose = labelIndex;
        ApplyLabel();
    }

    public void PreviewSortingOrder(int sortingOrder)
    {
        _sortingOrder = sortingOrder;
        ApplySortingOrder();
    }

    private void CacheComponents()
    {
        if (_resolver == null)
            _resolver = GetComponent<SpriteResolver>();

        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }

    private void Apply()
    {
        if (!_initialized)
            return;

        ApplyLabel();
        ApplySortingOrder();
    }

    private void ApplyLabel()
    {
        if (_resolver == null ||
            string.IsNullOrEmpty(category) ||
            _labels == null ||
            pose < 0 ||
            pose >= _labels.Length)
        {
            return;
        }

        _resolver.SetCategoryAndLabel(
            category,
            _labels[pose]);
    }

    private void ApplySortingOrder()
    {
        if (_renderer != null)
            _renderer.sortingOrder = _sortingOrder;
    }

    private static void AddLegacyLabelIfPresent(
        ICollection<string> result,
        SpriteLibraryAsset libraryAsset,
        string targetCategory,
        string candidate)
    {
        if (string.IsNullOrEmpty(candidate) ||
            libraryAsset.GetSprite(targetCategory, candidate) == null)
        {
            return;
        }

        result.Add(candidate);
    }
}
