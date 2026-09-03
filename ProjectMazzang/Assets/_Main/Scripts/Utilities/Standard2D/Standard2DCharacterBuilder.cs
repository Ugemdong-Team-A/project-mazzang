#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CharacterSetup의 각 독립 Builder를 정해진 순서로 실행한다.
/// RAP / Socket 같은 후속 제작 단계는 이 파이프라인에 추가한다.
/// </summary>
public static class Standard2DCharacterBuilder
{
    public static bool BuildOrRefresh(
        Standard2DCharacterSetup setup)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "Play Mode에서는 표준 캐릭터 자동 구성을 실행하지 않습니다.",
                setup);

            return false;
        }

        RefreshReferences(setup);

        if (!TryValidateRoot(
                setup,
                out List<string> rootErrors))
        {
            LogErrors(
                setup,
                "캐릭터 구성 중단",
                rootErrors);

            return false;
        }

        if (!Standard2DIKBuilder.BuildOrRebuild(
                setup.RigIKSetup))
        {
            return false;
        }

        Standard2DResolverBuilder.Result resolverResult =
            Standard2DResolverBuilder.BuildOrRefresh(
                setup.CharacterRoot);

        if (!resolverResult.Success)
        {
            LogErrors(
                setup,
                "SpriteResolver 구성 실패",
                resolverResult.Errors);

            return false;
        }

        RefreshReferences(setup);

        Standard2DVisualDriverBuilder.Result visualResult =
            Standard2DVisualDriverBuilder.BuildOrRefresh(
                setup.CharacterRoot,
                setup.SpriteResolvers);

        EditorUtility.SetDirty(setup);
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            setup);

        if (!visualResult.Success)
        {
            LogErrors(
                setup,
                "Visual Driver 구성 실패",
                visualResult.Errors);

            return false;
        }

        LogWarnings(
            setup,
            visualResult.Warnings);

        Debug.Log(
            $"[{nameof(Standard2DCharacterSetup)} v{Standard2DCharacterSetup.ToolVersion}] " +
            $"'{setup.name}' 표준 캐릭터 구성 완료\n" +
            "IK 생성/갱신 완료\n" +
            $"SpriteResolver: 추가 {resolverResult.AddedCount}, " +
            $"기존 {resolverResult.ExistingCount}, " +
            $"전체 {resolverResult.RendererCount}\n" +
            $"Visual Driver: 추가 {visualResult.AddedCount}, " +
            $"갱신 {visualResult.RefreshedCount}, " +
            $"전체 {visualResult.ResolverCount}",
            setup);

        return true;
    }

    public static bool Validate(
        Standard2DCharacterSetup setup)
    {
        RefreshReferences(setup);

        List<string> errors = new();

        if (!TryValidateRoot(
                setup,
                out List<string> rootErrors))
        {
            errors.AddRange(rootErrors);
        }

        if (setup.RigIKSetup != null &&
            !Standard2DIKBuilder.Validate(
                setup.RigIKSetup,
                false))
        {
            errors.Add(
                "표준 Skeleton / IK 입력 규격이 올바르지 않습니다.");
        }

        if (!Standard2DVisualDriverBuilder.Validate(
                setup.CharacterRoot,
                setup.SpriteResolvers,
                out int resolverCount,
                out List<string> visualErrors,
                out List<string> visualWarnings))
        {
            errors.AddRange(visualErrors);
        }

        LogWarnings(
            setup,
            visualWarnings);

        if (errors.Count > 0)
        {
            LogErrors(
                setup,
                "캐릭터 구성 Invalid",
                errors);

            return false;
        }

        Debug.Log(
            $"[{nameof(Standard2DCharacterSetup)} v{Standard2DCharacterSetup.ToolVersion}] " +
            $"'{setup.name}' 구성 Valid\n" +
            $"Animator / IKSetup 연결 정상\n" +
            $"Visual Driver {resolverCount}개 정상",
            setup);

        return true;
    }

    private static void RefreshReferences(
        Standard2DCharacterSetup setup)
    {
        Undo.RecordObject(
            setup,
            "Refresh Standard Character References");

        if (!setup.RefreshManagedReferences())
            return;

        EditorUtility.SetDirty(setup);
        PrefabUtility.RecordPrefabInstancePropertyModifications(
            setup);
    }

    private static bool TryValidateRoot(
        Standard2DCharacterSetup setup,
        out List<string> errors)
    {
        errors = new List<string>();

        if (setup == null)
        {
            errors.Add(
                "CharacterSetup 참조가 없습니다.");

            return false;
        }

        if (setup.Animator == null)
        {
            errors.Add(
                "Character Root에 Animator가 없습니다.");
        }
        else if (setup.Animator.gameObject != setup.gameObject)
        {
            errors.Add(
                "Animator는 CharacterSetup과 같은 Character Root에 있어야 합니다.");
        }

        if (setup.RigIKSetup == null)
        {
            errors.Add(
                "Character Root에 Standard2DRigIKSetup이 없습니다.");
        }
        else if (setup.RigIKSetup.gameObject != setup.gameObject)
        {
            errors.Add(
                "IKSetup은 CharacterSetup과 같은 Character Root에 있어야 합니다.");
        }

        return errors.Count == 0;
    }

    private static void LogErrors(
        Standard2DCharacterSetup setup,
        string title,
        IEnumerable<string> errors)
    {
        Debug.LogError(
            $"[{nameof(Standard2DCharacterSetup)} v{Standard2DCharacterSetup.ToolVersion}] " +
            $"{title}: '{setup.name}'\n- " +
            string.Join(
                "\n- ",
                errors),
            setup);
    }

    private static void LogWarnings(
        Standard2DCharacterSetup setup,
        IReadOnlyCollection<string> warnings)
    {
        if (warnings == null ||
            warnings.Count == 0)
        {
            return;
        }

        Debug.LogWarning(
            $"[{nameof(Standard2DCharacterSetup)} v{Standard2DCharacterSetup.ToolVersion}] " +
            $"SLA 확인 필요: '{setup.name}'\n- " +
            string.Join(
                "\n- ",
                warnings),
            setup);
    }
}

#endif
