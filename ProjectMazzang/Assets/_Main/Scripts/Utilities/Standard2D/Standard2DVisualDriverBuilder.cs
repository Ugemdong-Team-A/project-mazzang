#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// Character Root 아래의 SpriteResolver마다 Visual Driver를 구성한다.
/// IK Builder나 CharacterSetup의 존재를 알지 않는다.
/// </summary>
public static class Standard2DVisualDriverBuilder
{
    public sealed class Result
    {
        public int ResolverCount { get; internal set; }
        public int AddedCount { get; internal set; }
        public int RefreshedCount { get; internal set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool Success => Errors.Count == 0;
    }

    public static Result BuildOrRefresh(
        Transform characterRoot,
        IReadOnlyList<SpriteResolver> resolvers)
    {
        Result result = new();

        if (characterRoot == null)
        {
            result.Errors.Add(
                "Character Root가 없습니다.");

            return result;
        }

        result.ResolverCount =
            resolvers != null
                ? resolvers.Count
                : 0;

        if (resolvers == null ||
            resolvers.Count == 0)
        {
            result.Errors.Add(
                "Character Root 아래에 SpriteResolver가 없습니다.");

            return result;
        }

        foreach (SpriteResolver resolver in resolvers)
        {
            if (resolver.GetComponent<SpriteRenderer>() == null)
            {
                result.Errors.Add(
                    $"SpriteRenderer 없음: {GetPath(characterRoot, resolver.transform)}");

                continue;
            }

            SpriteVisualAnimationDriver driver =
                resolver.GetComponent<SpriteVisualAnimationDriver>();

            if (driver == null)
            {
                driver =
                    Undo.AddComponent<SpriteVisualAnimationDriver>(
                        resolver.gameObject);

                result.AddedCount++;
            }
            else
            {
                Undo.RecordObject(
                    driver,
                    "Refresh Sprite Visual Driver");

                result.RefreshedCount++;
            }

            // Build는 잘못 저장된 기본 모습을 SLA 규칙에 따라 다시 정리한다.
            driver.SynchronizeDefinition(true);

            EditorUtility.SetDirty(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                driver);

            if (string.IsNullOrEmpty(driver.Category) ||
                driver.Labels.Count == 0)
            {
                result.Warnings.Add(
                    $"SLA 미연결 또는 Category/Label 미설정: " +
                    GetPath(characterRoot, resolver.transform));
            }
        }

        return result;
    }

    public static bool Validate(
        Transform characterRoot,
        IReadOnlyList<SpriteResolver> resolvers,
        out int resolverCount,
        out List<string> errors,
        out List<string> warnings)
    {
        errors = new List<string>();
        warnings = new List<string>();
        resolverCount = 0;

        if (characterRoot == null)
        {
            errors.Add(
                "Character Root가 없습니다.");

            return false;
        }

        resolverCount =
            resolvers != null
                ? resolvers.Count
                : 0;

        if (resolvers == null ||
            resolvers.Count == 0)
        {
            errors.Add(
                "Character Root 아래에 SpriteResolver가 없습니다.");

            return false;
        }

        foreach (SpriteResolver resolver in resolvers)
        {
            string path =
                GetPath(
                    characterRoot,
                    resolver.transform);

            if (resolver.GetComponent<SpriteRenderer>() == null)
            {
                errors.Add(
                    $"SpriteRenderer 없음: {path}");

                continue;
            }

            SpriteVisualAnimationDriver driver =
                resolver.GetComponent<SpriteVisualAnimationDriver>();

            if (driver == null)
            {
                errors.Add(
                    $"Visual Driver 없음: {path}");

                continue;
            }

            if (string.IsNullOrEmpty(driver.Category) ||
                driver.Labels.Count == 0)
            {
                warnings.Add(
                    $"Visual Driver SLA 정보 없음: {path}");
            }
        }

        return errors.Count == 0;
    }

    private static string GetPath(
        Transform root,
        Transform target)
    {
        if (target == root)
            return root.name;

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
            root.name + "/" +
            string.Join("/", names);
    }
}

#endif
