using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PSB Sprite Layer와 실제 Skeleton Bone을 혼동하지 않도록
/// Skeleton root를 구조(signature)로 판별하고,
/// 그 root부터 부모 -> 직계 자식 순서로만 Bone을 resolve한다.
/// </summary>
public static class Standard2DRigResolver
{
    public sealed class Result
    {
        public Transform SkeletonRoot { get; internal set; }

        public Dictionary<string, Transform> Bones { get; } =
            new(StringComparer.Ordinal);

        public List<string> Aliases { get; } =
            new();
    }

    public static bool TryResolve(
        Transform searchRoot,
        out Result result,
        out List<string> errors)
    {
        result = new Result();
        errors = new List<string>();

        Transform skeletonRoot =
            FindSkeletonRoot(
                searchRoot,
                out string rootError);

        if (skeletonRoot == null)
        {
            errors.Add(rootError);
            return false;
        }

        result.SkeletonRoot =
            skeletonRoot;

        result.Bones["root"] =
            skeletonRoot;

        RegisterAlias(
            result,
            "root",
            skeletonRoot);

        foreach (Standard2DRigDefinition.BoneLink link
                 in Standard2DRigDefinition.RequiredHierarchy)
        {
            if (!result.Bones.TryGetValue(
                    link.Parent,
                    out Transform parent))
            {
                errors.Add(
                    $"Resolve Error: '{link.Child}'의 부모 논리 Bone " +
                    $"'{link.Parent}'을 먼저 찾지 못했습니다.");

                continue;
            }

            List<Transform> directMatches =
                FindDirectBoneChildren(
                    parent,
                    link.Child);

            if (directMatches.Count == 1)
            {
                Transform found =
                    directMatches[0];

                result.Bones[link.Child] =
                    found;

                RegisterAlias(
                    result,
                    link.Child,
                    found);

                continue;
            }

            if (directMatches.Count > 1)
            {
                errors.Add(
                    $"Ambiguous Bone: '{parent.name}' 바로 아래에 " +
                    $"'{link.Child}' 후보가 {directMatches.Count}개 있습니다. " +
                    $"({string.Join(", ", GetNames(directMatches))})");

                continue;
            }

            // 진단용 검색일 뿐, 이 결과를 Bone으로 채택하지 않는다.
            List<Transform> misplaced =
                FindBoneDescendants(
                    skeletonRoot,
                    link.Child);

            if (misplaced.Count == 1)
            {
                Transform item =
                    misplaced[0];

                string actualParent =
                    item.parent != null
                        ? item.parent.name
                        : "<none>";

                errors.Add(
                    $"Hierarchy Mismatch: 논리 Bone '{link.Child}'은 " +
                    $"'{link.Parent}' 바로 아래에 있어야 하지만, " +
                    $"실제 '{item.name}'의 Parent는 '{actualParent}'입니다.");

                continue;
            }

            if (misplaced.Count > 1)
            {
                errors.Add(
                    $"Ambiguous Bone: Skeleton 내부에 '{link.Child}' 후보가 " +
                    $"{misplaced.Count}개 있습니다. " +
                    $"({string.Join(", ", GetNames(misplaced))})");

                continue;
            }

            errors.Add(
                $"Missing Bone: '{link.Child}' " +
                $"(허용: {link.Child}, {link.Child}_1, {link.Child}_2 ...)");
        }

        return errors.Count == 0;
    }

    public static bool IsLogicalBoneName(
        string actualName,
        string logicalName)
    {
        if (string.Equals(
                actualName,
                logicalName,
                StringComparison.Ordinal))
        {
            return true;
        }

        string prefix =
            logicalName + "_";

        if (!actualName.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string suffix =
            actualName.Substring(
                prefix.Length);

        if (suffix.Length == 0)
            return false;

        for (int i = 0;
             i < suffix.Length;
             i++)
        {
            if (!char.IsDigit(suffix[i]))
                return false;
        }

        return true;
    }

    private static Transform FindSkeletonRoot(
        Transform searchRoot,
        out string error)
    {
        error = null;

        if (searchRoot == null)
        {
            error =
                "Rig Search Root가 null입니다.";

            return null;
        }

        if (IsLogicalBoneName(
                searchRoot.name,
                "root") &&
            HasSkeletonRootSignature(
                searchRoot))
        {
            return searchRoot;
        }

        Transform[] allTransforms =
            searchRoot.GetComponentsInChildren<Transform>(
                true);

        List<Transform> nameCandidates =
            new();

        List<Transform> validCandidates =
            new();

        foreach (Transform candidate
                 in allTransforms)
        {
            if (!IsLogicalBoneName(
                    candidate.name,
                    "root"))
            {
                continue;
            }

            nameCandidates.Add(candidate);

            if (HasSkeletonRootSignature(
                    candidate))
            {
                validCandidates.Add(candidate);
            }
        }

        if (validCandidates.Count == 1)
            return validCandidates[0];

        if (validCandidates.Count > 1)
        {
            error =
                "실제 Skeleton root 후보가 여러 개입니다. " +
                "Rig Search Root를 직접 지정해주세요.\n- " +
                string.Join(
                    "\n- ",
                    GetPaths(validCandidates));

            return null;
        }

        if (nameCandidates.Count > 0)
        {
            error =
                "root 이름 후보는 찾았지만 Skeleton 구조를 만족하지 않습니다.\n" +
                "필요 Signature: root -> pelvis -> abdomen -> chest, " +
                "그리고 pelvis -> hip\n- " +
                string.Join(
                    "\n- ",
                    GetPaths(nameCandidates));

            return null;
        }

        error =
            "실제 Skeleton root를 찾지 못했습니다. " +
            "컴포넌트를 Player/Character Root에 붙이고 " +
            "Rig Search Root는 우선 비워두세요.";

        return null;
    }

    private static bool HasSkeletonRootSignature(
        Transform rootCandidate)
    {
        List<Transform> pelvisMatches =
            FindDirectBoneChildren(
                rootCandidate,
                "pelvis");

        if (pelvisMatches.Count != 1)
            return false;

        Transform pelvis =
            pelvisMatches[0];

        List<Transform> abdomenMatches =
            FindDirectBoneChildren(
                pelvis,
                "abdomen");

        List<Transform> hipMatches =
            FindDirectBoneChildren(
                pelvis,
                "hip");

        if (abdomenMatches.Count != 1 ||
            hipMatches.Count != 1)
        {
            return false;
        }

        Transform abdomen =
            abdomenMatches[0];

        return
            FindDirectBoneChildren(
                abdomen,
                "chest").Count == 1;
    }

    private static List<Transform>
        FindDirectBoneChildren(
            Transform parent,
            string logicalName)
    {
        List<Transform> result =
            new();

        if (parent == null)
            return result;

        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform child =
                parent.GetChild(i);

            if (IsLogicalBoneName(
                    child.name,
                    logicalName))
            {
                result.Add(child);
            }
        }

        return result;
    }

    private static List<Transform>
        FindBoneDescendants(
            Transform root,
            string logicalName)
    {
        List<Transform> result =
            new();

        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(
                true);

        foreach (Transform current
                 in descendants)
        {
            if (current == root)
                continue;

            if (IsLogicalBoneName(
                    current.name,
                    logicalName))
            {
                result.Add(current);
            }
        }

        return result;
    }

    private static void RegisterAlias(
        Result result,
        string logicalName,
        Transform actual)
    {
        if (string.Equals(
                logicalName,
                actual.name,
                StringComparison.Ordinal))
        {
            return;
        }

        result.Aliases.Add(
            $"{logicalName} -> {actual.name}");
    }

    private static IEnumerable<string>
        GetNames(
            IEnumerable<Transform> transforms)
    {
        foreach (Transform item
                 in transforms)
        {
            yield return
                item != null
                    ? item.name
                    : "<null>";
        }
    }

    private static IEnumerable<string>
        GetPaths(
            IEnumerable<Transform> transforms)
    {
        foreach (Transform item
                 in transforms)
        {
            yield return
                GetPath(item);
        }
    }

    public static string GetPath(
        Transform target)
    {
        if (target == null)
            return "<null>";

        Stack<string> names =
            new();

        Transform current =
            target;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }
}
