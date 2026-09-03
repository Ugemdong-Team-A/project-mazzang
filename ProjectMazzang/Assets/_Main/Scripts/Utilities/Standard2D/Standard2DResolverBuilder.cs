#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// Character Root 아래의 SpriteRenderer마다 SpriteResolver를 보장한다.
/// SpriteLibraryAsset 연결 전에도 생성할 수 있으며 원본 Sprite는 변경하지 않는다.
/// </summary>
public static class Standard2DResolverBuilder
{
    public sealed class Result
    {
        public int RendererCount { get; internal set; }
        public int AddedCount { get; internal set; }
        public int ExistingCount { get; internal set; }
        public int CategoryConfiguredCount { get; internal set; }
        public List<string> Errors { get; } = new();
        public bool Success => Errors.Count == 0;
    }

    public static Result BuildOrRefresh(
        Transform characterRoot)
    {
        Result result = new();

        if (characterRoot == null)
        {
            result.Errors.Add(
                "Character Root가 없습니다.");

            return result;
        }

        SpriteRenderer[] renderers =
            characterRoot.GetComponentsInChildren<SpriteRenderer>(
                true);

        result.RendererCount = renderers.Length;

        if (renderers.Length == 0)
        {
            result.Errors.Add(
                "Character Root 아래에 SpriteRenderer가 없습니다.");

            return result;
        }

        foreach (SpriteRenderer renderer in renderers)
        {
            SpriteResolver resolver =
                renderer.GetComponent<SpriteResolver>();

            if (resolver != null)
            {
                result.ExistingCount++;
            }
            else
            {
                resolver =
                    Undo.AddComponent<SpriteResolver>(
                        renderer.gameObject);

                if (resolver == null)
                {
                    result.Errors.Add(
                        $"SpriteResolver 추가 실패: " +
                        GetPath(characterRoot, renderer.transform));

                    continue;
                }

                result.AddedCount++;
                EditorUtility.SetDirty(resolver);
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    resolver);
            }

            if (TryConfigureCategoryFromObjectName(
                    renderer,
                    resolver))
            {
                result.CategoryConfiguredCount++;
            }
        }

        return result;
    }

    private static bool TryConfigureCategoryFromObjectName(
        SpriteRenderer renderer,
        SpriteResolver resolver)
    {
        if (!string.IsNullOrEmpty(
                resolver.GetCategory()))
        {
            return false;
        }

        SpriteLibrary library =
            resolver.spriteLibrary;

        SpriteLibraryAsset libraryAsset =
            library != null
                ? library.spriteLibraryAsset
                : null;

        if (libraryAsset == null)
            return false;

        string category =
            libraryAsset
                .GetCategoryNames()
                .FirstOrDefault(
                    item => string.Equals(
                        item,
                        renderer.gameObject.name,
                        StringComparison.Ordinal));

        if (string.IsNullOrEmpty(category))
            return false;

        string[] labels =
            libraryAsset
                .GetCategoryLabelNames(category)
                .ToArray();

        if (labels.Length == 0)
            return false;

        string label =
            labels.FirstOrDefault(
                item => string.Equals(
                    item,
                    category,
                    StringComparison.Ordinal));

        if (string.IsNullOrEmpty(label))
        {
            label = labels.FirstOrDefault(
                item => string.Equals(
                    item,
                    "Default",
                    StringComparison.Ordinal));
        }

        if (string.IsNullOrEmpty(label))
            label = labels[0];

        Undo.RecordObject(
            resolver,
            "Configure Sprite Resolver Category");

        resolver.SetCategoryAndLabel(
            category,
            label);

        EditorUtility.SetDirty(resolver);
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            resolver);

        return true;
    }

    private static string GetPath(
        Transform root,
        Transform target)
    {
        List<string> names = new();
        Transform current = target;

        while (current != null &&
               current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();

        return
            names.Count == 0
                ? root.name
                : root.name + "/" + string.Join("/", names);
    }
}

#endif
