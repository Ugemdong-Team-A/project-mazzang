#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택된 상체 기준 본 아래에 RAP과 Weapon Socket을 생성하고 검증한다.
/// Player 모듈이나 네트워크 컴포넌트는 참조하거나 할당하지 않는다.
/// </summary>
public static class Standard2DReferenceBuilder
{
    public const string PivotName = "ResolvedAimPivot";
    public const string SocketName = "WeaponSocket";

    private static readonly string[] LegacyPivotNames =
    {
        PivotName,
        "ResolveAimPivot"
    };

    public static bool BuildOrRefresh(
        Transform characterRoot,
        Transform rigSearchRoot,
        Transform selectedReferenceBone)
    {
        if (!TryResolveSelectedBone(
                rigSearchRoot,
                selectedReferenceBone,
                out Transform referenceBone,
                out string error))
        {
            Debug.LogError(
                $"[{nameof(Standard2DReferenceBuilder)}] {error}",
                characterRoot);
            return false;
        }

        Undo.SetCurrentGroupName(
            "Build Standard 2D Aim References");

        int undoGroup = Undo.GetCurrentGroup();

        Standard2DAimAnchor existingAnchor =
            characterRoot.GetComponentInChildren<Standard2DAimAnchor>(
                true);

        Transform pivot = existingAnchor != null
            ? existingAnchor.transform
            : FindUniqueTransform(
                characterRoot,
                LegacyPivotNames);

        if (pivot == null)
            pivot = CreateTransform(PivotName);

        ConfigureTransform(
            pivot,
            referenceBone,
            Vector3.zero,
            Quaternion.identity);

        if (pivot.name != PivotName)
        {
            Undo.RecordObject(
                pivot.gameObject,
                "Normalize Resolved Aim Pivot Name");
            pivot.name = PivotName;
        }

        Standard2DAimAnchor anchor =
            pivot.GetComponent<Standard2DAimAnchor>();

        if (anchor == null)
        {
            anchor = Undo.AddComponent<Standard2DAimAnchor>(
                pivot.gameObject);
        }

        Transform socket = anchor.WeaponSocket != null
            ? anchor.WeaponSocket
            : FindUniqueTransform(
                characterRoot,
                new[] { SocketName });

        if (socket == null)
            socket = CreateTransform(SocketName);

        ConfigureTransform(
            socket,
            pivot,
            Vector3.zero,
            Quaternion.Euler(
                0f,
                0f,
                -90f));

        Undo.RecordObject(
            anchor,
            "Synchronize Standard Aim Anchor");

        if (anchor.Synchronize(socket))
            EditorUtility.SetDirty(anchor);

        RecordPrefabChanges(
            pivot.gameObject,
            pivot,
            anchor,
            socket.gameObject,
            socket);

        Undo.CollapseUndoOperations(undoGroup);

        return Validate(
            characterRoot,
            rigSearchRoot,
            selectedReferenceBone,
            false);
    }

    public static bool Validate(
        Transform characterRoot,
        Transform rigSearchRoot,
        Transform selectedReferenceBone,
        bool logErrors = true)
    {
        List<string> errors = new();

        if (!TryResolveSelectedBone(
                rigSearchRoot,
                selectedReferenceBone,
                out Transform referenceBone,
                out string referenceError))
        {
            errors.Add(referenceError);
        }

        Standard2DAimAnchor anchor =
            characterRoot != null
                ? characterRoot.GetComponentInChildren<Standard2DAimAnchor>(true)
                : null;

        if (anchor == null)
        {
            errors.Add("ResolvedAimPivot에 Standard2DAimAnchor가 없습니다.");
        }
        else
        {
            if (anchor.transform.name != PivotName)
                errors.Add($"RAP 이름은 '{PivotName}'이어야 합니다.");

            if (referenceBone != null &&
                anchor.ReferenceBone != referenceBone)
            {
                errors.Add("RAP은 선택한 상체 기준 본의 직접 자식이어야 합니다.");
            }

            if (!IsIdentityPose(anchor.transform))
                errors.Add("RAP의 로컬 위치/회전/크기는 0° 원점과 1이어야 합니다.");

            Transform socket = anchor.WeaponSocket;

            if (socket == null)
            {
                errors.Add("WeaponSocket이 없습니다.");
            }
            else
            {
                if (socket.parent != anchor.transform)
                    errors.Add("WeaponSocket은 RAP의 직접 자식이어야 합니다.");

                if (socket.localPosition.sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(
                        socket.localRotation,
                        Quaternion.Euler(0f, 0f, -90f)) > 0.01f ||
                    Vector3.Distance(
                        socket.localScale,
                        Vector3.one) > 0.0001f)
                {
                    errors.Add("WeaponSocket은 로컬 원점, Z -90°, 크기 1이어야 합니다.");
                }
            }
        }

        if (logErrors && errors.Count > 0)
        {
            Debug.LogError(
                $"[{nameof(Standard2DReferenceBuilder)}] " +
                $"'{characterRoot?.name}' 참조 구성 Invalid\n- " +
                string.Join("\n- ", errors),
                characterRoot);
        }

        return errors.Count == 0;
    }

    public static bool TryGetSelectableBones(
        Transform rigSearchRoot,
        out Transform[] bones,
        out string error)
    {
        bones = Array.Empty<Transform>();
        error = null;

        if (rigSearchRoot == null)
        {
            error = "IK Setup이 없어 상체 기준 본을 찾을 수 없습니다.";
            return false;
        }

        if (!Standard2DRigResolver.TryResolve(
                rigSearchRoot,
                out Standard2DRigResolver.Result rig,
                out List<string> rigErrors))
        {
            error = string.Join("\n", rigErrors);
            return false;
        }

        bones = Standard2DRigDefinition.BodyAimReferenceBones
            .Select(name => rig.Bones[name])
            .ToArray();

        return true;
    }

    private static bool TryResolveSelectedBone(
        Transform rigSearchRoot,
        Transform selectedReferenceBone,
        out Transform referenceBone,
        out string error)
    {
        referenceBone = null;
        error = null;

        if (!TryGetSelectableBones(
                rigSearchRoot,
                out Transform[] selectableBones,
                out error))
        {
            return false;
        }

        referenceBone = selectedReferenceBone;

        if (referenceBone == null)
        {
            error = "상체 기준 본을 선택해주세요.";
            return false;
        }

        if (!selectableBones.Contains(referenceBone))
        {
            error = "상체 기준 본은 abdomen, chest, neck 중 하나여야 합니다.";
            return false;
        }

        return true;
    }

    private static Transform FindUniqueTransform(
        Transform root,
        IReadOnlyCollection<string> names)
    {
        Transform[] matches = root
            .GetComponentsInChildren<Transform>(true)
            .Where(item => names.Contains(item.name))
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : null;
    }

    private static Transform CreateTransform(
        string name)
    {
        GameObject created = new(name);
        Undo.RegisterCreatedObjectUndo(
            created,
            $"Create {name}");
        return created.transform;
    }

    private static void ConfigureTransform(
        Transform target,
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        Undo.RecordObject(
            target,
            $"Configure {target.name}");

        target.SetParent(parent, false);
        target.localPosition = localPosition;
        target.localRotation = localRotation;
        target.localScale = Vector3.one;
    }

    private static bool IsIdentityPose(
        Transform target)
    {
        return target.localPosition.sqrMagnitude <= 0.000001f &&
               Quaternion.Angle(
                   target.localRotation,
                   Quaternion.identity) <= 0.01f &&
               Vector3.Distance(
                   target.localScale,
                   Vector3.one) <= 0.0001f;
    }

    private static void RecordPrefabChanges(
        params UnityEngine.Object[] objects)
    {
        foreach (UnityEngine.Object item in objects)
        {
            EditorUtility.SetDirty(item);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }
    }
}

#endif
